import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const {
  analyzePacketPages,
  buildPacketReviewReceipt,
  comparePacketToActiveDocument,
  createPacketReviewReport,
  projectPacketEvidenceV2
} = await import("../src/packet-review/packetReview.ts");

const pages = [
  {
    pageNumber: 1,
    text: [
      "WALMART RETAIL FIXTURE",
      "DRAWING NO ASY511185-80238229",
      "REV B",
      "DESCRIPTION: OPTICAL VALUE SIGN HOLDER",
      "MATERIAL: A1008",
      "THICKNESS: 0.0747 IN",
      "DRAWN BY: CW",
      "CUSTOMER ABBREVIATION: WM",
      "CONFIGURATION: Default",
      "SHEET 1 OF 2",
      "BILL OF MATERIALS",
      "ITEM QTY PART NUMBER DESCRIPTION",
      "1 2 PRT511284-80241102 BRACKET",
      "2 1 PRT511285-80241103 PANEL",
      "GENERAL NOTES:",
      "1. REMOVE ALL SHARP EDGES.",
      "2. FINISH: BLACK POWDER COAT.",
      "12.50 ±0.02"
    ].join("\n")
  },
  {
    pageNumber: 2,
    text: [
      "DETAIL A",
      "PRT511284-80241102",
      "4X Ø.250 THRU",
      "90°",
      "SHEET 2 OF 2"
    ].join("\n")
  }
];

const review = analyzePacketPages("Walmart-ASY511185-80238229.pdf", pages);
assert.equal(review.pageCount, 2);
assert.equal(review.titleBlocks.length, 2, "sheet markers create page-level title block evidence");
assert.deepEqual(review.partNumbers, [
  "ASY511185-80238229",
  "PRT511284-80241102",
  "PRT511285-80241103"
]);
assert.equal(review.bomRecords.length, 2, "BOM rows are extracted deterministically");
assert.ok(review.dimensions.some((item) => item.text.includes("12.50")));
assert.ok(review.dimensions.some((item) => item.text.includes("Ø.250")));
assert.ok(review.notes.some((item) => item.text.includes("SHARP EDGES")));
assert.ok(review.drawingGroups.some((item) => item.label === "Assembly"));
assert.ok(review.drawingGroups.some((item) => item.label === "Part"));
assert.ok(review.findings.some((item) => item.code === "PACKET_EVIDENCE_READY"));
assert.ok(review.findings.every((item) => item.evidence.length > 0));

const packetEvidenceV2 = await projectPacketEvidenceV2(review);
assert.equal(review.schemaVersion, "vira.packet-review.v1", "the existing packet review contract remains unchanged");
assert.equal(packetEvidenceV2.schemaVersion, "vira.packet-evidence.v2");
assert.match(packetEvidenceV2.snapshotId, /^packet-sha256:/);
assert.equal(packetEvidenceV2.identifiers.length, 3);
assert.ok(packetEvidenceV2.identifiers.every((item) => item.candidateDocumentTitleHashes.length === 4));
assert.deepEqual(
  packetEvidenceV2.properties.map((item) => item.name).sort(),
  ["Configuration", "CustomerAbbreviation", "Description", "DrawnBy", "Material", "Revision", "Thickness"]
);
assert.ok(packetEvidenceV2.properties.every((item) => item.evidence.pageOrSheet === "1"));
assert.ok(packetEvidenceV2.properties.every((item) => item.evidence.authority === "Observed"));
assert.ok(packetEvidenceV2.properties.every((item) => item.evidence.verificationStatus === "Confirmed"));

