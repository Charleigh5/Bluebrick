/**
 * Template-flow verification — standalone Playwright run against the
 * production dist bundle (same serving pattern as annotation-flow-verify.mjs).
 *
 * Covers with REAL input events:
 *   attach removed from toolbar -> SVG paperclip at composer rail right end ->
 *   light-gray thread -> premade template chips -> configurator ->
 *   custom template add -> one-click run -> removal -> persistence across reload.
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
  await page.waitForSelector(".composer-template-rail");

  const toolbarLabels = await page.$$eval(".primary-action-rail .toolbar-action", (buttons) =>
    buttons.map((b) => b.getAttribute("aria-label")),
  );
  expect("attach_removed_from_toolbar", !toolbarLabels.includes("Attach"), `toolbar=${toolbarLabels.join(",")}`);

  const paperclipState = await page.evaluate(() => {
    const btn = document.querySelector(".paperclip-button");
    const rail = document.querySelector(".composer-context-rail");
    const mp = document.querySelector(".model-picker");
    if (!btn || !rail || !mp) return null;
    const b = btn.getBoundingClientRect();
    const r = rail.getBoundingClientRect();
    const m = mp.getBoundingClientRect();
    return {
      svg: !!btn.querySelector("svg.toolbar-icon"),
      atRight: Math.abs(r.right - b.right) < 14,
      rightOfPicker: b.left > m.left,
    };
  });
  expect("paperclip_present", !!paperclipState, "paperclip-button/context rail missing");
  if (paperclipState) {
    expect("paperclip_svg_icon", paperclipState.svg, JSON.stringify(paperclipState));
    expect("paperclip_at_rail_right", paperclipState.atRight, JSON.stringify(paperclipState));
    expect("paperclip_right_of_model_picker", paperclipState.rightOfPicker, JSON.stringify(paperclipState));
  }

  const threadBg = await page.$eval("main > section", (el) => getComputedStyle(el).backgroundColor);
  expect("thread_light_gray", threadBg === "rgb(226, 229, 234)", `bg=${threadBg}`);

  const chips = await page.$$eval(".composer-template-rail .template-chip", (buttons) =>
    buttons.map((b) => b.textContent?.trim()),
  );
  expect("premade_chips", chips.length === 4, `chips=${chips.join(",")}`);

  await page.click(".template-add");
  await page.waitForSelector(".template-configurator");

  await page.fill('.template-custom-form input[aria-label="Custom template label"]', "Verify template");
  await page.fill('.template-custom-form input[aria-label="Custom template prompt text"]', "Verification prompt text.");
  await page.click(".template-custom-form button");
  await page.waitForSelector(".template-custom-item");

  const chipsAfterAdd = await page.$$eval(".composer-template-rail .template-chip", (buttons) =>
    buttons.map((b) => b.textContent?.trim()),
  );
  expect("custom_chip_added", chipsAfterAdd.includes("Verify template"), `chips=${chipsAfterAdd.join(",")}`);

  await page.click('.composer-template-rail .template-chip:has-text("Verify template")');
  await page.waitForSelector(".msg.user");
  const sentText = await page.$$eval(".msg.user .text", (nodes) => nodes.map((n) => n.textContent?.trim()));
  expect("template_run_sends_prompt", sentText.includes("Verification prompt text."), `sent=${sentText.join(" | ")}`);

  await page.click(".template-custom-item button");
  await page.waitForFunction(() => !document.querySelector(".template-custom-item"));
  const chipsAfterRemove = await page.$$eval(".composer-template-rail .template-chip", (buttons) =>
    buttons.map((b) => b.textContent?.trim()),
  );
  expect("custom_chip_removed", !chipsAfterRemove.includes("Verify template"), `chips=${chipsAfterRemove.join(",")}`);

  await page.fill('.template-custom-form input[aria-label="Custom template label"]', "Persistent template");
  await page.fill('.template-custom-form input[aria-label="Custom template prompt text"]', "Persisted prompt.");
  await page.click(".template-custom-form button");
  await page.waitForSelector(".template-custom-item");
  await page.reload();
  await page.waitForSelector(".composer-template-rail");
  const chipsAfterReload = await page.$$eval(".composer-template-rail .template-chip", (buttons) =>
    buttons.map((b) => b.textContent?.trim()),
  );
  expect("custom_template_persists", chipsAfterReload.includes("Persistent template"), `chips=${chipsAfterReload.join(",")}`);

  await browser.close();
} catch (error) {
  await browser.close().catch(() => {});
  fail("Flow error.", { detail: String(error) });
}

if (failures.length) {
  staticServer.server.close();
  console.error(JSON.stringify({ ok: false, failures }, null, 2));
  process.exit(1);
}
staticServer.server.close();
console.log(JSON.stringify({ ok: true, checks: 10 }, null, 2));
