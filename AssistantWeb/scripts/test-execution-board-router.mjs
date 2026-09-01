import assert from "node:assert/strict";
import { mkdir, mkdtemp, readdir, readFile, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, extname, join, relative } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import ts from "typescript";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const surfaceRoutingPath = join(root, "src", "surfaceRouting.ts");
const executionBoardRoot = join(root, "src", "execution-board");
const routingPath = join(root, "src", "execution-board", "routing.ts");
const unavailableAdapterPath = join(root, "src", "execution-board", "adapters", "UnavailableSystemAdapter.ts");
const relayContractPath = join(root, "src", "execution-board", "adapters", "RelayQueryAdapter.contract.ts");
const localRelayAdapterPath = join(root, "src", "execution-board", "adapters", "LocalRelayQueryAdapter.ts");
const hostContractPath = join(root, "src", "execution-board", "adapters", "InProcessHostBridgeAdapter.contract.ts");
const tempRoot = await mkdtemp(join(tmpdir(), "bluebrick-router-contract-"));

const compilerOptions = {
  target: ts.ScriptTarget.ES2022,
  module: ts.ModuleKind.ES2022,
  moduleResolution: ts.ModuleResolutionKind.Bundler,
  jsx: ts.JsxEmit.ReactJSX,
  strict: true
};

await transpileExecutionBoardSources();
await transpileToMjs(surfaceRoutingPath, join(tempRoot, "surfaceRouting.mjs"));

const { routeEngineeringQuery, serializeExecutionReceipt } = await import(pathToFileURL(join(tempRoot, "routing.mjs")).href);
const { resolveAssistantSurface } = await import(pathToFileURL(join(tempRoot, "surfaceRouting.mjs")).href);
const { UnavailableSystemAdapter } = await import(pathToFileURL(join(tempRoot, "adapters", "UnavailableSystemAdapter.mjs")).href);
const { relayQueryAdapterContract } = await import(pathToFileURL(join(tempRoot, "adapters", "RelayQueryAdapter.contract.mjs")).href);
const { isAllowedLocalRelayUrl } = await import(pathToFileURL(join(tempRoot, "adapters", "LocalRelayQueryAdapter.mjs")).href);
const { inProcessHostBridgeAdapterContract } = await import(pathToFileURL(join(tempRoot, "adapters", "InProcessHostBridgeAdapter.contract.mjs")).href);

const tests = [
  ["exact ID wins over fuzzy result", testExactIdWins],
  ["normalized identifier variants", testNormalizedVariants],
  ["unknown exact ID", testUnknownExactId],
  ["NOT_CONNECTED PDM", testPdmNotConnected],
  ["NOT_CONNECTED Epicor", testEpicorNotConnected],
  ["APPROVAL_REQUIRED SOLIDWORKS", testSolidWorksApprovalRequired],
  ["receipt serialization and redaction", testReceiptSerializationRedaction],
  ["default-mode regression", testDefaultModeRegression],
  ["local fixture port parity", testLocalFixturePortParity],
  ["unavailable system adapter", testUnavailableSystemAdapter],
  ["contract-only adapters", testContractOnlyAdapters],
  ["local Relay URL guard", testLocalRelayUrlGuard],
  ["execution-board source has no egress calls", testNoEgressTokens]
];

const rows = [];
for (const [name, test] of tests) {
  await test();
  rows.push({ name, ok: true });
}

console.log(JSON.stringify({ ok: true, tests: rows }, null, 2));

async function transpileExecutionBoardSources() {
  const sourcePaths = await listFiles(executionBoardRoot);
  for (const sourcePath of sourcePaths.filter((file) => extname(file) === ".ts")) {
    const relativePath = relative(executionBoardRoot, sourcePath);
    const targetPath = join(tempRoot, relativePath).replace(/\.ts$/, ".mjs");
    await mkdir(dirname(targetPath), { recursive: true });
    await transpileToMjs(sourcePath, targetPath);
  }
}

async function listFiles(rootPath) {
  const entries = await readdir(rootPath, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const entryPath = join(rootPath, entry.name);
    if (entry.isDirectory()) files.push(...await listFiles(entryPath));
    if (entry.isFile()) files.push(entryPath);
  }
  return files;
}

async function transpileToMjs(sourcePath, targetPath) {
  const source = await readFile(sourcePath, "utf8");
  const result = ts.transpileModule(source, {
    compilerOptions,
    fileName: sourcePath,
    reportDiagnostics: true
  });
  const diagnostics = result.diagnostics?.filter((diagnostic) => diagnostic.category === ts.DiagnosticCategory.Error) ?? [];
  assert.equal(diagnostics.length, 0, `TypeScript transpile failed for ${sourcePath}`);
  await writeFile(targetPath, rewriteImports(result.outputText), "utf8");
}

