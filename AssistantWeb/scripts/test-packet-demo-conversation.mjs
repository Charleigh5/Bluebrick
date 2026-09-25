import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdir, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { setTimeout as delay } from "node:timers/promises";

const port = 5187;
const origin = `http://127.0.0.1:${port}`;
const fixtureRoot = await mkdtemp(join(tmpdir(), "bluebrick-packet-demo-"));
const screenshotRoot = join(tmpdir(), "bluebrick-assistant-verification", "packet-demo-conversation");
const fixtureProcess = spawn(process.execPath, ["scripts/create-packet-demo-fixtures.mjs", fixtureRoot], {
  cwd: new URL("..", import.meta.url),
  stdio: "pipe",
});
let server;
let browser;

function waitForExit(child, label) {
  return new Promise((resolve, reject) => {
    let stderr = "";
    child.stderr?.on("data", (chunk) => { stderr += String(chunk); });
    child.once("error", reject);
    child.once("exit", (code) => code === 0 ? resolve() : reject(new Error(`${label} exited ${code}: ${stderr}`)));
  });
}

async function waitForServer() {
  for (let attempt = 0; attempt < 40; attempt += 1) {
    try {
      const response = await fetch(origin);
      if (response.ok) return;
    } catch { /* Vite is still starting. */ }
    await delay(125);
  }
  throw new Error("Vite packet-demo server did not become ready.");
}

