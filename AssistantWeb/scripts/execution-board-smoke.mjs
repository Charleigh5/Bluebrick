import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { readFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, extname, join, normalize, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(root);
const distRoot = join(root, "dist");
const indexPath = join(distRoot, "index.html");
const outputRoot = resolveOutputRoot();

function resolveOutputRoot() {
  const outArg = process.argv.find((arg) => arg.startsWith("--out="));
  if (outArg) return resolve(repoRoot, outArg.slice("--out=".length));

  const stamp = new Date().toISOString().replace(/[:.]/g, "-");
  return join(process.env.TEMP ?? root, "bluebrick-execution-board", stamp);
}

function fail(message, extra = {}) {
  console.error(JSON.stringify({ ok: false, error: message, ...extra }, null, 2));
  process.exit(1);
}

if (!existsSync(indexPath)) {
  fail("AssistantWeb dist shell is missing. Run npm --prefix .\\AssistantWeb run build before this smoke.", { indexPath });
}

let chromium;
try {
  ({ chromium } = await import("playwright"));
} catch (error) {
  fail("Playwright is not available from AssistantWeb dependencies.", {
    detail: error instanceof Error ? error.message : String(error)
  });
}

mkdirSync(outputRoot, { recursive: true });

const staticServer = await startStaticServer();
const browser = await chromium.launch({ headless: true });
const screenshots = [];
const events = [];

try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 960 }, deviceScaleFactor: 1 });
  const health = attachHealthChecks(page, staticServer.origin);
  await installNoEgressGuards(page);
  await page.goto(`${staticServer.origin}/?mode=execution-board`);
  await page.getByRole("heading", { name: "Engineering Query Sandbox" }).waitFor({ timeout: 10000 });
  assert.equal(await page.title(), "BlueBrick Assistant");
  assert.equal(await page.locator(".execution-board-shell").count(), 1);
  assert.equal(await page.locator(".vite-error-overlay").count(), 0);

  await routeQuery(page, "Need PDM availability for bb src 1001 before lunch");
  const exactReceipt = await readReceipt(page);
  assert.equal(exactReceipt.routeMode, "exact-id");
  assert.deepEqual(exactReceipt.matchedIds, ["BB-SRC-1001"]);
  assert.ok(exactReceipt.resultIds.includes("BB-SRC-1001"));
  assert.equal(exactReceipt.noExternalAccessAssertion, true);
  assert.equal(await page.getByText("Preview PDM search", { exact: true }).count(), 0);
  events.push("exact-id-precedence");
  screenshots.push(await screenshot(page, "desktop-exact-id.png"));

  await routeQuery(page, "Find PDM part 12345 availability");
  const pdmReceipt = await readReceipt(page);
  assert.equal(pdmReceipt.routeMode, "fixture-search");
  assert.ok(pdmReceipt.capabilityStates.some((item) => item.id === "BB-CAP-4003" && item.state === "NOT_CONNECTED"));
  assert.equal(await page.getByText("Preview PDM search", { exact: true }).count(), 1);
  assertNoFabricatedFacts(await page.textContent("body"));
  events.push("pdm-not-connected-preview-only");
  screenshots.push(await screenshot(page, "desktop-pdm-not-connected.png"));

  await routeQuery(page, "Show Epicor status for an engineering request");
  const epicorReceipt = await readReceipt(page);
  assert.ok(epicorReceipt.capabilityStates.some((item) => item.id === "BB-CAP-4004" && item.state === "NOT_CONNECTED"));
  assert.equal(await page.getByText("Preview Epicor part lookup", { exact: true }).count(), 1);
  events.push("epicor-not-connected-preview-only");

  await routeQuery(page, "Route a SOLIDWORKS metadata request safely");
  const solidWorksReceipt = await readReceipt(page);
  assert.ok(solidWorksReceipt.capabilityStates.some((item) => item.id === "BB-CAP-4002" && item.state === "APPROVAL_REQUIRED"));
  assert.equal(await page.getByText("Preview SOLIDWORKS metadata read", { exact: true }).count(), 1);
  events.push("solidworks-approval-required");

  const mobile = await browser.newPage({ viewport: { width: 390, height: 860 }, deviceScaleFactor: 1 });
  const mobileHealth = attachHealthChecks(mobile, staticServer.origin);
  await installNoEgressGuards(mobile);
  await mobile.goto(`${staticServer.origin}/?mode=execution-board`);
  await mobile.getByRole("heading", { name: "Engineering Query Sandbox" }).waitFor({ timeout: 10000 });
  await routeQuery(mobile, "bb_src_1001");
  const mobileReceipt = await readReceipt(mobile);
  assert.equal(mobileReceipt.routeMode, "exact-id");
  assert.deepEqual(mobileReceipt.matchedIds, ["BB-SRC-1001"]);
  screenshots.push(await screenshot(mobile, "mobile-normalized-id.png"));
  assert.deepEqual(mobileHealth.errors, []);
  assert.deepEqual(mobileHealth.blockedExternalRequests, []);
  assert.deepEqual(await readBrowserEgressAttempts(mobile), []);
  await mobile.close();

  await page.goto(`${staticServer.origin}/`);
  await page.waitForSelector(".shell", { timeout: 10000 });
  assert.equal(await page.locator(".execution-board-shell").count(), 0);
  assert.ok((await page.textContent("body"))?.includes("BlueBrick Assistant"));
  screenshots.push(await screenshot(page, "default-mode-regression.png"));

  assert.deepEqual(health.errors, []);
  assert.deepEqual(health.blockedExternalRequests, []);
  assert.deepEqual(await readBrowserEgressAttempts(page), []);
  await page.close();
} finally {
  await browser.close();
  await staticServer.close();
}