function rewriteImports(code) {
  return code.replace(/from "(\.{1,2}\/[^"]+)"/g, (_match, specifier) => {
    if (specifier.endsWith(".css")) return `from "${specifier}"`;
    if (specifier.endsWith(".mjs")) return `from "${specifier}"`;
    return `from "${specifier}.mjs"`;
  });
}

function states(route) {
  return route.capabilities.map((capability) => capability.state);
}

function capability(route, id) {
  return route.capabilities.find((item) => item.id === id);
}

function assertNoExternalAccess(receipt) {
  assert.equal(receipt.noExternalAccessAssertion, true);
  assert.equal(receipt.externalSystemsAccessed, false);
  assert.equal(receipt.cadAccessed, false);
  assert.equal(receipt.pdmAccessed, false);
  assert.equal(receipt.secretsAccessed, false);
  assert.equal(receipt.productionDataAccessed, false);
}

function assertNoFabricatedBusinessFacts(route) {
  const rendered = JSON.stringify(route).toLowerCase();
  for (const forbidden of ["inventory quantity", "unit price", "bom line", "geometry volume", "customer price"]) {
    assert.equal(rendered.includes(forbidden), false, `fabricated business fact leaked: ${forbidden}`);
  }
}

function testExactIdWins() {
  const route = routeEngineeringQuery("Need PDM availability for bb src 1001 before lunch");
  assert.equal(route.mode, "exact-id");
  assert.deepEqual(route.receipt.matchedIds, ["BB-SRC-1001"]);
  assert.ok(route.sources.some((source) => source.id === "BB-SRC-1001"));
  assert.equal(capability(route, "BB-CAP-4003"), undefined, "PDM fuzzy match should not override exact ID");
  assertNoExternalAccess(route.receipt);
}

function testNormalizedVariants() {
  for (const query of ["bb-src-1001", "bb_src_1001", "bb src 1001", "BBSRC1001"]) {
    const route = routeEngineeringQuery(query);
    assert.equal(route.mode, "exact-id", query);
    assert.deepEqual(route.receipt.matchedIds, ["BB-SRC-1001"], query);
    assert.ok(route.sources.some((source) => source.id === "BB-SRC-1001"), query);
  }
}

function testUnknownExactId() {
  const route = routeEngineeringQuery("BB-SRC-9999");
  assert.equal(route.mode, "exact-id");
  assert.deepEqual(route.receipt.matchedIds, ["BB-SRC-9999"]);
  assert.deepEqual(route.receipt.resultIds, []);
  assert.ok(route.gaps.some((gap) => gap.includes("No local fixture record exists")));
  assert.equal(route.receipt.redactedErrorDetails[0]?.code, "UNKNOWN_EXACT_ID");
  assertNoExternalAccess(route.receipt);
}

function testPdmNotConnected() {
  const route = routeEngineeringQuery("Find PDM part 12345 availability");
  assert.equal(route.mode, "fixture-search");
  assert.equal(capability(route, "BB-CAP-4003")?.state, "NOT_CONNECTED");
  assert.ok(route.actions.some((action) => action.id === "BB-ACT-5002" && action.executionState === "NON_EXECUTABLE_PREVIEW"));
  assertNoFabricatedBusinessFacts(route);
  assertNoExternalAccess(route.receipt);
}

function testEpicorNotConnected() {
  const route = routeEngineeringQuery("Show Epicor cost and inventory for part 12345");
  assert.equal(route.mode, "fixture-search");
  assert.equal(capability(route, "BB-CAP-4004")?.state, "NOT_CONNECTED");
  assert.ok(route.actions.some((action) => action.id === "BB-ACT-5003" && action.executionState === "NON_EXECUTABLE_PREVIEW"));
  assertNoFabricatedBusinessFacts(route);
  assertNoExternalAccess(route.receipt);
}

function testSolidWorksApprovalRequired() {
  const route = routeEngineeringQuery("Route a SOLIDWORKS metadata request safely");
  assert.equal(route.mode, "fixture-search");
  assert.equal(capability(route, "BB-CAP-4002")?.state, "APPROVAL_REQUIRED");
  assert.ok(states(route).includes("APPROVAL_REQUIRED"));
  assert.ok(route.actions.some((action) => action.id === "BB-ACT-5001" && action.executionState === "NON_EXECUTABLE_PREVIEW"));
  assertNoExternalAccess(route.receipt);
}