const splitTitleBlockReview = analyzePacketPages("split-title-block-fixture.pdf", [
  {
    pageNumber: 3,
    text: [
      "DOC NO",
      "PART NO",
      "80230001",
      "ASY500001",
      "SHEET 1 OF 2"
    ].join("\n")
  },
  {
    pageNumber: 4,
    text: [
      "DOC NO",
      "PART NO",
      "80230001",
      "ASY500001",
      "SHEET 2 OF 2"
    ].join("\n")
  },
  {
    pageNumber: 5,
    text: [
      "PART NO",
      "DOC NO",
      "MPM59609",
      "80103477"
    ].join("\n")
  },
  {
    pageNumber: 6,
    text: [
      "80230002",
      "ASY500002"
    ].join("\n")
  },
  {
    pageNumber: 7,
    text: [
      "DOC NO",
      "PART NO",
      "80230003",
      "UNRELATED",
      "ASY500003"
    ].join("\n")
  },
  {
    pageNumber: 8,
    text: [
      "DOC NO",
      "PART NO",
      "80230004",
      "XYZ500004"
    ].join("\n")
  },
  {
    pageNumber: 9,
    text: [
      "DOC NO",
      "PART NO",
      "8023005",
      "ASY500005"
    ].join("\n")
  },
  {
    pageNumber: 10,
    text: [
      "DOC NO",
      "PART NO"
    ].join("\n")
  },
  {
    pageNumber: 11,
    text: [
      "80230006",
      "ASY500006"
    ].join("\n")
  }
]);
assert.deepEqual(
  splitTitleBlockReview.partNumbers,
  ["ASY500001-80230001", "MPM59609-80103477"],
  "only governed, same-page, label-anchored title-block field pairs become identifiers"
);
const splitTitleBlockEvidence = await projectPacketEvidenceV2(splitTitleBlockReview);
const repeatedAssemblyIdentity = splitTitleBlockEvidence.identifiers.find((item) => item.identifier === "ASY500001-80230001");
assert.equal(repeatedAssemblyIdentity?.evidence.pageOrSheet, "3");
assert.equal(repeatedAssemblyIdentity?.evidence.extractionMethod, "deterministic-title-block-field-pair");
assert.equal(repeatedAssemblyIdentity?.evidence.ruleId, "VIRA-PACKET-IDENTIFIER-TITLE-BLOCK-PAIR-001");
assert.equal(repeatedAssemblyIdentity?.evidence.authority, "Derived");
assert.equal(repeatedAssemblyIdentity?.evidence.verificationStatus, "Candidate");
assert.deepEqual(
  repeatedAssemblyIdentity?.supportingEvidence?.map((item) => item.pageOrSheet),
  ["3", "4"],
  "deduplicated identifiers retain every supporting page occurrence"
);
assert.equal(
  splitTitleBlockEvidence.identifiers.find((item) => item.identifier === "MPM59609-80103477")?.evidence.pageOrSheet,
  "5",
  "legacy five-digit controlled part numbers remain supported when explicitly labeled"
);

const viraBomReview = analyzePacketPages("vira-prefix-fixture.pdf", [{
  pageNumber: 7,
  text: [
    "1 1 SUB511200-80240000 WELDED SUBASSEMBLY",
    "2 2 MPM511284-80241102 MOUNTING BRACKET",
    "3 1 MPP511285-80241103 PRINTED PANEL",
    "4 1 MPW511286-80241104 WELDMENT",
    "5 8 HWD511287-80241105 SCREW",
    "6 1 GRA511288-80241106 GRAPHIC",
    "7 1 LVL511289-80241107 LEVEL",
    "8 1 PBO511290-80241108 CUSTOMER PURCHASED ITEM",
    "9 1 REF511291-80241109 REFERENCE ONLY",
    "10 1 EXIST511292-80241110 EXISTING ITEM",
    "11 4 HWD511293-80241111 STORE SUPPLIED FASTENER"
  ].join("\n")
}]);
const viraBomEvidence = await projectPacketEvidenceV2(viraBomReview);
assert.equal(viraBomEvidence.bomRows.length, 11, "all governed VIRA BOM identifier families are projected");
assert.deepEqual(
  viraBomEvidence.bomRows.map((item) => item.identifier),
  ["SUB511200-80240000", "MPM511284-80241102", "MPP511285-80241103", "MPW511286-80241104", "HWD511287-80241105", "GRA511288-80241106", "LVL511289-80241107", "PBO511290-80241108", "REF511291-80241109", "EXIST511292-80241110", "HWD511293-80241111"]
);
assert.equal(viraBomEvidence.bomRows.find((item) => item.identifier.startsWith("PBO"))?.responsibility, "PurchasedByOthers");
assert.equal(viraBomEvidence.bomRows.find((item) => item.identifier.startsWith("REF"))?.responsibility, "Reference");
assert.equal(viraBomEvidence.bomRows.find((item) => item.identifier.startsWith("EXIST"))?.responsibility, "Existing");
assert.equal(viraBomEvidence.bomRows.find((item) => item.description.includes("STORE SUPPLIED"))?.responsibility, "StoreSupplied");
assert.ok(viraBomEvidence.bomRows.every((item) => item.evidence.pageOrSheet === "7"));
assert.ok(viraBomEvidence.bomRows.every((item) => item.evidence.regionOrNativePath.startsWith("bom-row:")));

