import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import ts from "typescript";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const tempRoot = await mkdtemp(join(tmpdir(), "vira-lab-contract-"));
const surfaceRoutingPath = join(root, "src", "surfaceRouting.ts");
const runtimePath = join(root, "src", "vira-lab", "viraLabRuntime.ts");
const packetWorkflowPath = join(root, "src", "vira-lab", "packetWorkflow.ts");
const receiptPath = join(root, "src", "vira-lab", "viraLabReceipt.ts");
const appPath = join(root, "src", "App.tsx");
const labAppPath = join(root, "src", "vira-lab", "ViraLabApp.tsx");
const packetPanelPath = join(root, "src", "packet-review", "PacketReviewPanel.tsx");

const compilerOptions = {
  target: ts.ScriptTarget.ES2022,
  module: ts.ModuleKind.ES2022,
  moduleResolution: ts.ModuleResolutionKind.Bundler,
  jsx: ts.JsxEmit.ReactJSX,
  strict: true
};

await transpileToMjs(surfaceRoutingPath, join(tempRoot, "surfaceRouting.mjs"));
await transpileToMjs(runtimePath, join(tempRoot, "viraLabRuntime.mjs"));
await transpileToMjs(packetWorkflowPath, join(tempRoot, "packetWorkflow.mjs"));
await transpileToMjs(receiptPath, join(tempRoot, "viraLabReceipt.mjs"));

const { resolveAssistantSurface } = await import(pathToFileURL(join(tempRoot, "surfaceRouting.mjs")).href);
const { createViraLabRuntimeFromSearch } = await import(pathToFileURL(join(tempRoot, "viraLabRuntime.mjs")).href);
const { nextPacketWorkflowState, sanitizePacketFileName } = await import(pathToFileURL(join(tempRoot, "packetWorkflow.mjs")).href);
const { buildViraDiagnosticsExport, buildViraLabReceipt } = await import(pathToFileURL(join(tempRoot, "viraLabReceipt.mjs")).href);

const tests = [
  ["surface routing", testSurfaceRouting],
  ["deterministic fixture context", testFixtureContext],
  ["unsupported adapters stay unavailable", testUnsupportedAdapters],
  ["packet workflow state machine", testPacketWorkflowStateMachine],
  ["packet filename redaction", testPacketFileNameRedaction],
  ["sanitized receipt and bounded diagnostics", testReceiptAndDiagnostics],
  ["fixture source has no egress or host bridge", testNoEgress],
  ["app route integration", testAppRouteIntegration],
  ["workbench state contract", testWorkbenchStateContract],
  ["packet lifecycle integration", testPacketLifecycleIntegration]
];

const rows = [];
for (const [name, test] of tests) {
  await test();
  rows.push({ name, ok: true });
}

console.log(JSON.stringify({ ok: true, tests: rows }, null, 2));

async function transpileToMjs(sourcePath, targetPath) {
  const source = await readFile(sourcePath, "utf8");
  const result = ts.transpileModule(source, {
    compilerOptions,
    fileName: sourcePath,
    reportDiagnostics: true
  });
  const diagnostics = result.diagnostics?.filter((diagnostic) => diagnostic.category === ts.DiagnosticCategory.Error) ?? [];
  assert.equal(diagnostics.length, 0, `TypeScript transpile failed for ${sourcePath}`);
  await mkdir(dirname(targetPath), { recursive: true });
  await writeFile(targetPath, result.outputText.replace(/from "(\.{1,2}\/[^"]+)"/g, 'from "$1.mjs"'), "utf8");
}

function testSurfaceRouting() {
  assert.equal(resolveAssistantSurface(""), "default");
  assert.equal(resolveAssistantSurface("?mode=execution-board"), "execution-board");
  assert.equal(resolveAssistantSurface("?mode=vira-lab"), "vira-lab");
  assert.equal(resolveAssistantSurface("?mode=unknown"), "default");
}

async function testFixtureContext() {
  const runtime = createViraLabRuntimeFromSearch("?mode=vira-lab");
  assert.equal(runtime.kind, "fixture");
  assert.equal(runtime.sessionId, "VIRA-LAB-FIXTURE-001");
  const result = await runtime.getActiveDocumentContext();
  assert.equal(result.status, "ok");
  assert.equal(result.value.state, "ready");
  assert.equal(result.value.documentType, "Assembly");
  assert.equal(result.value.mutationActions, 0);
  assert.equal(result.value.assemblyTraversal.externalSystemsAccessed, false);
}

async function testUnsupportedAdapters() {
  for (const runtimeName of ["localhost-relay", "embedded-host", "unavailable"]) {
    const runtime = createViraLabRuntimeFromSearch(`?mode=vira-lab&runtime=${runtimeName}`);
    const result = await runtime.getActiveDocumentContext();
    assert.equal(result.status, "unavailable");
    assert.equal(result.value, undefined);
  }
}

async function testNoEgress() {
  const source = await readFile(runtimePath, "utf8");
  for (const token of ["fetch(", "XMLHttpRequest", "WebSocket", "EventSource", "sendBeacon", "chrome.webview", "http://", "https://"]) {
    assert.equal(source.includes(token), false, `${token} found in fixture runtime`);
  }
}

