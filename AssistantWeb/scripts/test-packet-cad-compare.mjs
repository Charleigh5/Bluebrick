import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const { normalizeActiveDocumentContext } = await import("../src/activeDocumentContext.ts");
const { analyzePacketPages, projectPacketEvidenceV2 } = await import("../src/packet-review/packetReview.ts");
const { comparePacketEvidenceToActiveDocument } = await import("../src/cad-compare/phaseAComparison.ts");

async function hash(value, prefix = "value_sha256") {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
  const hex = [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, "0")).join("");
  return `${prefix}:${hex.slice(0, 16)}`;
}

const review = analyzePacketPages("ASY511185-80238229.pdf", [
  {
    pageNumber: 1,
    text: [
      "DRAWING NO ASY511185-80238229",
      "REV B",
      "DESCRIPTION: Optical  Value Sign Holder",
      "MATERIAL: A1008",
      "THICKNESS: 0.0747 IN",
      "SHEET 1 OF 1"
    ].join("\n")
  }
], "2026-07-14T16:00:00.000Z");
const packet = await projectPacketEvidenceV2(review);
const titleHash = packet.identifiers[0].candidateDocumentTitleHashes[1];
const propertyEvidence = [
  {
    evidenceId: "cad:document:part-number",
    canonicalField: "part_number",
    scope: "document",
    normalizedValueHash: await hash("ASY511185-80238229"),
    rawValueHash: await hash("ASY511185-80238229"),
    evaluatedValueHash: await hash("ASY511185-80238229"),
    wasResolved: true,
    linkedToParent: false,
    resultCode: 0,
    readStatus: "resolved",
    ruleId: "VIRA-CAD-PROPERTY-CACHED-001"
  },
  {
    evidenceId: "cad:document:revision",
    canonicalField: "revision",
    scope: "document",
    normalizedValueHash: await hash("B"),
    rawValueHash: await hash("B"),
    evaluatedValueHash: await hash("B"),
    wasResolved: true,
    linkedToParent: false,
    resultCode: 0,
    readStatus: "resolved",
    ruleId: "VIRA-CAD-PROPERTY-CACHED-001"
  },
  {
    evidenceId: "cad:configuration:description",
    canonicalField: "description",
    scope: "configuration",
    normalizedValueHash: await hash("OPTICAL VALUE SIGN HOLDER"),
    rawValueHash: await hash("OPTICAL VALUE SIGN HOLDER"),
    evaluatedValueHash: await hash("OPTICAL VALUE SIGN HOLDER"),
    wasResolved: true,
    linkedToParent: false,
    resultCode: 0,
    readStatus: "resolved",
    ruleId: "VIRA-CAD-PROPERTY-CACHED-001"
  },
  {
    evidenceId: "cad:document:material",
    canonicalField: "material",
    scope: "document",
    normalizedValueHash: await hash("A36"),
    rawValueHash: await hash("A36"),
    evaluatedValueHash: await hash("A36"),
    wasResolved: true,
    linkedToParent: false,
    resultCode: 0,
    readStatus: "resolved",
    ruleId: "VIRA-CAD-PROPERTY-CACHED-001"
  },
  {
    evidenceId: "cad:document:thickness",
    canonicalField: "thickness",
    scope: "document",
    normalizedValueHash: await hash("INCHES:0.0750"),
    rawValueHash: await hash("0.075 IN"),
    evaluatedValueHash: await hash("0.075 IN"),
    wasResolved: true,
    linkedToParent: false,
    resultCode: 0,
    readStatus: "resolved",
    ruleId: "VIRA-CAD-THICKNESS-INCH-001"
  },
  {
    evidenceId: "cad:configuration:active_configuration",
    canonicalField: "configuration",
    scope: "configuration",
    normalizedValueHash: await hash("DEFAULT"),
    rawValueHash: await hash("Default"),
    evaluatedValueHash: await hash("Default"),
    wasResolved: true,
    linkedToParent: false,
    resultCode: 0,
    readStatus: "resolved",
    ruleId: "VIRA-CAD-ACTIVE-CONFIGURATION-001"
  }
];
const context = normalizeActiveDocumentContext({
  status: "ok",
  message: "Redacted active SOLIDWORKS document context captured.",
  items: [{
    metadata: {
      document_type: "ASSEMBLY",
      document_title_hash: titleHash,
      document_path_hash: "path_sha256:fixture",
      active_configuration_hash: "config_sha256:fixture",
      runtime_version: "32.3.1.2",
      is_dirty: "false",
      is_read_only: "true",
      custom_property_count: String(propertyEvidence.length),
      property_evidence_json: JSON.stringify(propertyEvidence),
      mutation_actions: "0"
    }
  }]
});