const unavailableComparison = await comparePacketToActiveDocument(review, {
  state: "unavailable",
  message: "Adapter unavailable.",
  documentType: "Unknown document",
  titleHash: "redacted",
  customPropertyCount: 0,
  mutationActions: 0
});
assert.equal(unavailableComparison.status, "needs-verification");
assert.match(unavailableComparison.summary, /unavailable/i);

const matchingHash = await crypto.subtle.digest(
  "SHA-256",
  new TextEncoder().encode("ASY511185-80238229.SLDASM")
);
const titleHash = [...new Uint8Array(matchingHash)]
  .map((value) => value.toString(16).padStart(2, "0"))
  .join("")
  .slice(0, 16);
const confirmedComparison = await comparePacketToActiveDocument(review, {
  state: "ready",
  message: "Context ready.",
  documentType: "Assembly",
  titleHash: `sha256:${titleHash}`,
  isDirty: false,
  isReadOnly: true,
  customPropertyCount: 4,
  mutationActions: 0
});
assert.equal(confirmedComparison.status, "confirmed");
assert.equal(confirmedComparison.matchedPartNumber, "ASY511185-80238229");

const report = createPacketReviewReport(review, confirmedComparison);
assert.match(report, /# VIRA Packet Review/);
assert.match(report, /ASY511185-80238229/);
assert.match(report, /PACKET_EVIDENCE_READY/);
assert.doesNotMatch(report, /blob:/, "derived report does not leak browser object URLs");

const receipt = buildPacketReviewReceipt(review, confirmedComparison, "2026-07-13T12:00:00.000Z");
assert.equal(receipt.schemaVersion, "vira.packet-review.receipt.v1");
assert.equal(receipt.source.localOnly, true);
assert.equal(receipt.source.fileName, "Walmart-ASY511185-80238229.pdf");
assert.equal(receipt.actions.engineeringMutations, 0);
assert.equal(receipt.verification.liveSolidWorksMutation, false);

const appSource = await readFile(new URL("../src/App.tsx", import.meta.url), "utf8");
const panelSource = await readFile(new URL("../src/packet-review/PacketReviewPanel.tsx", import.meta.url), "utf8");
assert.match(appSource, /<PacketReviewPanel context=\{activeDocumentContext\} \/>/);
assert.match(panelSource, /accept="application\/pdf,\.pdf"/);
assert.match(panelSource, /aria-label="Choose engineering PDF packet"/);
assert.match(panelSource, /Local file only/);
assert.match(panelSource, /Export report/);
assert.match(panelSource, /Export receipt/);
assert.match(panelSource, /role="status"/);
assert.match(panelSource, /Derived output ready:/);
assert.match(panelSource, /Packet ↔ CAD Phase A/);
assert.match(panelSource, /Identity authority/);
assert.match(panelSource, /Static build · read only/);
assert.match(panelSource, /CAD evidence/);

console.log(JSON.stringify({
  ok: true,
  checked: [
    "packet evidence extraction",
    "title-block field-pair identifiers",
    "identifier rejection boundaries",
    "multi-page identifier provenance",
    "drawing groups",
    "BOM records",
    "dimensions and notes",
    "active-document comparison",
    "derived report",
    "execution receipt",
    "task-pane integration"
  ]
}, null, 2));
