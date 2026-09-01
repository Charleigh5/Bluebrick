import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { readFile } from "node:fs/promises";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { dirname, extname, join, normalize, relative, resolve } from "node:path";
import { spawn } from "node:child_process";
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
  return join(tmpdir(), "bluebrick-execution-board-relay", stamp);
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
const relayPort = await findFreePort();
const relayOrigin = `http://127.0.0.1:${relayPort}`;
const relayServer = await startRelayServer(relayOrigin, staticServer.origin);
const browser = await chromium.launch({ headless: true });
const screenshots = [];
const events = [];

try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 960 }, deviceScaleFactor: 1 });
  const health = attachHealthChecks(page, new Set([staticServer.origin, relayOrigin]));
  await installLocalRelayOnlyGuards(page, relayOrigin);
  await page.goto(`${staticServer.origin}/?mode=execution-board&relay=local&relayUrl=${encodeURIComponent(relayOrigin)}`);
  await page.getByRole("heading", { name: "Engineering Query Sandbox" }).waitFor({ timeout: 10000 });
  assert.equal(await page.locator(".execution-board-shell").count(), 1);
  assert.equal(await page.getByText("localhost-relay", { exact: true }).count(), 1);

  await routeQuery(page, "Need PDM availability for bb src 1001 before lunch");
  const exactReceipt = await readReceipt(page);
  assert.equal(exactReceipt.relayStatus, "LOCAL_FIXTURE_RESULT");
  assert.equal(exactReceipt.routeMode, "exact-id");
  assert.deepEqual(exactReceipt.matchedIds, ["BB-SRC-1001"]);
  assert.ok(exactReceipt.resultIds.includes("BB-SRC-1001"));
  assert.equal(exactReceipt.persistedReceipt, true);
  assert.equal(exactReceipt.transport.kind, "localhost-relay");
  assert.equal(exactReceipt.noExternalAccessAssertion, true);
  events.push("relay-exact-id-local-fixture-result");
  screenshots.push(await screenshot(page, "desktop-relay-exact-id.png"));

  await routeQuery(page, "BB-SRC-9999");
  const unknownReceipt = await readReceipt(page);
  assert.equal(unknownReceipt.relayStatus, "UNKNOWN_ID");
  assert.equal(unknownReceipt.routeMode, "exact-id");
  assert.deepEqual(unknownReceipt.resultIds, []);
  events.push("relay-unknown-id");

  await routeQuery(page, "Find PDM part 12345 availability");
  const pdmReceipt = await readReceipt(page);
  assert.equal(pdmReceipt.relayStatus, "NOT_CONNECTED");
  assert.ok(pdmReceipt.capabilityStates.some((item) => item.id === "BB-CAP-4003" && item.state === "NOT_CONNECTED"));
  assert.equal(await page.getByText("Preview PDM search", { exact: true }).count(), 1);
  events.push("relay-pdm-not-connected");
  screenshots.push(await screenshot(page, "desktop-relay-pdm-not-connected.png"));

  await routeQuery(page, "Route a SOLIDWORKS metadata request safely");
  const solidWorksReceipt = await readReceipt(page);
  assert.equal(solidWorksReceipt.relayStatus, "APPROVAL_REQUIRED");
  assert.ok(solidWorksReceipt.capabilityStates.some((item) => item.id === "BB-CAP-4002" && item.state === "APPROVAL_REQUIRED"));
  assert.equal(await page.getByText("Preview SOLIDWORKS metadata read", { exact: true }).count(), 1);
  events.push("relay-solidworks-approval-required");
  screenshots.push(await screenshot(page, "desktop-relay-solidworks-approval-required.png"));

  assert.deepEqual(health.errors, []);
  assert.deepEqual(health.blockedExternalRequests, []);
  assert.deepEqual(await readBrowserEgressAttempts(page), []);
  await page.close();
} finally {
  await browser.close();
  await relayServer.close();
  await staticServer.close();
}

const assets = screenshots.map((asset) => ({
  ...asset,
  relativePath: relative(repoRoot, asset.path).replace(/\\/g, "/"),
  sha256: sha256(asset.path)
}));

const summary = {
  ok: true,
  schemaVersion: "2026-07-07.bluebrick-execution-board-relay-smoke.v1",
  url: `${staticServer.origin}/?mode=execution-board&relay=local&relayUrl=${encodeURIComponent(relayOrigin)}`,
  relayOrigin,
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
    browserEgressApiCalls: false,
    allowedBrowserNetwork: ["AssistantWeb static localhost origin", "BlueBrick.Relay localhost origin"]
  }
};

writeFileSync(join(outputRoot, "execution-board-relay-smoke-summary.json"), JSON.stringify(summary, null, 2), "utf8");
writeFileSync(join(outputRoot, "execution-board-relay-smoke-summary.md"), renderMarkdown(summary), "utf8");
console.log(JSON.stringify(summary, null, 2));

async function routeQuery(page, query) {
  await page.getByLabel("Natural-language engineering request or exact ID").fill(query);
  await page.getByRole("button", { name: "Route request" }).click();
  await page.waitForFunction(
    (expectedQuery) => {
      try {
        const receipt = JSON.parse(document.querySelector(".eb-receipt")?.textContent ?? "{}");
        return receipt.query === expectedQuery && typeof receipt.relayStatus === "string";
      } catch {
        return false;
      }
    },
    query,
    { timeout: 10000 }
  );
}

async function readReceipt(page) {
  return JSON.parse(await page.locator(".eb-receipt").textContent());
}

async function screenshot(page, name) {
  const path = join(outputRoot, name);
  await page.screenshot({ path, fullPage: true });
  return { name, path };
}