assert.equal(context.propertyEvidence.length, 6);
assert.equal(context.propertyEvidence[0].canonicalField, "part_number");
assert.equal(context.pathHash, "path_sha256:fixture");
assert.equal(context.activeConfigurationHash, "config_sha256:fixture");
assert.equal(context.runtimeVersion, "32.3.1.2");

const comparison = await comparePacketEvidenceToActiveDocument(packet, context);
assert.equal(comparison.schemaVersion, "vira.packet-cad.phase-a.v1");
assert.equal(comparison.identity.authority, "confirmed");
assert.equal(comparison.identity.isAuthoritative, true);
assert.deepEqual(comparison.identity.matchSources, ["CAD_CONTROLLED_PROPERTY", "DOCUMENT_TITLE_HASH"]);
assert.equal(comparison.properties.find((item) => item.canonicalField === "revision")?.status, "normalized-match");
assert.equal(comparison.properties.find((item) => item.canonicalField === "description")?.status, "normalized-match");
assert.equal(comparison.properties.find((item) => item.canonicalField === "material")?.status, "conflict");
assert.equal(comparison.properties.find((item) => item.canonicalField === "material")?.isAuthoritative, true);
assert.equal(comparison.properties.find((item) => item.canonicalField === "thickness")?.status, "normalized-match");
assert.equal(comparison.properties.find((item) => item.canonicalField === "configuration")?.status, "missing-in-pdf");
assert.ok(comparison.properties.every((item) => item.packetEvidence.length > 0 || item.status === "missing-in-pdf"));
assert.ok(comparison.properties.every((item) => item.cadEvidence.length > 0 || item.status === "missing-in-cad"));
assert.equal(comparison.mutationActions, 0);
assert.equal(comparison.externalSystemsAccessed, false);

const splitIdentityReview = analyzePacketPages("split-identity-fixture.pdf", [{
  pageNumber: 9,
  text: [
    "DOC NO",
    "PART NO",
    "80238229",
    "ASY511185",
    "REV B",
    "SHEET 1 OF 1"
  ].join("\n")
}]);
const splitIdentityPacket = await projectPacketEvidenceV2(splitIdentityReview);
const splitIdentityComparison = await comparePacketEvidenceToActiveDocument(splitIdentityPacket, context);
assert.deepEqual(splitIdentityReview.partNumbers, ["ASY511185-80238229"]);
assert.equal(splitIdentityComparison.identity.authority, "confirmed");
assert.equal(splitIdentityComparison.identity.isAuthoritative, true);
assert.deepEqual(splitIdentityComparison.identity.matchSources, ["CAD_CONTROLLED_PROPERTY", "DOCUMENT_TITLE_HASH"]);

const missingPacketIdentity = await comparePacketEvidenceToActiveDocument(
  { ...packet, identifiers: [] },
  context
);
assert.equal(
  missingPacketIdentity.identity.authority,
  "unresolved",
  "resolved CAD identity alone must not turn absent packet identity into a conflict"
);
assert.equal(missingPacketIdentity.identity.isAuthoritative, false);
assert.deepEqual(missingPacketIdentity.identity.matchSources, []);
assert.ok(
  missingPacketIdentity.properties.every((item) => item.isAuthoritative === false),
  "property comparisons remain non-authoritative until packet identity is established"
);

const serializedContext = JSON.stringify(context);
assert.doesNotMatch(serializedContext, /ASY511185-80238229|OPTICAL VALUE|A1008|A36|0\.075 IN/);

const hostReaderSource = await readFile(new URL("../../Agent/SolidWorksActiveDocumentContextReader.cs", import.meta.url), "utf8");
const toolServiceSource = await readFile(new URL("../../Agent/AssistantToolService.cs", import.meta.url), "utf8");
assert.match(hostReaderSource, /ActiveDocumentPropertyDigest/, "host has a redacted property digest contract");
assert.match(hostReaderSource, /Get6\([^\n]+true,/, "custom properties use cached-only Get6 reads");
assert.match(hostReaderSource, /VIRA-CAD-ACTIVE-CONFIGURATION-001/, "active configuration is emitted as redacted evidence");
assert.doesNotMatch(hostReaderSource, /Get6\([^\n]+false,/, "host never forces uncached property evaluation");
assert.doesNotMatch(hostReaderSource, /ForceRebuild/, "read-only property capture never rebuilds");
assert.match(toolServiceSource, /property_evidence_json/, "tool result transports only digest evidence JSON");

console.log(JSON.stringify({
  ok: true,
  checked: [
    "phase-a packet projection",
    "redacted CAD property digest parsing",
    "two-source identity authority",
    "derived title-block identity authority",
    "missing packet identity remains unresolved",
    "property comparison statuses",
    "thickness tolerance hash candidates",
    "cached-only host source boundary",
    "zero mutation and external access"
  ]
}, null, 2));
