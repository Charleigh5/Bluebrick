import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { readFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, extname, join, normalize, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(root);
const distRoot = resolveArgument("--dist=", join(root, "dist"));
const indexPath = join(distRoot, "index.html");
const outputRoot = resolveArgument(
  "--out=",
  join(process.env.TEMP ?? root, "vira-lab-smoke", new Date().toISOString().replace(/[:.]/g, "-"))
);
const widths = [320, 380, 480];

function resolveArgument(prefix, fallback) {
  const value = process.argv.find((argument) => argument.startsWith(prefix));
  return resolve(value ? value.slice(prefix.length) : fallback);
}

function fail(message, extra = {}) {
  console.error(JSON.stringify({ ok: false, error: message, ...extra }, null, 2));
  process.exit(1);
}

if (!existsSync(indexPath)) {
  fail("VIRA Lab smoke requires a production build. Run npm run build first or pass --dist=<temporary-build-path>.", { indexPath });
}

let chromium;
try {
  ({ chromium } = await import("playwright"));
} catch (error) {
  fail("Playwright is not available from the existing AssistantWeb dependencies.", {
    detail: error instanceof Error ? error.message : String(error)
  });
}

mkdirSync(outputRoot, { recursive: true });
const fixtureRoot = join(outputRoot, "fixtures");
mkdirSync(fixtureRoot, { recursive: true });
const validPacketPath = join(fixtureRoot, "ASY511185-80238229-packet.pdf");
const blankPacketPath = join(fixtureRoot, "blank-packet.pdf");
const invalidPacketPath = join(fixtureRoot, "invalid-packet.pdf");
writePdfFixture(validPacketPath, [
  "DOC NO",
  "PART NO",
  "80238229",
  "ASY511185",
  "REV B",
  "DESCRIPTION: OPTICAL VALUE SIGN HOLDER",
  "SHEET 1 OF 1",
  "ITEM QTY PART NUMBER DESCRIPTION",
  "1 2 MPM511284-80241102 MOUNTING BRACKET",
  "2 1 PBO511290-80241108 PURCHASED BY OTHERS"
]);
writePdfFixture(blankPacketPath, []);
writeFileSync(invalidPacketPath, "not a PDF", "utf8");

const staticServer = await startStaticServer();
const browser = await chromium.launch({ headless: true });
const rows = [];
const negativeStates = {};
const edgeCases = {};
const outputExports = {};
const regressions = {};

try {
  for (const width of widths) {
    const page = await browser.newPage({ viewport: { width, height: 800 }, deviceScaleFactor: 1 });
    const health = attachHealthChecks(page, staticServer.origin);
    await page.goto(`${staticServer.origin}/?mode=vira-lab`);
    await page.getByRole("heading", { name: "VIRA Lab" }).waitFor({ timeout: 10000 });

    const reload = page.getByRole("button", { name: "Reload fixture" });
    assert.equal(await reload.count(), 1);
    await reload.click();
    await page.locator('.vira-lab-shell[data-workbench-state="ready"]').waitFor({ timeout: 5000 });
    await page.getByLabel("Choose engineering PDF packet").setInputFiles(validPacketPath);
    await page.locator('.vira-lab-shell[data-packet-state="partial"]').waitFor({ timeout: 15000 });
    await page.locator(".packet-review-workbench").waitFor({ timeout: 5000 });

    const result = await page.evaluate(() => {
      const documentRoot = document.documentElement;
      const buttons = Array.from(document.querySelectorAll("button"));
      const identifierSummary = Array.from(document.querySelectorAll(".packet-summary span"))
        .find((item) => /identifiers/i.test(item.textContent ?? ""));
      return {
        clientWidth: documentRoot.clientWidth,
        scrollWidth: documentRoot.scrollWidth,
        horizontalOverflow: documentRoot.scrollWidth > documentRoot.clientWidth + 1,
        workbenchState: document.querySelector(".vira-lab-shell")?.getAttribute("data-workbench-state") ?? "missing",
        fixtureRuntimeVisible: /Fixture/i.test(document.querySelector(".vira-lab-runtime")?.textContent ?? ""),
        readOnlyBoundaryVisible: /Read-only browser lab/i.test(document.body.textContent ?? ""),
        mutationBoundaryVisible: /0 mutation actions/i.test(document.body.textContent ?? ""),
        packetState: document.querySelector(".vira-lab-shell")?.getAttribute("data-packet-state") ?? "missing",
        splitIdentifierVisible:
          identifierSummary?.querySelector("strong")?.textContent === "3" &&
          /ASY511185-80238229/.test(document.body.textContent ?? ""),
        phaseAConfirmed: /Identity authority\s*confirmed/i.test(document.body.textContent ?? ""),
        phaseBVisible: /Packet ↔ CAD Phase B/i.test(document.body.textContent ?? ""),
        retainedEvidenceVisible: Boolean(document.querySelector(".packet-review-workbench")),
        unnamedButtonCount: buttons.filter((button) => !(button.getAttribute("aria-label") || button.textContent?.trim())).length
      };
    });

    const screenshot = join(outputRoot, `vira-lab-${width}.png`);
    await page.screenshot({ path: screenshot, fullPage: true });
    rows.push({
      width,
      ...result,
      consoleErrors: health.errors,
      blockedExternalRequests: health.blockedExternalRequests,
      screenshot
    });
    await page.close();
  }

  const statePage = await browser.newPage({ viewport: { width: 480, height: 800 }, deviceScaleFactor: 1 });
  const stateHealth = attachHealthChecks(statePage, staticServer.origin);

  await statePage.goto(`${staticServer.origin}/?mode=vira-lab&runtime=embedded-host`);
  await statePage.locator('.vira-lab-shell[data-workbench-state="unavailable"]').waitFor({ timeout: 10000 });
  negativeStates.embeddedHost = {
    state: await statePage.locator(".vira-lab-shell").getAttribute("data-workbench-state"),
    messageVisible: await statePage
      .getByRole("region", { name: "Active document context" })
      .getByText("The embedded SOLIDWORKS host adapter is not enabled in the browser-first slice.", { exact: true })
      .isVisible()
  };

  await statePage.goto(`${staticServer.origin}/?mode=vira-lab&fixtureState=error`);
  await statePage.locator('.vira-lab-shell[data-workbench-state="error"]').waitFor({ timeout: 10000 });
  negativeStates.fixtureError = {
    state: await statePage.locator(".vira-lab-shell").getAttribute("data-workbench-state"),
    messageVisible: await statePage
      .getByRole("region", { name: "Active document context" })
      .getByText("The fixture context returned a controlled test error.", { exact: true })
      .isVisible()
  };

  await statePage.goto(`${staticServer.origin}/?mode=vira-lab`);
  await statePage.getByLabel("Choose engineering PDF packet").setInputFiles(validPacketPath);
  await statePage.locator('.vira-lab-shell[data-packet-state="partial"]').waitFor({ timeout: 15000 });
  const phaseBTable = statePage.getByRole("table", { name: "Packet BOM to active assembly comparisons" });
  const allPhaseBRows = await phaseBTable.locator("tbody tr").count();
  await statePage.getByRole("button", { name: "Issues only" }).click();
  const issuePhaseBRows = await phaseBTable.locator("tbody tr").count();
  await statePage.getByRole("button", { name: "Unresolved only" }).click();
  const unresolvedPhaseBRows = await phaseBTable.locator("tbody tr").count();
  await statePage.getByRole("button", { name: "All", exact: true }).click();
  edgeCases.phaseBFilters = {
    allRows: allPhaseBRows,
    issueRows: issuePhaseBRows,
    unresolvedRows: unresolvedPhaseBRows,
    evidenceRetained: (await statePage.locator(".packet-review-workbench").count()) === 1
  };
  const receiptDownloadPromise = statePage.waitForEvent("download");
  await statePage.getByRole("button", { name: "Export VIRA receipt" }).click();
  const receiptDownload = await receiptDownloadPromise;
  const receiptPath = join(outputRoot, receiptDownload.suggestedFilename());
  await receiptDownload.saveAs(receiptPath);
  const receipt = JSON.parse(readFileSync(receiptPath, "utf8"));

  const diagnosticsDownloadPromise = statePage.waitForEvent("download");
  await statePage.getByRole("button", { name: "Export diagnostics" }).click();
  const diagnosticsDownload = await diagnosticsDownloadPromise;
  const diagnosticsPath = join(outputRoot, diagnosticsDownload.suggestedFilename());
  await diagnosticsDownload.saveAs(diagnosticsPath);
  const diagnosticExport = JSON.parse(readFileSync(diagnosticsPath, "utf8"));
  const serializedExports = JSON.stringify({ receipt, diagnosticExport });
  outputExports.receipt = {
    schemaVersion: receipt.schemaVersion,
    phaseAAuthority: receipt.packet?.phaseAAuthority,
    retainedEvidence: receipt.packet?.retainedEvidence,
    mutationActions: receipt.actions?.engineeringMutations,
    externalRequests: receipt.actions?.externalRequests,
    sha256: sha256(receiptPath)
  };
  outputExports.diagnostics = {
    schemaVersion: diagnosticExport.schemaVersion,
    entryCount: diagnosticExport.entries?.length,
    boundedTo: diagnosticExport.boundedTo,
    redaction: diagnosticExport.redaction,
    sha256: sha256(diagnosticsPath)
  };
  outputExports.forbiddenContentAbsent = [
    "DRAWING NO ASY511185-80238229",
    "OPTICAL VALUE SIGN HOLDER",
    fixtureRoot,
    "sk-",
    "ghp_"
  ].every((value) => !serializedExports.includes(value));

  const beforeReplacement = await statePage.locator(".packet-review-workbench").textContent();
  await statePage.getByLabel("Choose engineering PDF packet").setInputFiles(invalidPacketPath);
  await statePage.locator('.vira-lab-shell[data-packet-state="partial"]').waitFor({ timeout: 10000 });
  edgeCases.invalidReplacement = {
    retainedPriorWorkbench: (await statePage.locator(".packet-review-workbench").count()) === 1,
    retainedPriorEvidence: (await statePage.locator(".packet-review-workbench").textContent()) === beforeReplacement,
    alertVisible: await statePage.getByRole("alert").isVisible()
  };
  await statePage.getByRole("button", { name: "Clear packet" }).click();
  await statePage.locator('.vira-lab-shell[data-packet-state="cancelled"]').waitFor({ timeout: 5000 });
  edgeCases.clearPacket = {
    state: await statePage.locator(".vira-lab-shell").getAttribute("data-packet-state"),
    workbenchRemoved: (await statePage.locator(".packet-review-workbench").count()) === 0
  };

  await statePage.getByLabel("Choose engineering PDF packet").setInputFiles(invalidPacketPath);
  await statePage.locator('.vira-lab-shell[data-packet-state="failed"]').waitFor({ timeout: 10000 });
  edgeCases.invalidFirstPacket = {
    state: await statePage.locator(".vira-lab-shell").getAttribute("data-packet-state"),
    alertVisible: await statePage.getByRole("alert").isVisible(),
    workbenchAbsent: (await statePage.locator(".packet-review-workbench").count()) === 0
  };

  await statePage.getByLabel("Choose engineering PDF packet").setInputFiles(blankPacketPath);
  await statePage.locator('.vira-lab-shell[data-packet-state="partial"]').waitFor({ timeout: 15000 });
  edgeCases.blankPacket = {
    state: await statePage.locator(".vira-lab-shell").getAttribute("data-packet-state"),
    onePageVisible: await statePage.getByText("1 pages", { exact: false }).first().isVisible(),
    findingsVisible: await statePage.getByText("Review findings", { exact: false }).first().isVisible(),
    activeDocumentNeedsVerification:
      (await statePage.locator(".packet-comparison strong").textContent())?.trim() === "needs verification",
    phaseAUnresolved:
      (await statePage.locator(".packet-cad-identity strong").textContent())?.trim() === "unresolved",
    noIdentityMatchSource:
      (await statePage.locator(".packet-cad-identity small").textContent())?.trim() === "No independent identity match source"
  };

  await statePage.goto(`${staticServer.origin}/?mode=vira-lab&fixtureState=no-document`);
  await statePage.getByLabel("Choose engineering PDF packet").setInputFiles(validPacketPath);
  await statePage.locator('.vira-lab-shell[data-packet-state="partial"]').waitFor({ timeout: 15000 });
  edgeCases.noActiveDocument = {
    workbenchState: await statePage.locator(".vira-lab-shell").getAttribute("data-workbench-state"),
    packetState: await statePage.locator(".vira-lab-shell").getAttribute("data-packet-state"),
    packetEvidenceVisible: (await statePage.locator(".packet-review-workbench").count()) === 1
  };
  edgeCases.consoleMessages = stateHealth.errors;
  edgeCases.blockedExternalRequests = stateHealth.blockedExternalRequests;
  await statePage.close();

  const regressionPage = await browser.newPage({ viewport: { width: 480, height: 800 }, deviceScaleFactor: 1 });
  const regressionHealth = attachHealthChecks(regressionPage, staticServer.origin);
  await regressionPage.goto(`${staticServer.origin}/?mode=execution-board`);
  regressions.executionBoard = await regressionPage.getByRole("heading", { name: "Engineering Query Sandbox" }).isVisible();
  await regressionPage.goto(`${staticServer.origin}/?mode=unknown`);
  regressions.defaultAssistant = await regressionPage.getByText("BlueBrick Assistant", { exact: true }).isVisible();
  regressions.consoleErrors = regressionHealth.errors;
  regressions.blockedExternalRequests = regressionHealth.blockedExternalRequests;
  await regressionPage.close();
} finally {
  await browser.close();
  await staticServer.close();
}

const passed =
  rows.every(
    (row) =>
      !row.horizontalOverflow &&
      row.workbenchState === "ready" &&
      row.fixtureRuntimeVisible &&
      row.readOnlyBoundaryVisible &&
      row.mutationBoundaryVisible &&
      row.packetState === "partial" &&
      row.splitIdentifierVisible &&
      row.phaseAConfirmed &&
      row.phaseBVisible &&
      row.retainedEvidenceVisible &&
      row.unnamedButtonCount === 0 &&
      row.consoleErrors.length === 0 &&
      row.blockedExternalRequests.length === 0
  ) &&
  negativeStates.embeddedHost?.state === "unavailable" &&
  negativeStates.embeddedHost?.messageVisible === true &&
  negativeStates.fixtureError?.state === "error" &&
  negativeStates.fixtureError?.messageVisible === true &&
  edgeCases.invalidReplacement?.retainedPriorWorkbench === true &&
  edgeCases.invalidReplacement?.retainedPriorEvidence === true &&
  edgeCases.invalidReplacement?.alertVisible === true &&
  edgeCases.clearPacket?.state === "cancelled" &&
  edgeCases.clearPacket?.workbenchRemoved === true &&
  edgeCases.invalidFirstPacket?.state === "failed" &&
  edgeCases.invalidFirstPacket?.alertVisible === true &&
  edgeCases.invalidFirstPacket?.workbenchAbsent === true &&
  edgeCases.blankPacket?.state === "partial" &&
  edgeCases.blankPacket?.onePageVisible === true &&
  edgeCases.blankPacket?.findingsVisible === true &&
  edgeCases.blankPacket?.activeDocumentNeedsVerification === true &&
  edgeCases.blankPacket?.phaseAUnresolved === true &&
  edgeCases.blankPacket?.noIdentityMatchSource === true &&
  edgeCases.noActiveDocument?.workbenchState === "ready" &&
  edgeCases.noActiveDocument?.packetState === "partial" &&
  edgeCases.noActiveDocument?.packetEvidenceVisible === true &&
  edgeCases.phaseBFilters?.allRows === 2 &&
  edgeCases.phaseBFilters?.issueRows === 1 &&
  edgeCases.phaseBFilters?.unresolvedRows === 0 &&
  edgeCases.phaseBFilters?.evidenceRetained === true &&
  edgeCases.consoleMessages?.every((message) => message.includes("Warning: Indexing all PDF objects")) &&
  edgeCases.blockedExternalRequests?.length === 0 &&
  outputExports.receipt?.schemaVersion === "vira.lab.receipt.v1" &&
  outputExports.receipt?.phaseAAuthority === "confirmed" &&
  outputExports.receipt?.retainedEvidence === true &&
  outputExports.receipt?.mutationActions === 0 &&
  outputExports.receipt?.externalRequests === 0 &&
  outputExports.diagnostics?.schemaVersion === "vira.lab.diagnostics.v1" &&
  outputExports.diagnostics?.boundedTo === 20 &&
  outputExports.diagnostics?.redaction?.includesRawPacketText === false &&
  outputExports.diagnostics?.redaction?.includesCadPaths === false &&
  outputExports.diagnostics?.redaction?.includesSecrets === false &&
  outputExports.forbiddenContentAbsent === true &&
  regressions.executionBoard === true &&
  regressions.defaultAssistant === true &&
  regressions.consoleErrors.length === 0 &&
  regressions.blockedExternalRequests.length === 0;

const summary = {
  ok: passed,
  schemaVersion: "2026-07-23.vira-lab-local-packet-smoke.v2",
  distRoot,
  outputRoot,
  rows: rows.map((row) => ({
    ...row,
    screenshot: relative(repoRoot, row.screenshot).replace(/\\/g, "/"),
    screenshotSha256: sha256(row.screenshot)
  })),
  negativeStates,
  edgeCases,
  outputExports,
  regressions,
  safetyBoundary: {
    launchesSolidWorks: false,
    attachesCom: false,
    opensCadFiles: false,
    accessesPdm: false,
    accessesEpicor: false,
    accessesSalesforce: false,
    readsSecrets: false,
    externalRequests: false,
    mutationActions: 0
  }
};

writeFileSync(join(outputRoot, "vira-lab-smoke-summary.json"), JSON.stringify(summary, null, 2), "utf8");
console.log(JSON.stringify(summary, null, 2));
if (!passed) process.exit(1);

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

async function startStaticServer() {
  const distResolved = resolve(distRoot);
  const contentTypes = new Map([
    [".html", "text/html; charset=utf-8"],
    [".js", "text/javascript; charset=utf-8"],
    [".mjs", "text/javascript; charset=utf-8"],
    [".css", "text/css; charset=utf-8"],
    [".json", "application/json; charset=utf-8"]
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

function writePdfFixture(path, lines) {
  const escapePdf = (value) => value.replaceAll("\\", "\\\\").replaceAll("(", "\\(").replaceAll(")", "\\)");
  const content = [
    "BT",
    "/F1 11 Tf",
    "48 740 Td",
    ...lines.flatMap((line, index) => [index ? "0 -18 Td" : "", `(${escapePdf(line)}) Tj`]).filter(Boolean),
    "ET"
  ].join("\n");
  const objects = [
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
    `<< /Length ${Buffer.byteLength(content, "ascii")} >>\nstream\n${content}\nendstream`,
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
  ];
  let pdf = "%PDF-1.4\n";
  const offsets = [0];
  objects.forEach((object, index) => {
    offsets.push(Buffer.byteLength(pdf, "ascii"));
    pdf += `${index + 1} 0 obj\n${object}\nendobj\n`;
  });
  const xref = Buffer.byteLength(pdf, "ascii");
  pdf += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  for (const offset of offsets.slice(1)) pdf += `${String(offset).padStart(10, "0")} 00000 n \n`;
  pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  writeFileSync(path, Buffer.from(pdf, "ascii"));
}
