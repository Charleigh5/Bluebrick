import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const css = readFileSync(join(root, "src", "styles.css"), "utf8");
const narrowSmoke = readFileSync(join(root, "scripts", "narrow-smoke.mjs"), "utf8");

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function mediaRule(maxWidth) {
  const marker = `@media (max-width: ${maxWidth}px)`;
  const start = css.indexOf(marker);
  assert(start >= 0, `Missing responsive breakpoint ${marker}.`);
  const end = css.indexOf("@media", start + marker.length);
  return css.slice(start, end >= 0 ? end : css.length);
}

const compact = mediaRule(340);
const ultraNarrow = mediaRule(300);
assert(/\.shell\s*\{[^}]*min-width:\s*260px;/s.test(css), "Wide/default shell must retain its recovered 260px minimum contract.");
assert(/\.shell\s*\{[^}]*min-width:\s*0;[^}]*overflow-x:\s*hidden;/s.test(compact), "260/300/340 widths must release the default min-width and suppress unintended shell overflow.");
assert(/\.composer\s*\{[^}]*grid-template-columns:\s*minmax\(0,\s*1fr\);/s.test(compact), "260/300/340 widths must stack the composer so its action remains reachable.");
assert(/\.composer-actions\s*\{[^}]*grid-template-columns:\s*minmax\(0,\s*1fr\)\s+minmax\(0,\s*1fr\);/s.test(compact), "Compact composer actions must remain visible as two equal rails.");
assert(/\.scopes\s*\{[^}]*grid-template-columns:\s*1fr\s+1fr;/s.test(ultraNarrow), "300px width must reduce legacy scope controls to two columns.");
assert(/\.scope-chips\s*\{[^}]*display:\s*flex;[^}]*flex-wrap:\s*wrap;/s.test(css), "Current scope controls must wrap instead of relying on the retired grid selector.");
assert(narrowSmoke.includes("const widths = [260, 280, 300, 320, 340, 360, 480, 640];"), "The isolated render smoke manifest must enumerate the required narrow, normal, and enlarged widths.");

const { chromium } = await import("playwright");
const browser = await chromium.launch({ headless: true, args: ["--allow-file-access-from-files"] });
const renderRows = [];
const baseUrl = pathToFileURL(join(root, "dist", "index.html")).href;

function injectHostState(streaming = false) {
  window.bbSetModels([{ id: "local-model", displayName: "Local model", available: true }]);
  window.bbSetModel("local-model");
  window.bbSetScopes([
    { id: "local", label: "Local fixture", enabled: true },
    { id: "audit", label: "Audit", enabled: true },
    { id: "packet", label: "Packet fixture", enabled: true },
    { id: "lab", label: "Lab fixture", enabled: true },
  ]);
  window.bbSetScope("local");
  window.bbSetStatus({ connection: "READY", configured: true, mode: "LOCAL_ONLY" });
  window.bbSetTools([{ id: "fixture-tool" }]);
  window.bbSetToolReceipts([{ id: "fixture-receipt" }]);
  window.bbAppendToolResult({ label: "Fixture receipt", status: "done", message: "Visible local receipt" });
  window.bbAppendScreenshotArtifact({
    screenshotId: "responsive-shot", fileName: "local-fixture.png", width: 100, height: 80,
    localOnlyCloudState: "local only",
    annotations: [{ id: "a1", label: "Fixture annotation", source: "local", reviewStatus: "pending" }],
  });
  if (streaming) window.bbTypingStart();
}

function evaluateSurface(requiredSelectors) {
  const interactiveSelectorInPage = [
    "button",
    "select",
    "textarea",
    'input:not([type="hidden"]):not([type="file"])',
    "summary",
    '[role="button"]',
    ".packet-file-picker",
  ].join(",");
  function controlNameInPage(element, index) {
    const text = (element.getAttribute("aria-label") || element.id || element.textContent || "")
      .replace(/\s+/g, " ")
      .trim();
    return text ? `${element.tagName.toLowerCase()}:${text.slice(0, 80)}` : `${element.tagName.toLowerCase()}:${index}`;
  }

  function clippedByOverflowAncestorsInPage(element, box) {
    const clippedBy = [];
    for (let ancestor = element.parentElement; ancestor && ancestor !== document.documentElement; ancestor = ancestor.parentElement) {
      const style = getComputedStyle(ancestor);
      const clipsX = ["hidden", "scroll", "auto", "clip"].includes(style.overflowX);
      const clipsY = ["hidden", "scroll", "auto", "clip"].includes(style.overflowY);
      if (!clipsX && !clipsY) continue;
      const ancestorBox = ancestor.getBoundingClientRect();
      if ((clipsX && (box.left < ancestorBox.left - 1 || box.right > ancestorBox.right + 1)) ||
          (clipsY && (box.top < ancestorBox.top - 1 || box.bottom > ancestorBox.bottom + 1))) {
        clippedBy.push(ancestor.className || ancestor.id || ancestor.tagName.toLowerCase());
      }
    }
    return clippedBy;
  }

  function detailForInPage(element) {
    element.scrollIntoView({ block: "center", inline: "center" });
    const viewportWidth = document.documentElement.clientWidth;
    const viewportHeight = document.documentElement.clientHeight;
    const box = element.getBoundingClientRect();
    const style = getComputedStyle(element);
    return {
      present: true,
      visible: box.width > 0 && box.height > 0 && style.display !== "none" && style.visibility !== "hidden",
      inBounds: box.left >= -1 && box.right <= viewportWidth + 1 && box.top >= -1 && box.bottom <= viewportHeight + 1,
      clippedBy: clippedByOverflowAncestorsInPage(element, box),
      left: box.left, right: box.right, top: box.top, bottom: box.bottom,
    };
  }

  const viewportWidth = document.documentElement.clientWidth;
  const details = Object.fromEntries(requiredSelectors.map(([name, selector]) => {
    const element = document.querySelector(selector);
    return [name, element ? detailForInPage(element) : { present: false, visible: false, inBounds: false, clippedBy: [], left: null, right: null, top: null, bottom: null }];
  }));
  const interactiveControls = Array.from(document.querySelectorAll(interactiveSelectorInPage))
    .map((element, index) => ({ name: controlNameInPage(element, index), ...detailForInPage(element) }));
  return {
    viewportWidth,
    horizontalOverflow: document.documentElement.scrollWidth > viewportWidth + 1 || document.body.scrollWidth > viewportWidth + 1,
    details,
    interactiveControls,
  };
}