function testPacketWorkflowStateMachine() {
  assert.equal(nextPacketWorkflowState("idle", { type: "select" }), "packet-loading");
  assert.equal(nextPacketWorkflowState("packet-loading", { type: "packet-ready" }), "ready-to-compare");
  assert.equal(nextPacketWorkflowState("ready-to-compare", { type: "evaluate" }), "evaluating");
  assert.equal(nextPacketWorkflowState("evaluating", { type: "complete" }), "complete");
  assert.equal(nextPacketWorkflowState("evaluating", { type: "fail", retainsEvidence: true }), "partial");
  assert.equal(nextPacketWorkflowState("packet-loading", { type: "fail", retainsEvidence: false }), "failed");
  assert.equal(nextPacketWorkflowState("packet-loading", { type: "cancel" }), "cancelled");
  assert.equal(nextPacketWorkflowState("complete", { type: "select" }), "packet-loading");
}

function testPacketFileNameRedaction() {
  assert.equal(sanitizePacketFileName("C:\\customer\\secret\\packet.pdf"), "packet.pdf");
  assert.equal(sanitizePacketFileName("/mnt/customer/packet.pdf"), "packet.pdf");
  assert.equal(sanitizePacketFileName(""), "packet.pdf");
  assert.equal(sanitizePacketFileName(`${"a".repeat(200)}.pdf`).length <= 120, true);
}

async function testReceiptAndDiagnostics() {
  const runtime = createViraLabRuntimeFromSearch("?mode=vira-lab");
  const contextResult = await runtime.getActiveDocumentContext();
  assert.equal(contextResult.status, "ok");
  const diagnostics = [
    { id: "C:\\secret\\trace-id", level: "info", code: "FIXTURE_CONTEXT_READY", message: "C:\\secret\\drawing.sldasm" },
    { id: "token-sk-private", level: "warning", code: "PACKET_PARTIAL", message: "raw packet text" }
  ];
  const receipt = buildViraLabReceipt({
    sessionId: runtime.sessionId,
    runtimeKind: runtime.kind,
    workbenchState: "ready",
    packetState: "partial",
    packetSummary: {
      state: "partial",
      fileName: "packet.pdf",
      message: "raw packet text",
      retainsEvidence: true,
      pageCount: 1,
      findingCount: 2,
      phaseAStatus: "confirmed",
      phaseBComparisonCount: 3
    },
    context: contextResult.value,
    diagnostics,
    durationMs: 1234.6
  }, "2026-07-23T00:00:00.000Z");
  const receiptJson = JSON.stringify(receipt);
  assert.equal(receipt.schemaVersion, "vira.lab.receipt.v1");
  assert.equal(receipt.session.durationMs, 1235);
  assert.equal(receipt.packet.phaseAAuthority, "confirmed");
  assert.deepEqual(receipt.diagnostics.warningCodes, ["PACKET_PARTIAL"]);
  for (const forbidden of ["C:\\secret", "raw packet text", "token-sk-private"]) {
    assert.equal(receiptJson.includes(forbidden), false, `Receipt leaked ${forbidden}`);
  }

  const exportValue = buildViraDiagnosticsExport(runtime.sessionId, diagnostics, "2026-07-23T00:00:00.000Z");
  const diagnosticsJson = JSON.stringify(exportValue);
  assert.equal(exportValue.entries.length, 2);
  assert.equal(exportValue.entries[1].summary, "Packet review completed with explicit incomplete evidence.");
  assert.equal(exportValue.redaction.includesDiagnosticIds, false);
  for (const forbidden of ["C:\\secret", "raw packet text", "token-sk-private"]) {
    assert.equal(diagnosticsJson.includes(forbidden), false, `Diagnostics leaked ${forbidden}`);
  }
}

async function testAppRouteIntegration() {
  const source = await readFile(appPath, "utf8");
  assert.ok(source.includes("resolveAssistantSurface"));
  assert.ok(source.includes('surface === "execution-board"'));
  assert.ok(source.includes('surface === "vira-lab"'));
  assert.ok(source.includes("return <ViraLabApp"));
}

async function testWorkbenchStateContract() {
  const source = await readFile(labAppPath, "utf8");
  for (const state of ["loading", "ready", "unavailable", "error"]) {
    assert.ok(source.includes(`"${state}"`), `Missing ${state} workbench state`);
  }
  assert.ok(source.includes("ActiveDocumentContextCard"));
  assert.ok(source.includes("Read-only"));
}

async function testPacketLifecycleIntegration() {
  const labSource = await readFile(labAppPath, "utf8");
  const panelSource = await readFile(packetPanelPath, "utf8");
  assert.ok(labSource.includes("PacketReviewPanel"));
  assert.ok(labSource.includes("onLifecycleEvent"));
  assert.ok(labSource.includes("Export VIRA receipt"));
  assert.ok(labSource.includes("Export diagnostics"));
  for (const state of ["idle", "packet-loading", "ready-to-compare", "evaluating", "partial", "complete", "failed", "cancelled"]) {
    assert.ok(labSource.includes(`"${state}"`) || (await readFile(packetWorkflowPath, "utf8")).includes(`"${state}"`), `Missing packet state ${state}`);
  }
  assert.ok(panelSource.includes("Promise.allSettled"));
  assert.ok(panelSource.includes("loadGenerationRef"));
  assert.ok(panelSource.includes("retainsEvidence"));
}