const assets = screenshots.map((asset) => ({
  ...asset,
  relativePath: relative(repoRoot, asset.path).replace(/\\/g, "/"),
  sha256: sha256(asset.path)
}));

const summary = {
  ok: true,
  schemaVersion: "2026-07-07.bluebrick-execution-board-smoke.v1",
  url: `${staticServer.origin}/?mode=execution-board`,
  outputRoot,
  events,
  assets,
  safetyBoundary: {
    launchesSolidWorks: false,
    attachesCom: false,
    opensCadFiles: false,
    accessesPdm: false,
    accessesEpicor: false,
    accessesSalesforce: false,
    readsSecrets: false,
    externalRequests: false,
    browserEgressApiCalls: false
  }
};

writeFileSync(join(outputRoot, "execution-board-smoke-summary.json"), JSON.stringify(summary, null, 2), "utf8");
writeFileSync(join(outputRoot, "execution-board-smoke-summary.md"), renderMarkdown(summary), "utf8");
console.log(JSON.stringify(summary, null, 2));

async function routeQuery(page, query) {
  await page.getByLabel("Natural-language engineering request or exact ID").fill(query);
  await page.getByRole("button", { name: "Route request" }).click();
  await page.locator(".eb-receipt").waitFor({ timeout: 5000 });
}

async function readReceipt(page) {
  return JSON.parse(await page.locator(".eb-receipt").textContent());
}

function assertNoFabricatedFacts(text = "") {
  const lower = text.toLowerCase();
  for (const forbidden of ["inventory quantity", "unit price", "bom line", "geometry volume", "customer price"]) {
    assert.equal(lower.includes(forbidden), false, `fabricated fact visible: ${forbidden}`);
  }
}

async function screenshot(page, name) {
  const path = join(outputRoot, name);
  await page.screenshot({ path, fullPage: true });
  return { name, path };
}

function attachHealthChecks(page, localOrigin) {
  const errors = [];
  const blockedExternalRequests = [];

  page.on("console", (message) => {
    if (["error", "warning"].includes(message.type())) errors.push(`${message.type()}: ${message.text()}`);
  });
  page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
  page.route("**/*", async (route) => {
    const url = route.request().url();
    if (url.startsWith(localOrigin) || url.startsWith("data:") || url.startsWith("blob:")) {
      await route.continue();
      return;
    }
    blockedExternalRequests.push(url);
    await route.abort();
  });

  return { errors, blockedExternalRequests };
}