async function renderAt(page, width, mode, requiredSelectors, hostState) {
  const externalRequests = [];
  page.on("request", request => { if (!request.url().startsWith("file:")) externalRequests.push(request.url()); });
  await page.addInitScript(() => { window.chrome = { webview: { postMessage: () => {} } }; });
  await page.goto(`${baseUrl}${mode ? `?mode=${mode}` : ""}`);
  await page.waitForSelector(requiredSelectors[0][1], { timeout: 10000 });
  if (hostState !== null) {
    await page.evaluate(injectHostState, hostState);
    await page.waitForTimeout(50);
  }
  const row = await page.evaluate(evaluateSurface, requiredSelectors);
  return { width, mode: mode || "default", ...row, externalRequests };
}

try {
  for (const width of [260, 280, 300, 320, 340, 360, 480, 640]) {
    const page = await browser.newPage({ viewport: { width, height: 720 } });
    renderRows.push(await renderAt(page, width, "", [
      ["modelSelector", "#assistant-model"], ["scopeLocal", '[data-scope="local"]'], ["scopeAudit", '[data-scope="audit"]'],
      ["scopePacket", '[data-scope="packet"]'], ["scopeLab", '[data-scope="lab"]'],
      ["new", '[aria-label="New session"]'], ["capture", '[aria-label="Capture local screenshot"]'],
      ["attach", '[aria-label="Attach image or PDF"]'], ["search", '[aria-label="Search the selected scope"]'],
      ["more", '[aria-label="More actions"]'], ["composer", '.composer textarea'], ["send", '.send-button'],
      ["receipt", '.tool-card'], ["screenshot", '.shot'], ["approveScreenshot", '[aria-label^="Approve screenshot"]'],
      ["rejectScreenshot", '[aria-label="Reject screenshot review"]'],
    ], false));
    await page.close();

    const streaming = await browser.newPage({ viewport: { width, height: 720 } });
    renderRows.push(await renderAt(streaming, width, "", [
      ["composer", '.composer textarea'], ["stop", '.stop-button'],
    ], true));
    await streaming.close();

    const lab = await browser.newPage({ viewport: { width, height: 720 } });
    renderRows.push(await renderAt(lab, width, "vira-lab", [
      ["reloadFixture", '.context-panel button'], ["packetFilePicker", '.packet-file-picker'],
      ["exportLabReceipt", '.vira-lab-export-actions button'],
    ], null));
    await lab.close();

    const board = await browser.newPage({ viewport: { width, height: 720 } });
    renderRows.push(await renderAt(board, width, "execution-board", [
      ["query", "#engineering-query"], ["routeAction", ".eb-command-row button"],
      ["sampleAction", ".eb-samples button"], ["receiptExport", '.eb-section-title button'],
    ], null));
    await board.close();
  }
} finally {
  await browser.close();
}

for (const row of renderRows) {
  assert(!row.horizontalOverflow, `${row.width}px ${row.mode} render must not horizontally overflow.`);
  assert(row.externalRequests.length === 0, `${row.width}px ${row.mode} render must not request an external resource.`);
  for (const [name, detail] of Object.entries(row.details)) {
    assert(detail.present, `${row.width}px ${row.mode} must render ${name}.`);
    assert(detail.visible, `${row.width}px ${row.mode} must make ${name} visible.`);
    assert(detail.inBounds, `${row.width}px ${row.mode} must keep ${name} in bounds.`);
    assert(detail.clippedBy.length === 0, `${row.width}px ${row.mode} must not clip ${name}: ${detail.clippedBy.join(", ")}.`);
  }
  assert(row.interactiveControls.length > 0, `${row.width}px ${row.mode} must expose interactive controls.`);
  for (const control of row.interactiveControls) {
    assert(control.visible, `${row.width}px ${row.mode} must make ${control.name} visible.`);
    assert(control.inBounds, `${row.width}px ${row.mode} must keep ${control.name} in the viewport.`);
    assert(control.clippedBy.length === 0, `${row.width}px ${row.mode} must not clip ${control.name}: ${control.clippedBy.join(", ")}.`);
  }
}

console.log(JSON.stringify({
  ok: true,
  checkedWidths: [260, 280, 300, 320, 340, 360, 480, 640],
  checks: ["all_default_controls", "forced_streaming_stop", "screenshot_review_actions", "all_vira_lab_packet_controls", "all_execution_board_controls", "overflow_ancestor_clipping", "file_scheme_render_layout"],
  renderRows,
  runtimeCeiling: "FILE_SCHEME_RENDER_ONLY__NOT_SOLIDWORKS_RUNTIME",
}, null, 2));