try {
  await waitForExit(fixtureProcess, "fixture generator");
  await mkdir(screenshotRoot, { recursive: true });
  const files = [
    join(fixtureRoot, "Walmart-ASY511185-80238229.pdf"),
    join(fixtureRoot, "ASY511185-80238229-detail-sheets.pdf"),
  ];
  assert(files.every(existsSync), "packet-demo fixtures must exist");

  const { chromium } = await import("playwright");
  server = spawn(process.execPath, ["node_modules/vite/bin/vite.js", "--host", "127.0.0.1", "--port", String(port), "--strictPort"], {
    cwd: new URL("..", import.meta.url),
    stdio: "pipe",
    env: { ...process.env, VITE_BLUEBRICK_BUILD_ID: "BB-ASSISTANT-PACKET-DEMO-LOCAL-VERIFY" },
  });
  await waitForServer();
  browser = await chromium.launch({ headless: true });
  const rows = [];

  for (const width of [260, 320, 480, 640]) {
    const page = await browser.newPage({ viewport: { width, height: 760 } });
    let externalRequests = 0;
    await page.route("**/*", async (route) => {
      const url = new URL(route.request().url());
      if (["127.0.0.1", "localhost"].includes(url.hostname)) await route.continue();
      else { externalRequests += 1; await route.abort(); }
    });
    await page.goto(`${origin}/?demo=packet-upload`, { waitUntil: "networkidle" });
    const example = page.locator(".packet-demo-example");
    await assert.doesNotReject(example.waitFor());
    assert.match(await example.locator(".role").innerText(), /demo example/i);
    const initialStyle = await example.evaluate((node) => {
      const style = getComputedStyle(node);
      const text = node.querySelector(".text");
      const role = node.querySelector(".role");
      return {
        background: style.backgroundColor,
        color: getComputedStyle(text).color,
        roleColor: getComputedStyle(role).color,
        roleOpacity: getComputedStyle(role).opacity,
      };
    });
    assert.equal(initialStyle.background, "rgb(201, 236, 255)");
    assert.equal(initialStyle.color, "rgb(8, 13, 18)");
    assert.equal(initialStyle.roleColor, "rgb(8, 13, 18)");
    assert.equal(initialStyle.roleOpacity, "1");
    const screenshot = [260, 640].includes(width)
      ? join(screenshotRoot, `packet-demo-initial-${width}.png`)
      : null;
    if (screenshot) await page.screenshot({ path: screenshot });
    const initialTranscript = await page.evaluate(() => window.bbGetTranscript());
    assert(!initialTranscript.some((item) => /Review the attached drawing packet/.test(item.text)), "presentation-only example is absent from the host transcript");

    const composer = page.getByLabel("Message BlueBrick Assistant");
    await composer.fill("Keep the upper detail open");
    await composer.press("Shift+Enter");
    await composer.pressSequentially("for a local review note.");
    assert.equal(await composer.inputValue(), "Keep the upper detail open\nfor a local review note.", "Shift+Enter retains a line break");
    await composer.press("Enter");
    await page.getByText("Demo note kept locally", { exact: false }).waitFor();
    assert.equal(await composer.inputValue(), "");
    assert.equal(await page.locator(".packet-demo-example").count(), 0, "a real demo turn replaces the presentation-only example");
    assert.equal(await page.locator(".msg.packet-demo-user").count(), 1);
    assert.equal(await page.locator(".msg.packet-demo-user .text").innerText(), "Keep the upper detail open\nfor a local review note.");
    assert.equal(await page.locator(".msg:not(.packet-demo-user)").count(), 1, "typing adds no fabricated assistant response");
    const localTranscript = await page.evaluate(() => window.bbGetTranscript());
    assert(!localTranscript.some((item) => /Keep the upper detail open/.test(item.text)), "local demo text is not dispatched through the host transcript");

    await page.locator('input[aria-label="Choose PDF or image files"]').setInputFiles(files);
    await page.getByText("DEMO · generated packet findings", { exact: false }).waitFor();
    assert.equal(await page.locator(".msg.packet-demo-user").count(), 2, "typed and ordered-upload user turns remain visible");
    const uploadStyle = await page.locator(".msg.packet-demo-user").last().evaluate((node) => {
      const role = node.querySelector(".role");
      const attachments = node.querySelector(".message-attachments");
      return {
        roleColor: getComputedStyle(role).color,
        roleOpacity: getComputedStyle(role).opacity,
        attachmentColor: getComputedStyle(attachments).color,
        attachmentOpacity: getComputedStyle(attachments).opacity,
      };
    });
    assert.deepEqual(uploadStyle, {
      roleColor: "rgb(8, 13, 18)",
      roleOpacity: "1",
      attachmentColor: "rgb(8, 13, 18)",
      attachmentOpacity: "1",
    });
    const metrics = await page.evaluate(() => ({
      clientWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
      bodyScrollWidth: document.body.scrollWidth,
      composerHeight: document.querySelector(".composer textarea")?.getBoundingClientRect().height ?? 0,
      sendBounds: document.querySelector(".send-button")?.getBoundingClientRect().toJSON(),
    }));
    assert(metrics.composerHeight >= (width <= 340 ? 58 : 68), `${width}px composer keeps two readable lines`);
    assert(metrics.scrollWidth <= metrics.clientWidth + 1 && metrics.bodyScrollWidth <= metrics.clientWidth + 1, `${width}px has no horizontal clipping`);
    assert(metrics.sendBounds && metrics.sendBounds.right <= width + 1, `${width}px Send remains reachable`);
    assert.equal(externalRequests, 0, "demo makes no external request");
    await page.evaluate(() => window.bbReset());
    await page.locator(".packet-demo-example").waitFor();
    assert.equal(await page.locator(".packet-demo-notice").count(), 0, "reset clears the prior local-demo notice");
    assert.equal(await page.locator(".msg.packet-demo-user").count(), 1, "reset restores only the presentation-only example user bubble");
    rows.push({ width, ...metrics, initialStyle, screenshot });
    await page.close();
  }

  const normal = await browser.newPage({ viewport: { width: 480, height: 760 } });
  await normal.addInitScript(() => {
    window.bbPacketDemoPosts = [];
    window.chrome = { webview: { postMessage: (payload) => window.bbPacketDemoPosts.push(payload) } };
  });
  await normal.goto(origin, { waitUntil: "networkidle" });
  await normal.evaluate(() => {
    window.bbSetModels([{ id: "host-model", displayName: "Host model", available: true }]);
    window.bbSetModel("host-model");
  });
  await normal.getByLabel("Message BlueBrick Assistant").fill("Normal host-backed send.");
  await normal.getByLabel("Send message").click();
  const posts = await normal.evaluate(() => window.bbPacketDemoPosts);
  assert(posts.some((payload) => payload?.type === "sendMessage" && payload.message === "Normal host-backed send."), "normal mode retains host-backed send");
  assert.equal(await normal.locator(".packet-demo-user").count(), 0);
  await normal.close();

  console.log(JSON.stringify({
    ok: true,
    checked: ["initial-example", "opaque-demo-text", "local-text-send", "ordered-two-pdf-upload", "reset-clears-local-notice", "no-external-requests", "narrow-composer", "normal-mode-host-send"],
    rows,
    runtimeCeiling: "VITE_BROWSER_DEMO_ONLY__NOT_SOLIDWORKS_OR_HOST_RUNTIME",
  }, null, 2));
} finally {
  await browser?.close();
  if (server && !server.killed) server.kill();
  await rm(fixtureRoot, { recursive: true, force: true });
}