function attachHealthChecks(page, allowedOrigins) {
  const errors = [];
  const blockedExternalRequests = [];

  page.on("console", (message) => {
    if (["error", "warning"].includes(message.type())) errors.push(`${message.type()}: ${message.text()}`);
  });
  page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
  page.route("**/*", async (route) => {
    const url = route.request().url();
    if ([...allowedOrigins].some((origin) => url.startsWith(origin)) || url.startsWith("data:") || url.startsWith("blob:")) {
      await route.continue();
      return;
    }
    blockedExternalRequests.push(url);
    await route.abort();
  });

  return { errors, blockedExternalRequests };
}

async function installLocalRelayOnlyGuards(page, relayOrigin) {
  await page.addInitScript((allowedRelayOrigin) => {
    window.__executionBoardEgressAttempts = [];
    const record = (kind, target) => {
      window.__executionBoardEgressAttempts.push({ kind, target: String(target ?? "") });
    };
    const originalFetch = window.fetch.bind(window);
    window.fetch = (input, init) => {
      const target = typeof input === "string" ? input : input?.url;
      if (String(target ?? "").startsWith(allowedRelayOrigin)) return originalFetch(input, init);
      record("fetch", target);
      throw new Error("Execution board Relay smoke blocked non-Relay browser fetch.");
    };
    const OriginalXMLHttpRequest = window.XMLHttpRequest;
    window.XMLHttpRequest = function GuardedXMLHttpRequest() {
      const xhr = new OriginalXMLHttpRequest();
      const originalOpen = xhr.open;
      xhr.open = function guardedOpen(method, url, ...rest) {
        if (String(url).startsWith(allowedRelayOrigin)) return originalOpen.call(xhr, method, url, ...rest);
        record("XMLHttpRequest", url);
        throw new Error("Execution board Relay smoke blocked non-Relay XHR.");
      };
      return xhr;
    };
    window.WebSocket = function GuardedWebSocket(url) {
      record("WebSocket", url);
      throw new Error("Execution board Relay smoke blocked WebSocket.");
    };
    window.EventSource = function GuardedEventSource(url) {
      record("EventSource", url);
      throw new Error("Execution board Relay smoke blocked EventSource.");
    };
    navigator.sendBeacon = (url) => {
      record("sendBeacon", url);
      return false;
    };
  }, relayOrigin);
}

async function readBrowserEgressAttempts(page) {
  return page.evaluate(() => window.__executionBoardEgressAttempts ?? []);
}

async function startRelayServer(origin, staticOrigin) {
  const dotnet = process.env.DOTNET_EXE || "C:\\Users\\cweir\\.dotnet\\dotnet.exe";
  if (!existsSync(dotnet)) {
    fail("Local dotnet executable was not found.", { dotnet });
  }

  const dbPath = join(outputRoot, "relay-proof.db");
  const child = spawn(dotnet, ["run", "--project", join(repoRoot, "BlueBrick.Relay", "BlueBrick.Relay.csproj"), "--no-launch-profile"], {
    cwd: repoRoot,
    env: {
      ...process.env,
      ASPNETCORE_URLS: origin,
      ExecutionBoard__Enabled: "true",
      ExecutionBoard__AllowedOrigins__0: staticOrigin,
      Relay__SqlitePath: dbPath,
      OAuth__RequireHttpsMetadata: "false",
      DOTNET_SKIP_FIRST_TIME_EXPERIENCE: "1"
    },
    stdio: ["ignore", "pipe", "pipe"]
  });

  const logs = [];
  child.stdout.on("data", (chunk) => logs.push(String(chunk)));
  child.stderr.on("data", (chunk) => logs.push(String(chunk)));
  child.once("exit", (code) => {
    if (code !== null && code !== 0) logs.push(`Relay exited with code ${code}`);
  });

  try {
    await waitForHealth(origin, logs);
  } catch (error) {
    child.kill();
    fail("Relay did not start for local execution-board proof.", {
      detail: error instanceof Error ? error.message : String(error),
      logs: logs.join("").slice(-4000)
    });
  }

  return {
    close: () =>
      new Promise((resolveClose) => {
        if (child.exitCode !== null) {
          resolveClose();
          return;
        }
        child.once("exit", resolveClose);
        child.kill();
        setTimeout(resolveClose, 3000).unref();
      })
  };
}

async function waitForHealth(origin, logs) {
  const deadline = Date.now() + 30000;
  let lastError = "";
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${origin}/health`);
      if (response.ok) return;
      lastError = `HTTP ${response.status}`;
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error);
    }
    await new Promise((resolveWait) => setTimeout(resolveWait, 500));
  }

  throw new Error(`${lastError}\n${logs.join("").slice(-1000)}`);
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

async function findFreePort() {
  const server = createServer();
  await new Promise((resolveListen, rejectListen) => {
    server.once("error", rejectListen);
    server.listen(0, "127.0.0.1", resolveListen);
  });
  const address = server.address();
  const port = typeof address === "object" && address ? address.port : 0;
  await new Promise((resolveClose) => server.close(resolveClose));
  return port;
}

function sha256(path) {
  return createHash("sha256").update(readFileSync(path)).digest("hex");
}

function renderMarkdown(summary) {
  return [
    "# BlueBrick Execution Board Local Relay Smoke",
    "",
    `- Passed: ${summary.ok}`,
    `- URL: \`${summary.url}\``,
    `- Relay: \`${summary.relayOrigin}\``,
    `- Output: \`${relative(repoRoot, summary.outputRoot).replace(/\\/g, "/")}\``,
    `- Events: ${summary.events.join(", ")}`,
    "",
    "| Asset | SHA-256 |",
    "| --- | --- |",
    ...summary.assets.map((asset) => `| \`${asset.relativePath}\` | \`${asset.sha256}\` |`)
  ].join("\n") + "\n";
}
