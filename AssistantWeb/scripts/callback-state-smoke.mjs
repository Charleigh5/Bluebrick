/**
 * BB-REACT-BRIDGE-STATE-APPLICATION-003 — callback/state smoke.
 *
 * Loads the production dist bundle in a normal browser, invokes the REAL
 * window.bb* host->browser callbacks with the EXACT payload shapes the C#
 * host sends (AssistantPanel.cs), and asserts the rendered DOM reflects the
 * authoritative state.
 *
 * RED expectation (pre-fix): state does NOT reach the DOM.
 */
import { existsSync, mkdirSync } from "node:fs";
import { createServer } from "node:http";
import { dirname, join } from "node:path";
import { extname, normalize, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const distRoot = join(root, "dist");
const indexPath = join(distRoot, "index.html");

function fail(message, extra = {}) {
  console.error(JSON.stringify({ ok: false, error: message, ...extra }, null, 2));
  process.exit(1);
}

if (!existsSync(indexPath)) {
  fail("AssistantWeb dist shell is missing. Run a build first.", { indexPath });
}

const outputRoot = join(
  process.env.TEMP ?? root,
  "bluebrick-assistant-verification",
  `callback-state-smoke-${new Date().toISOString().replace(/[:.]/g, "-")}`,
);
mkdirSync(outputRoot, { recursive: true });

const mimeTypes = {
  ".html": "text/html",
  ".js": "text/javascript",
  ".css": "text/css",
  ".json": "application/json",
  ".png": "image/png",
  ".svg": "image/svg+xml",
};

function startStaticServer() {
  return new Promise((resolvePromise) => {
    const server = createServer((req, res) => {
      const url = new URL(req.url ?? "/", "http://localhost");
      let filePath = normalize(join(distRoot, url.pathname));
      if (!filePath.startsWith(distRoot)) {
        res.writeHead(403);
        res.end();
        return;
      }
      if (!existsSync(filePath) || filePath === distRoot) {
        filePath = indexPath;
      }
      const ext = extname(filePath).toLowerCase();
      res.writeHead(200, { "Content-Type": mimeTypes[ext] ?? "application/octet-stream" });
      import("node:fs").then((fs) => fs.createReadStream(filePath).pipe(res));
    });
    server.listen(0, "127.0.0.1", () => {
      const port = server.address().port;
      resolvePromise({ server, origin: `http://127.0.0.1:${port}` });
    });
  });
}

// Exact payload shapes the C# host sends (AssistantPanel.cs RefreshStatusAsync /
// LoadModelsAsync / LoadToolsAsync / LoadScopesAsync / LoadToolAuditAsync /
// LoadProductCatalogsAsync). Source types are authoritative.
const hostPushes = `
(() => {
  // bbSetModel — string (JsonConvert.SerializeObject(_activeModel))
  window.bbSetModel("NVIDIA Llama 3.1 70B");

  // bbSetStatus — uiState JObject (mode/model/scopeId/configured/activeModel/scopes/toolAvailability)
  window.bbSetStatus({
    mode: "real",
    model: "NVIDIA Llama 3.1 70B",
    scopeId: "local_vault",
    configured: true,
    bridge: "127.0.0.1:17178",
    relayConnected: false,
    activeModel: { Id: "nvidia-llama-3-1-70b", Name: "NVIDIA Llama 3.1 70B", Provider: "NVIDIA" },
    scopes: [],
    toolAvailability: { TotalTools: 8, EnabledTools: 4 },
    status: "Real mode ready"
  });

  // bbSetModels — JArray with PascalCase entries exactly like /assistant/models
  window.bbSetModels([
    { Id: "nvidia-llama-3-1-70b", Name: "NVIDIA Llama 3.1 70B", Provider: "NVIDIA", IsDefault: true },
    { Id: "second-model", Name: "Second Model", Provider: "NVIDIA", IsDefault: false }
  ]);

  // bbSetScopes — JArray with PascalCase entries exactly like /assistant/scopes
  window.bbSetScopes([
    { Id: "local_vault", Label: "Local Vault", Enabled: true },
    { Id: "pdm", Label: "PDM", Enabled: false, UnavailableReason: "disabled" }
  ]);

  // bbSetScope — string
  window.bbSetScope("local_vault");

  // bbSetTools — JArray PascalCase like /assistant/tools
  window.bbSetTools([
    { Name: "search_local_vault", DisplayName: "Local Vault Search", Enabled: true },
    { Name: "capture_screenshot", DisplayName: "Capture", Enabled: true }
  ]);

  // bbSetToolReceipts — JArray
  window.bbSetToolReceipts([ { ToolName: "search_local_vault", ResultStatus: "ok" } ]);

  // bbSetProductCatalogs — { integrations, documents }
  window.bbSetProductCatalogs({ integrations: [ { Id: "epicor" } ], documents: [ { Id: "doc-1" } ] });
})()
`;

const staticServer = await startStaticServer();
let chromium;
try {
  ({ chromium } = await import("playwright"));
} catch (error) {
  fail("Playwright is not available.", { detail: error instanceof Error ? error.message : String(error) });
}

const browser = await chromium.launch({ headless: true });
const failures = [];
try {
  const page = await browser.newPage({ viewport: { width: 480, height: 900 }, deviceScaleFactor: 1 });
  await page.addInitScript(() => {
    window.__bbBridgeMessages = [];
    window.chrome = { webview: { postMessage(m) { window.__bbBridgeMessages.push(m); } } };
  });
  await page.goto(`${staticServer.origin}/index.html`);
  await page.waitForSelector(".shell", { timeout: 10000 });

  const before = await page.evaluate(() => ({
    headerSub: document.querySelector(".brand-sub")?.textContent ?? null,
    modelCount: document.querySelector(".cap-count")?.textContent ?? null,
    modelOptions: document.querySelectorAll("#assistant-model option").length,
    scopeButtons: document.querySelectorAll(".scope").length,
  }));

  await page.evaluate(hostPushes);
  await page.waitForTimeout(700);

  const after = await page.evaluate(() => ({
    headerSub: document.querySelector(".brand-sub")?.textContent ?? null,
    modelCount: document.querySelector(".cap-count")?.textContent ?? null,
    modelValue: document.querySelector("#assistant-model")?.value ?? null,
    modelOptions: document.querySelectorAll("#assistant-model option").length,
    scopeButtons: document.querySelectorAll(".scope").length,
    selectedScope: document.querySelector(".scope.selected")?.textContent ?? null,
    transcript: typeof window.bbGetTranscript === "function" ? window.bbGetTranscript() : null,
  }));

  console.log(JSON.stringify({ before, after }, null, 2));

  const expect = (name, condition, detail) => {
    if (!condition) failures.push(`${name}: ${detail}`);
  };

  expect("connection", !(after.headerSub ?? "").includes("CONNECTING"), `headerSub=${after.headerSub}`);
  expect("mode", (after.headerSub ?? "").includes("real"), `headerSub=${after.headerSub}`);
  expect("model_count", (after.modelCount ?? "").startsWith("2"), `modelCount=${after.modelCount}`);
  expect("model_options", after.modelOptions === 2, `options=${after.modelOptions}`);
  expect("model_selected", after.modelValue === "nvidia-llama-3-1-70b", `modelValue=${after.modelValue}`);
  expect("scopes_rendered", after.scopeButtons === 2, `scopeButtons=${after.scopeButtons}`);
  expect("scope_selected", (after.selectedScope ?? "").includes("Local Vault"), `selected=${after.selectedScope}`);
} finally {
  await browser.close();
  staticServer.server.close();
}

if (failures.length > 0) {
  console.error(JSON.stringify({ ok: false, failures }, null, 2));
  process.exit(1);
}
console.log(JSON.stringify({ ok: true, outputRoot }, null, 2));
