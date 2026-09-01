/**
 * Task 3 browser behavior check for the rendered bridge footer.
 *
 * The old implementation rendered bridgeRef.current during the first render,
 * so a WebView transport stayed visibly offline after the ref was installed.
 * This check serves the source through Vite and exercises install plus a
 * postMessage failure without writing AssistantWeb/dist.
 */
import { createServer } from "node:net";
import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const vitePath = join(root, "node_modules", "vite", "bin", "vite.js");

function fail(message, extra = {}) {
  console.error(JSON.stringify({ ok: false, error: message, ...extra }, null, 2));
  process.exit(1);
}

if (!existsSync(vitePath)) {
  fail("Vite is not available in the existing AssistantWeb dependencies.", { vitePath });
}

let chromium;
try {
  ({ chromium } = await import("playwright"));
} catch (error) {
  fail("Playwright is not available from the existing machine-level tooling.", {
    detail: error instanceof Error ? error.message : String(error)
  });
}

const port = await findAvailablePort();
const vite = spawn(
  process.execPath,
  [vitePath, "--host", "127.0.0.1", "--port", String(port)],
  { cwd: root, stdio: ["ignore", "pipe", "pipe"] }
);

try {
  await waitForHttp(`http://127.0.0.1:${port}/`);
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 480, height: 800 }, deviceScaleFactor: 1 });
    await page.addInitScript(() => {
      window.chrome = {
        webview: {
          postMessage() {}
        }
      };
    });

    await page.goto(`http://127.0.0.1:${port}/`, { waitUntil: "networkidle" });
    await page.locator(".safety-bridge").waitFor({ timeout: 10000 });
    const installed = await page.locator(".safety-bridge").innerText();
    if (installed !== "Bridge connected") {
      fail("Bridge install did not update the rendered footer.", { installed });
    }

    await page.evaluate(() => {
      window.chrome.webview.postMessage = () => {
        throw new Error("Task 3 verifier transport failure");
      };
    });
    await page.getByLabel("Message BlueBrick Assistant").fill("trigger bridge failure");
    await page.getByRole("button", { name: "Send message" }).click();
    await page.waitForFunction(
      () => document.querySelector(".safety-bridge")?.textContent === "Bridge error",
      undefined,
      { timeout: 10000 }
    );

    const failureTranscript = await page.locator(".msg").evaluateAll((nodes) =>
      nodes.map((node) => ({
        role: node.querySelector(".role")?.textContent?.trim() ?? "",
        text: node.querySelector(".text")?.textContent?.trim() ?? "",
        streaming: node.querySelector(".streaming-cursor") !== null,
      })),
    );
    const assistantFailures = failureTranscript.filter(
      (record) =>
        record.role === "assistant" &&
        record.text === "Bridge transport failed. Please try again." &&
        !record.streaming,
    );
    if (assistantFailures.length !== 1) {
      fail("Bridge failure must finalize exactly one pending assistant record.", {
        failureTranscript,
      });
    }

    console.log(JSON.stringify({
      ok: true,
      installState: installed,
      failureState: await page.locator(".safety-bridge").innerText(),
      assistantFailureCount: assistantFailures.length,
      writesDeploymentDist: false,
      launchesSolidWorks: false,
      callsExternalSystems: false
    }, null, 2));
  } finally {
    await browser.close();
  }
} finally {
  vite.kill();
}

async function findAvailablePort() {
  const server = createServer();
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  const address = server.address();
  const selectedPort = typeof address === "object" && address ? address.port : 0;
  await new Promise((resolve) => server.close(resolve));
  return selectedPort;
}

async function waitForHttp(url) {
  const deadline = Date.now() + 15000;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch {
      // Vite is still starting.
    }
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(`Timed out waiting for ${url}`);
}
