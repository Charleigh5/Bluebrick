import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { activeDocumentContextFromToolResult, normalizeActiveDocumentContext } from "../src/activeDocumentContext.ts";
import { comparePacketBomToAssembly } from "../src/cad-compare/phaseBComparison.ts";
import { analyzePacketPages, projectPacketEvidenceV2 } from "../src/packet-review/packetReview.ts";

async function hash(value) {
  const bytes = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
  return `value_sha256:${[...new Uint8Array(bytes)].map((item) => item.toString(16).padStart(2, "0")).join("").slice(0, 16)}`;
}

const packet = await projectPacketEvidenceV2(analyzePacketPages("assembly.pdf", [{
  pageNumber: 3,
  text: [
    "1 2 MPM511284-80241102 MOUNTING BRACKET",
    "2 1 PBO511290-80241108 PURCHASED BY OTHERS"
  ].join("\n")
}]));
const identifierHash = await hash("MPM511284-80241102");
const descriptionHash = await hash("MOUNTING BRACKET");
const components = await Promise.all([1, 2].map(async (index) => ({
  snapshotId: `component-${index}`,
  parentSnapshotId: "sub-1",
  nativeComponentId: index,
  depth: 1,
  nameHash: `name_sha256:${index}`,
  nativePathHash: `native_path_sha256:${index}`,
  identifierHash,
  referencedConfigurationHash: await hash("DEFAULT"),
  kind: "part",
  suppressionState: "fully-resolved",
  resolutionState: "resolved",
  childrenState: "none",
  isVirtual: false,
  isGraphicsOnly: false,
  isSpeedPak: false,
  propertyEvidence: [{
    evidenceId: `cad:component:description:${index}`,
    canonicalField: "description",
    scope: "component",
    rawValueHash: descriptionHash,
    evaluatedValueHash: descriptionHash,
    normalizedValueHash: descriptionHash,
    wasResolved: true,
    linkedToParent: false,
    resultCode: 0,
    readStatus: "resolved",
    ruleId: "VIRA-CAD-COMPONENT-PROPERTY-CACHED-001"
  }],
  limitations: []
})));
const context = activeDocumentContextFromToolResult({
  toolName: "read_active_document_context",
  status: "ok",
  message: "fixture",
  items: [{ metadata: {
    document_type: "ASSEMBLY",
    component_evidence_json: JSON.stringify(components),
    assembly_traversal_json: JSON.stringify({ maxDepth: 32, recordLimit: 5000, recordedCount: 2, unloadedCount: 0, cycleCount: 0, truncated: false, mutationActions: 0, externalSystemsAccessed: false }),
    mutation_actions: "0"
  }}]
});
assert.ok(context);
assert.equal(context.componentEvidence.length, 2);
assert.equal(context.assemblyTraversal.recordedCount, 2);
const report = await comparePacketBomToAssembly(packet, context);
assert.equal(report.schemaVersion, "vira.packet-cad.phase-b.v1");
assert.ok(report.comparisons.some((item) => item.category === "bom:membership" && item.status === "exact-match"));
assert.ok(report.comparisons.some((item) => item.category === "bom:quantity" && item.status === "exact-match"));
assert.ok(report.comparisons.some((item) => item.category === "bom:description" && item.status === "exact-match"));
assert.ok(report.comparisons.some((item) => item.category === "bom:responsibility" && item.status === "not-applicable"));
assert.equal(report.scorecard.precision, 1);
assert.equal(report.scorecard.recall, 1);
assert.equal(report.mutationActions, 0);
assert.equal(report.externalSystemsAccessed, false);

const malformed = normalizeActiveDocumentContext({ status: "ok", items: [{ metadata: { component_evidence_json: "{not-json" } }] });
assert.deepEqual(malformed.componentEvidence, []);
assert.match(malformed.assemblyPayloadStatus, /malformed/);
const oversized = normalizeActiveDocumentContext({ status: "ok", items: [{ metadata: { component_evidence_json: "x".repeat(524289) } }] });
assert.deepEqual(oversized.componentEvidence, []);
assert.match(oversized.assemblyPayloadStatus, /oversized/);

const panelSource = await readFile(new URL("../src/packet-review/PacketReviewPanel.tsx", import.meta.url), "utf8");
assert.match(panelSource, /Packet ↔ CAD Phase B/);
assert.match(panelSource, /BOM ↔ active assembly/);
assert.match(panelSource, /aria-label="Filter Phase B findings"/);
assert.match(panelSource, /Unresolved only/);
assert.match(panelSource, /Precision/);
assert.match(panelSource, /Component payload/);

console.log(JSON.stringify({ ok: true, checked: [
  "redacted component transport parsing",
  "membership and quantity",
  "component description",
  "responsibility semantics",
  "precision and recall",
  "malformed and oversized payload rejection",
  "zero mutation and external access"
] }, null, 2));