function testReceiptSerializationRedaction() {
  const route = routeEngineeringQuery("unknown adapter stack trace token=abc123");
  const serialized = serializeExecutionReceipt(route.receipt);
  const parsed = JSON.parse(serialized);
  assert.equal(parsed.query, route.request);
  assert.equal(typeof parsed.routingDecision, "string");
  assert.ok(Array.isArray(parsed.resultIds));
  assert.ok(Array.isArray(parsed.capabilityStates));
  assert.equal(parsed.noExternalAccessAssertion, true);
  assert.equal(parsed.redactedErrorDetails[0]?.detail, "[REDACTED]");
  assert.equal(serialized.includes("abc123"), true, "query is preserved as user input");
}

async function testDefaultModeRegression() {
  assert.equal(resolveAssistantSurface(""), "default");
  assert.equal(resolveAssistantSurface("?mode=execution-board"), "execution-board");
  assert.equal(resolveAssistantSurface("?mode=unknown"), "default");
}

function testLocalFixturePortParity() {
  const defaultRoute = routeEngineeringQuery("BB-SRC-1001");
  const observed = [];
  const route = routeEngineeringQuery("BB-SRC-1001", {
    kind: "local-fixture",
    route(query) {
      observed.push(query);
      return defaultRoute;
    }
  });

  assert.equal(route.mode, "exact-id");
  assert.equal(observed.length, 1);
  assert.equal(observed[0].request, "BB-SRC-1001");
  assert.equal(observed[0].noExternalEgress, true);
}

function testUnavailableSystemAdapter() {
  const adapter = new UnavailableSystemAdapter({
    system: "PDM",
    capabilityState: "NOT_CONNECTED",
    reason: "No approved PDM adapter is connected."
  });
  const route = routeEngineeringQuery("Find PDM part 12345", adapter);
  assert.equal(route.mode, "not-connected");
  assert.equal(route.capabilities[0]?.state, "NOT_CONNECTED");
  assert.equal(route.actions[0]?.executionState, "NON_EXECUTABLE_PREVIEW");
  assertNoExternalAccess(route.receipt);
}

function testContractOnlyAdapters() {
  assert.equal(relayQueryAdapterContract.status, "NOT_IMPLEMENTED");
  assert.equal(relayQueryAdapterContract.forbiddenInCurrentSlice, true);
  assert.equal(inProcessHostBridgeAdapterContract.status, "NOT_IMPLEMENTED");
  assert.equal(inProcessHostBridgeAdapterContract.forbiddenInCurrentSlice, true);
}

function testLocalRelayUrlGuard() {
  assert.equal(isAllowedLocalRelayUrl("http://127.0.0.1:5085"), true);
  assert.equal(isAllowedLocalRelayUrl("http://localhost:5085"), true);
  assert.equal(isAllowedLocalRelayUrl("https://127.0.0.1:5085"), false);
  assert.equal(isAllowedLocalRelayUrl("http://example.com:5085"), false);
  assert.equal(isAllowedLocalRelayUrl("not-a-url"), false);
}

async function testNoEgressTokens() {
  const files = await listFiles(executionBoardRoot);
  const sourceFiles = files.filter((file) => [".ts", ".tsx"].includes(extname(file)));
  const forbidden = [
    "fetch(",
    "XMLHttpRequest",
    "WebSocket",
    "EventSource",
    "sendBeacon",
    "chrome.webview.postMessage",
    "http://",
    "https://"
  ];

  for (const file of sourceFiles) {
    const isReviewedLocalRelayAdapter = file === localRelayAdapterPath;
    const source = await readFile(file, "utf8");
    for (const token of forbidden) {
      if (isReviewedLocalRelayAdapter && ["fetch(", "http://"].includes(token)) continue;
      assert.equal(source.includes(token), false, `${token} found in ${file}`);
    }

    if (isReviewedLocalRelayAdapter) {
      assert.ok(source.includes("requireLocalhostHttpOrigin"), "Local Relay adapter must keep localhost URL validation.");
      assert.ok(source.includes('parsed.protocol !== "http:"'), "Local Relay adapter must reject non-HTTP local proof URLs.");
      assert.ok(source.includes('"127.0.0.1"'), "Local Relay adapter must explicitly allow loopback only.");
      assert.ok(source.includes('"localhost"'), "Local Relay adapter must explicitly allow localhost only.");
    }
  }
}
