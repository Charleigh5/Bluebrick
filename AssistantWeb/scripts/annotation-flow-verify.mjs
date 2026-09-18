/**
 * Annotation-flow verification — standalone Playwright run against the
 * production dist bundle (same serving pattern as callback-state-smoke.mjs).
 *
 * Covers the full annotation workflow with REAL input events:
 *   toggle -> multi-pin (header/thread/composer) -> hover highlight ->
 *   badge layout -> note entry -> Copy JSON export -> persistence across
 *   reload -> pin removal -> badge-click duplicate exclusion.
 */
import { existsSync, statSync } from "node:fs";
import { createServer } from "node:http";
import { dirname, join } from "node:path";
import { extname, normalize } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const distRoot = join(root, "dist");
const indexPath = join(distRoot, "index.html");

function fail(message, extra = {}) {
  console.error(JSON.stringify({ ok: false, error: message, ...extra }, null, 2));
  process.exit(1);
}

if (!existsSync(indexPath)) fail("dist missing — build first.", { indexPath });

const mimeTypes = {
  ".html": "text/html",
  ".js": "text/javascript",
  ".css": "text/css",
  ".json": "application/json",
};

const staticServer = await new Promise((resolvePromise) => {
  const server = createServer((req, res) => {
    const url = new URL(req.url ?? "/", "http://localhost");
    let filePath = normalize(join(distRoot, url.pathname));
    if (!filePath.startsWith(distRoot)) {
      res.writeHead(403);
      res.end();
      return;
    }
    if (!existsSync(filePath) || filePath === distRoot || statSync(filePath).isDirectory()) {
      filePath = indexPath;
    }
    res.writeHead(200, { "Content-Type": mimeTypes[extname(filePath).toLowerCase()] ?? "application/octet-stream" });
    import("node:fs").then((fs) => fs.createReadStream(filePath).pipe(res));
  });
  server.listen(0, "127.0.0.1", () => resolvePromise({ server, origin: `http://127.0.0.1:${server.address().port}` }));
});

const failures = [];
const expect = (name, condition, detail) => {
  if (!condition) failures.push(`${name}: ${detail}`);
};

let chromium;
try {
  ({ chromium } = await import("playwright"));
} catch (error) {
  fail("Playwright is not available.", { detail: String(error) });
}

const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 480, height: 900 } });
  await page.addInitScript(() => {
    window.chrome = { webview: { postMessage() {} } };
  });
  await page.goto(staticServer.origin);
  await page.waitForSelector("[data-annotation-control]");

  // 1. Toggle annotation mode with a real click
  await page.click("[data-annotation-control]");
  await page.waitForSelector(".pin-editor");
  expect("toggle_on", (await page.getAttribute("[data-annotation-control]", "aria-pressed")) === "true", "aria-pressed");
  expect("cursor", (await page.evaluate(() => getComputedStyle(document.querySelector(".shell")).cursor)) === "crosshair", "crosshair expected");

  // 2. Multi-pin: header cap-count (select itself is disabled offline), thread
  //    empty-state, composer
  await page.click(".cap-count");
  await page.click(".empty");
  await page.click(".composer");
  await page.waitForTimeout(200);
  expect("three_pins", (await page.locator(".pin-row").count()) === 3, "3 pin rows expected");
  expect("three_overlay_badges", (await page.locator(".pin-layer .pin-badge").count()) === 3, "3 overlay badges expected");
  expect("stored_pins", (await page.evaluate(() => JSON.parse(localStorage.getItem("bb.pins") ?? "[]").length)) === 3, "localStorage count");

  // 3. Hover highlight on a pinnable region (chip-row is in the selector list)
  await page.hover(".chip-row");
  expect("hover_highlight", (await page.locator(".annotate-hover").count()) === 1, "exactly one highlighted element");

  // 4. Badge-click exclusion: clicking an overlay badge must NOT add a pin
  await page.locator(".pin-layer .pin-badge").first().click();
  await page.waitForTimeout(150);
  expect("badge_click_no_duplicate", (await page.locator(".pin-row").count()) === 3, "pin count must stay 3");

  // 5. Note entry on pin 2 (nth disambiguation)
  await page.locator(".pin-row textarea").nth(1).fill("make the empty state friendlier");
  expect("note_value", (await page.locator(".pin-row textarea").nth(1).inputValue()) === "make the empty state friendlier", "textarea round-trip");

  // 6. Copy JSON export: clipboard success shows "Copied", clipboard denial
  //    (headless default) reveals the JSON fallback textarea. Accept either.
  await page.getByRole("button", { name: "Copy JSON" }).click();
  const exportOutcome = await page
    .waitForSelector(".pin-editor .model-note.ok, .pin-export-area:not([hidden])")
    .then((el) => el.getAttribute("class"));
  expect(
    "export_status",
    exportOutcome === "model-note ok" || exportOutcome === "pin-export-area",
    `expected copy confirmation or JSON fallback, got class=${exportOutcome}`,
  );
  if (exportOutcome === "pin-export-area") {
    expect(
      "export_fallback_json",
      (await page.inputValue(".pin-export-area")).includes("bluebrick-assistant"),
      "fallback textarea must contain the JSON bundle",
    );
  }

  // 7. Persistence across reload (pins + note survive, toggle resets off)
  await page.reload();
  await page.waitForSelector("[data-annotation-control]");
  expect("persist_annotate_off", (await page.getAttribute("[data-annotation-control]", "aria-pressed")) === "false", "toggle resets");
  await page.click("[data-annotation-control]");
  await page.waitForSelector(".pin-editor");
  expect("persist_rows", (await page.locator(".pin-row").count()) === 3, "3 rows after reload");
  expect("persist_note", (await page.locator(".pin-row textarea").nth(1).inputValue()) === "make the empty state friendlier", "note survives reload");

  // 8. Remove pin 2; count drops everywhere
  await page.getByRole("button", { name: "Remove pin 2" }).click();
  await page.waitForTimeout(150);
  expect("after_remove_rows", (await page.locator(".pin-row").count()) === 2, "2 rows after remove");
  expect("after_remove_stored", (await page.evaluate(() => JSON.parse(localStorage.getItem("bb.pins") ?? "[]").length)) === 2, "localStorage updated");

  // 9. Clear all
  await page.getByRole("button", { name: "Clear" }).click();
  await page.waitForTimeout(150);
  expect("clear_all", (await page.locator(".pin-row").count()) === 0, "0 rows after clear");
} finally {
  await browser.close();
  staticServer.server.close();
}

if (failures.length > 0) {
  console.error(JSON.stringify({ ok: false, failures }, null, 2));
  process.exit(1);
}
console.log(JSON.stringify({ ok: true, checks: 15 }, null, 2));