async function installNoEgressGuards(page) {
  await page.addInitScript(() => {
    window.__executionBoardEgressAttempts = [];
    const record = (kind, target) => {
      window.__executionBoardEgressAttempts.push({ kind, target: String(target ?? "") });
    };
    window.fetch = (input) => {
      record("fetch", typeof input === "string" ? input : input?.url);
      throw new Error("Execution board smoke blocked browser fetch egress.");
    };
    const OriginalXMLHttpRequest = window.XMLHttpRequest;
    window.XMLHttpRequest = function GuardedXMLHttpRequest() {
      const xhr = new OriginalXMLHttpRequest();
      const originalOpen = xhr.open;
      xhr.open = function guardedOpen(method, url, ...rest) {
        record("XMLHttpRequest", url);
        throw new Error("Execution board smoke blocked XHR egress.");
      };
      return xhr;
    };
    window.WebSocket = function GuardedWebSocket(url) {
      record("WebSocket", url);
      throw new Error("Execution board smoke blocked WebSocket egress.");
    };
    window.EventSource = function GuardedEventSource(url) {
      record("EventSource", url);
      throw new Error("Execution board smoke blocked EventSource egress.");
    };
    navigator.sendBeacon = (url) => {
      record("sendBeacon", url);
      return false;
    };
  });
}

async function readBrowserEgressAttempts(page) {
  return page.evaluate(() => window.__executionBoardEgressAttempts ?? []);
}

async function startStaticServer() {
  const distResolved = resolve(distRoot);
  const contentTypes = new Map([
    [".html", "text/html; charset=utf-8"],
    [".js", "text/javascript; charset=utf-8"],
    [".css", "text/css; charset=utf-8"],
    [".json", "application/json; charset=utf-8"],
    [".png", "image/png"],
    [".jpg", "image/jpeg"],
    [".jpeg", "image/jpeg"],
    [".svg", "image/svg+xml"]
  ]);

  const server = createServer(async (request, response) => {
    try {
      const requestPath = new URL(request.url ?? "/", "http://127.0.0.1").pathname;
      const relativePath = decodeURIComponent(requestPath === "/" ? "/index.html" : requestPath);
      const target = resolve(distResolved, `.${normalize(relativePath)}`);
      if (!target.startsWith(distResolved)) {
        response.writeHead(403);
        response.end("Forbidden");
        return;
      }

      const body = await readFile(target);
      response.writeHead(200, {
        "Cache-Control": "no-store",
        "Content-Type": contentTypes.get(extname(target).toLowerCase()) ?? "application/octet-stream"
      });
      response.end(body);
    } catch {
      response.writeHead(404);
      response.end("Not found");
    }
  });

  await new Promise((resolveListen, rejectListen) => {
    server.once("error", rejectListen);
    server.listen(0, "127.0.0.1", resolveListen);
  });

  const address = server.address();
  const port = typeof address === "object" && address ? address.port : 0;
  return {
    origin: `http://127.0.0.1:${port}`,
    close: () => new Promise((resolveClose) => server.close(resolveClose))
  };
}

function sha256(path) {
  return createHash("sha256").update(readFileSync(path)).digest("hex");
}

function renderMarkdown(summary) {
  return [
    "# BlueBrick Execution Board Smoke",
    "",
    `- Passed: ${summary.ok}`,
    `- URL: \`${summary.url}\``,
    `- Output: \`${relative(repoRoot, summary.outputRoot).replace(/\\/g, "/")}\``,
    `- Events: ${summary.events.join(", ")}`,
    "",
    "| Asset | SHA-256 |",
    "| --- | --- |",
    ...summary.assets.map((asset) => `| \`${asset.relativePath}\` | \`${asset.sha256}\` |`)
  ].join("\n") + "\n";
}
