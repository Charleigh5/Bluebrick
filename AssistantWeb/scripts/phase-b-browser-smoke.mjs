import { createHash } from "node:crypto";
import { existsSync, mkdirSync } from "node:fs";
import { join } from "node:path";

let chromium;
try {
  ({ chromium } = await import("playwright"));
} catch (error) {
  console.error(JSON.stringify({ ok: false, error: "Playwright runtime unavailable.", detail: String(error) }, null, 2));
  process.exit(1);
}

const origin = process.argv[2] || "http://127.0.0.1:5179";
const fixture = process.argv[3] || join(process.env.TEMP || ".", "vira-phase-b-browser-fixture.pdf");
if (!existsSync(fixture)) throw new Error(`PDF fixture missing: ${fixture}`);
const output = join(process.env.TEMP || ".", "bluebrick-assistant-verification", "phase-b-browser");
mkdirSync(output, { recursive: true });
const digest = (value) => `value_sha256:${createHash("sha256").update(value).digest("hex").slice(0, 16)}`;
const identifierHash = digest("MPM511284-80241102");
const descriptionHash = digest("MOUNTING BRACKET");
const components = [1, 2].map((index) => ({
  snapshotId: `component-${index}`,
  parentSnapshotId: "sub-1",
  nativeComponentId: index,
  depth: 1,
  nameHash: `name_sha256:${index}`,
  nativePathHash: `native_path_sha256:${index}`,
  identifierHash,
  referencedConfigurationHash: digest("DEFAULT"),
  kind: "part",
  suppressionState: "fully-resolved",
  resolutionState: "resolved",
  childrenState: "none",
  isVirtual: false,
  isGraphicsOnly: false,
  isSpeedPak: false,
  propertyEvidence: [{
    evidenceId: `cad:component:description:${index}`,
    canonicalField: "description",
    scope: "component",
    rawValueHash: descriptionHash,
    evaluatedValueHash: descriptionHash,
    normalizedValueHash: descriptionHash,
    wasResolved: true,
    linkedToParent: false,
    resultCode: 0,
    readStatus: "resolved",
    ruleId: "VIRA-CAD-COMPONENT-PROPERTY-CACHED-001"
  }],
  limitations: []
}));
const toolResult = {
  toolName: "read_active_document_context",
  status: "ok",
  message: "Approved redacted browser fixture.",
  items: [{ title: "Active assembly fixture", metadata: {
    document_type: "ASSEMBLY",
    component_evidence_json: JSON.stringify(components),
    assembly_traversal_json: JSON.stringify({ maxDepth: 32, recordLimit: 5000, recordedCount: 2, unloadedCount: 0, cycleCount: 0, truncated: false, mutationActions: 0, externalSystemsAccessed: false, warnings: [] }),
    assembly_payload_status: "ok",
    mutation_actions: "0"
  }}]
};

const browser = await chromium.launch({ headless: true });
const rows = [];
try {
  for (const width of [320, 380, 480]) {
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    let blockedExternalRequests = 0;
    await page.route("**/*", async (route) => {
      const url = new URL(route.request().url());
      if (!["127.0.0.1", "localhost"].includes(url.hostname)) {
        blockedExternalRequests++;
        await route.abort();
      } else {
        await route.continue();
      }
    });
    await page.goto(origin, { waitUntil: "networkidle" });
    await page.waitForFunction(() => typeof window.bbAppendToolResult === "function");
    await page.evaluate((result) => window.bbAppendToolResult?.(result), toolResult);
    await page.locator('input[aria-label="Choose engineering PDF packet"]').setInputFiles(fixture);
    await page.getByText("Packet ↔ CAD Phase B").waitFor();
    await page.getByText("BOM ↔ active assembly").waitFor();
    const phaseB = page.locator(".packet-cad-phase-b");
    await phaseB.getByText("100%").first().waitFor();
    const metrics = await page.evaluate(() => ({
      clientWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
      bodyScrollWidth: document.body.scrollWidth,
      unnamedControls: [...document.querySelectorAll("button,input,select,textarea")].filter((item) => !item.getAttribute("aria-label") && !item.textContent?.trim() && !item.getAttribute("title")).length
    }));
    const screenshot = join(output, `phase-b-${width}.png`);
    await phaseB.screenshot({ path: screenshot });
    rows.push({
      width,
      ...metrics,
      horizontalOverflow: metrics.scrollWidth > metrics.clientWidth || metrics.bodyScrollWidth > metrics.clientWidth,
      phaseBVisible: await phaseB.isVisible(),
      exactMatches: await phaseB.locator(".packet-cad-status.exact-match").count(),
      responsibilityLabels: await phaseB.locator(".packet-cad-status.not-applicable").count(),
      blockedExternalRequests,
      screenshot
    });
    await page.close();
  }
} finally {
  await browser.close();
}

const ok = rows.every((row) => row.phaseBVisible && !row.horizontalOverflow && row.unnamedControls === 0 && row.exactMatches >= 2 && row.responsibilityLabels >= 1 && row.blockedExternalRequests === 0);
console.log(JSON.stringify({ ok, origin, fixture, safetyBoundary: { launchesSolidWorks: false, callsLiveConnectors: false, uploadsPacket: false, mutationActions: 0 }, rows }, null, 2));
if (!ok) process.exit(1);
