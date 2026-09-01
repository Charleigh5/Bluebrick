import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { readFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, join } from "node:path";
import { extname, normalize, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const distRoot = join(root, "dist");
const indexPath = join(distRoot, "index.html");
const widths = [260, 280, 300, 320, 340, 360, 480, 640];
const outputRoot = resolveOutputRoot();

function resolveOutputRoot() {
  const outArg = process.argv.find((arg) => arg.startsWith("--out="));
  if (outArg) return outArg.slice("--out=".length);

  const stamp = new Date().toISOString().replace(/[:.]/g, "-");
  return join(process.env.TEMP ?? root, "bluebrick-assistant-verification", `narrow-smoke-${stamp}`);
}

function fail(message, extra = {}) {
  console.error(JSON.stringify({ ok: false, error: message, ...extra }, null, 2));
  process.exit(1);
}

if (!existsSync(indexPath)) {
  fail("AssistantWeb dist shell is missing. Run this smoke only after static dist assets exist.", { indexPath });
}

let chromium;
try {
  ({ chromium } = await import("playwright"));
} catch (error) {
  fail("Playwright is not available. Install dependencies only after approval, then rerun this smoke.", {
    detail: error instanceof Error ? error.message : String(error)
  });
}

mkdirSync(outputRoot, { recursive: true });

const staticServer = await startStaticServer();
const browser = await chromium.launch({ headless: true });
const rows = [];
try {
  const url = `${staticServer.origin}/index.html`;

  for (const width of widths) {
    const page = await browser.newPage({
      viewport: { width, height: 720 },
      deviceScaleFactor: 1
    });

    await page.addInitScript(() => {
      window.__bbBridgeMessages = [];
      window.chrome = {
        webview: {
          postMessage(message) {
            window.__bbBridgeMessages.push(message);
          }
        }
      };
    });

    const blockedRequests = [];
    await page.route("**/*", async (route) => {
      const requestUrl = route.request().url();
      if (
        requestUrl.startsWith(staticServer.origin) ||
        requestUrl.startsWith("data:") ||
        requestUrl.startsWith("blob:")
      ) {
        await route.continue();
        return;
      }

      blockedRequests.push(requestUrl);
      await route.abort();
    });

    await page.goto(url);
    await page.waitForSelector(".shell", { timeout: 10000 });
    await seedShell(page);
    await exerciseBridge(page);
    await page.waitForTimeout(250);

    const result = await page.evaluate(() => {
      const doc = document.documentElement;
      const composer = document.querySelector(".composer textarea");
      const send = document.querySelector(".send-button");
      const receiptText = document.body.textContent ?? "";
      const sourceChips = Array.from(document.querySelectorAll(".scope-chips .scope"));
      const formControls = Array.from(document.querySelectorAll("textarea,input,select"));
      const bridgeMessages = Array.isArray(window.__bbBridgeMessages) ? window.__bbBridgeMessages : [];
      const bridgeTypes = bridgeMessages.map((message) => message?.type).filter(Boolean);
      const requiredBridgeTypes = [
        "selectModel",
        "selectScope",
        "captureScreenshot",
        "search",
        "sendMessage",
        "cancelMessage",
        "reviewScreenshotItem"
      ];
      // Frozen browser→host contract: exactly these ten message types may cross.
      const frozenBridgeTypesExact10 = [
        "newSession",
        "captureScreenshot",
        "attach",
        "search",
        "selectModel",
        "selectScope",
        "sendMessage",
        "cancelMessage",
        "saveScreenshotAnnotation",
        "reviewScreenshotItem"
      ];
      const unexpectedBridgeTypes = bridgeTypes.filter((type) => !frozenBridgeTypesExact10.includes(type));

      // Verify all 17 bb* callbacks are installed on window
      const expectedCallbacks = [
        "bbReset", "bbAppend", "bbTypingStart", "bbAppendChunk", "bbTypingStop",
        "bbSetModel", "bbSetModels", "bbSetScope", "bbSetScopes", "bbSetStatus",
        "bbSetTools", "bbSetToolReceipts", "bbSetProductCatalogs",
        "bbAppendToolResult", "bbAppendScreenshotArtifact",
        "bbUpdateScreenshotArtifact", "bbGetTranscript"
      ];
      const missingCallbacks = expectedCallbacks.filter((name) => typeof window[name] !== "function");

      // Verify bbGetTranscript returns [{role, text}]
      let transcriptShapeOk = false;
      try {
        const transcript = window.bbGetTranscript();
        transcriptShapeOk = Array.isArray(transcript) &&
          transcript.length >= 0 &&
          transcript.every((item) => item && typeof item === "object" && "role" in item && "text" in item);
      } catch {
        transcriptShapeOk = false;
      }

      // Verify streaming invariant: "Hello " + "world" = one "Hello world" assistant message
      let streamTest = { ok: false, resultingText: "", assistantMessageCount: 0, duplicateMessages: 0 };
      try {
        // Clean slate so the idempotent bbTypingStart creates this case's own pending record.
        window.bbReset();
        // Start streaming
        window.bbTypingStart();
        // Append two chunks
        window.bbAppendChunk("Hello ");
        window.bbAppendChunk("world");
        // Stop streaming
        window.bbTypingStop();
        // Check result
        const transcript = window.bbGetTranscript();
        const assistantMessages = (transcript || []).filter((m) => m.role === "assistant");
        const helloMessages = assistantMessages.filter((m) => m.text && m.text.includes("Hello world"));
        streamTest = {
          ok: helloMessages.length === 1 && assistantMessages.filter((m) => m.text === "Hello world").length === 1,
          resultingText: helloMessages[0]?.text ?? "",
          assistantMessageCount: assistantMessages.filter((m) => m.text === "Hello world").length,
          duplicateMessages: assistantMessages.filter((m) => m.text === "Hello world").length > 1 ? 1 : 0
        };
        // Reset to clean state
        window.bbReset();
      } catch (e) {
        streamTest = { ok: false, resultingText: String(e), assistantMessageCount: 0, duplicateMessages: 1 };
      }

      // CASE A — failure delivered as a chunk finalizes the SAME pending record
      let failureFinalize = {
        ok: false, userRecords: 0, assistantRecords: 0,
        emptyAssistantRecords: 0, duplicateAssistantRecords: 0
      };
      try {
        window.bbReset();
        window.bbAppend({ role: "user", text: "BB-PROVIDER-PROVENANCE-V3" });
        window.bbTypingStart(); // pending assistant created
        window.bbAppendChunk("[prov provider=NVIDIA model=meta/llama-3.1-70b-instruct httpStatus=401 category=auth_or_permission] Invalid credentials");
        window.bbTypingStop();  // same record becomes failed/final
        const transcript = window.bbGetTranscript();
        const userRecords = transcript.filter((m) => m.role === "user");
        const assistantRecords = transcript.filter((m) => m.role === "assistant");
        failureFinalize = {
          ok: userRecords.length === 1 &&
              assistantRecords.length === 1 &&
              assistantRecords.every((m) => (m.text ?? "").length > 0),
          userRecords: userRecords.length,
          assistantRecords: assistantRecords.length,
          emptyAssistantRecords: assistantRecords.filter((m) => !(m.text ?? "").length).length,
          duplicateAssistantRecords: Math.max(0, assistantRecords.length - 1)
        };
      } catch (e) {
        failureFinalize = { ok: false, userRecords: -1, assistantRecords: -1, emptyAssistantRecords: -1, duplicateAssistantRecords: -1, error: String(e) };
      }

      // CASE B — defensive bbAppend(assistant) while pending finalizes THAT record
      let defensiveAppendOk = false;
      try {
        window.bbReset();
        window.bbTypingStart(); // pending exists
        window.bbAppend({ role: "assistant", text: "final" });
        const transcript = window.bbGetTranscript();
        const assistantRecords = transcript.filter((m) => m.role === "assistant");
        defensiveAppendOk =
          assistantRecords.length === 1 &&
          assistantRecords[0].text === "final";
      } catch {
        defensiveAppendOk = false;
      }

      // CASE C — bbTypingStart is idempotent while a request is active
      let typingStartIdempotentOk = false;
      try {
        window.bbReset();
        window.bbTypingStart();
        const before = window.bbGetTranscript().filter((m) => m.role === "assistant").length;
        window.bbTypingStart();
        const after = window.bbGetTranscript().filter((m) => m.role === "assistant").length;
        typingStartIdempotentOk = before === 1 && after === 1;
      } catch {
        typingStartIdempotentOk = false;
      }

      // CASE D — IMMEDIATE FAILURE TRANSCRIPT READBACK: chunk + typingStop +
      // bbGetTranscript back-to-back with no timer/promise/render wait. The
      // synchronous bridge-state mirror must expose the finalized record.
      let immediateFailureReadback = {
        ok: false, userCount: 0, assistantCount: 0, assistantText: "", assistantEmpty: true
      };
      try {
        window.bbReset();
        window.bbAppend({ role: "user", text: "BB-IMMEDIATE-READBACK-D" });
        window.bbTypingStart();
        window.bbAppendChunk("Request failed");
        window.bbTypingStop();
        const transcript = window.bbGetTranscript(); // IMMEDIATE — no await
        const users = transcript.filter((m) => m.role === "user");
        const assistants = transcript.filter((m) => m.role === "assistant");
        immediateFailureReadback = {
          ok: users.length === 1 &&
              assistants.length === 1 &&
              assistants[0].text === "Request failed",
          userCount: users.length,
          assistantCount: assistants.length,
          assistantText: assistants[0]?.text ?? "",
          assistantEmpty: !(assistants[0]?.text ?? "").length
        };
      } catch (e) {
        immediateFailureReadback = {
          ok: false, userCount: -1, assistantCount: -1,
          assistantText: String(e), assistantEmpty: true
        };
      }

      // CASE E — RESET CONSISTENCY: seed, reset, immediate readback must be []
      let resetConsistency = { ok: false, transcriptLength: -1 };
      try {
        window.bbReset();
        window.bbAppend({ role: "user", text: "seed-1" });
        window.bbAppend({ role: "assistant", text: "seed-2" });
        window.bbReset();
        const transcript = window.bbGetTranscript(); // IMMEDIATE
        resetConsistency = {
          ok: Array.isArray(transcript) && transcript.length === 0,
          transcriptLength: transcript.length
        };
      } catch (e) {
        resetConsistency = { ok: false, transcriptLength: -1, error: String(e) };
      }

      // CASE F — DEFENSIVE bbAppend(assistant) with immediate readback
      let defensiveAppendImmediate = { ok: false, assistantCount: 0, assistantText: "" };
      try {
        window.bbReset();
        window.bbTypingStart();
        window.bbAppend({ role: "assistant", text: "failure" });
        const transcript = window.bbGetTranscript(); // IMMEDIATE
        const assistants = transcript.filter((m) => m.role === "assistant");
        defensiveAppendImmediate = {
          ok: assistants.length === 1 && assistants[0].text === "failure",
          assistantCount: assistants.length,
          assistantText: assistants[0]?.text ?? ""
        };
      } catch (e) {
        defensiveAppendImmediate = { ok: false, assistantCount: -1, assistantText: String(e) };
      }

      // CASE G — NORMAL STREAM "A"+"B" with immediate readback = exactly "AB"
      let normalStreamImmediate = { ok: false, assistantCount: 0, assistantText: "" };
      try {
        window.bbReset();
        window.bbTypingStart();
        window.bbAppendChunk("A");
        window.bbAppendChunk("B");
        window.bbTypingStop();
        const transcript = window.bbGetTranscript(); // IMMEDIATE
        const assistants = transcript.filter((m) => m.role === "assistant");
        normalStreamImmediate = {
          ok: assistants.length === 1 && assistants[0].text === "AB",
          assistantCount: assistants.length,
          assistantText: assistants[0]?.text ?? ""
        };
      } catch (e) {
        normalStreamImmediate = { ok: false, assistantCount: -1, assistantText: String(e) };
      }

      // CASE H — two sequential chat rounds must replay exactly four ordered
      // records. Host completion must finalize each pending assistant record,
      // never append a second echo record.
      let twoRoundReplay = { ok: false, recordCount: 0, records: [] };
      try {
        window.bbReset();
        window.bbAppend({ role: "user", text: "round-1 user" });
        window.bbTypingStart();
        window.bbAppendChunk("round-1 assistant");
        window.bbTypingStop();
        window.bbAppend({ role: "user", text: "round-2 user" });
        window.bbTypingStart();
        window.bbAppendChunk("round-2 assistant");
        window.bbTypingStop();
        const transcript = window.bbGetTranscript();
        const expected = [
          { role: "user", text: "round-1 user" },
          { role: "assistant", text: "round-1 assistant" },
          { role: "user", text: "round-2 user" },
          { role: "assistant", text: "round-2 assistant" },
        ];
        twoRoundReplay = {
          ok: JSON.stringify(transcript) === JSON.stringify(expected),
          recordCount: transcript.length,
          records: transcript,
        };
      } catch (e) {
        twoRoundReplay = { ok: false, recordCount: -1, records: [String(e)] };
      }

      // Verify bbAppend accepts host object shape {role, text, attachment}
      let appendObjectOk = false;
      try {
        window.bbAppend({ role: "assistant", text: "test append object", attachment: "file.png" });
        const transcript = window.bbGetTranscript();
        appendObjectOk = transcript.some((m) => m.text === "test append object");
        window.bbReset();
      } catch {
        appendObjectOk = false;
      }

      // Verify bbAppendChunk accepts a string
      let chunkStringOk = false;
      try {
        window.bbTypingStart();
        window.bbAppendChunk("test string chunk");
        chunkStringOk = true;
        window.bbTypingStop();
        window.bbReset();
      } catch {
        chunkStringOk = false;
      }

      // Verify bbSetModel accepts a string
      let setModelStringOk = false;
      try {
        window.bbSetModel("test-model-id");
        setModelStringOk = true;
      } catch {
        setModelStringOk = false;
      }

      // Verify bbSetModels accepts object array
      let setModelsArrayOk = false;
      try {
        window.bbSetModels([{ id: "a", displayName: "A" }]);
        setModelsArrayOk = true;
      } catch {
        setModelsArrayOk = false;
      }

      // Verify bbSetScope accepts a string
      let setScopeStringOk = false;
      try {
        window.bbSetScope("test-scope");
        setScopeStringOk = true;
      } catch {
        setScopeStringOk = false;
      }

      // Verify bbSetScopes accepts object array
      let setScopesArrayOk = false;
      try {
        window.bbSetScopes([{ id: "x", label: "X", enabled: true }]);
        setScopesArrayOk = true;
      } catch {
        setScopesArrayOk = false;
      }

      // Verify bbSetStatus accepts rich object
      let setStatusObjectOk = false;
      try {
        window.bbSetStatus({ configured: true, mode: "Lab", bridge: "test" });
        setStatusObjectOk = true;
      } catch {
        setStatusObjectOk = false;
      }

      // Verify bbSetTools accepts tool array
      let setToolsArrayOk = false;
      try {
        window.bbSetTools([{ name: "search" }]);
        setToolsArrayOk = true;
      } catch {
        setToolsArrayOk = false;
      }

      // Verify bbSetToolReceipts accepts receipt array
      let setReceiptsArrayOk = false;
      try {
        window.bbSetToolReceipts([{ receiptId: "r1", status: "done" }]);
        setReceiptsArrayOk = true;
      } catch {
        setReceiptsArrayOk = false;
      }

      // Verify bbSetProductCatalogs accepts object
      let setCatalogsObjectOk = false;
      try {
        window.bbSetProductCatalogs({ integrations: [], documents: [] });
        setCatalogsObjectOk = true;
      } catch {
        setCatalogsObjectOk = false;
      }

      // Verify bbAppendToolResult accepts flat object
      let appendToolResultOk = false;
      try {
        window.bbAppendToolResult({ label: "test", status: "done", message: "msg" });
        appendToolResultOk = true;
      } catch {
        appendToolResultOk = false;
      }

      // Verify screenshot artifact callbacks
      let screenshotArtifactOk = false;
      let screenshotUpdateOk = false;
      try {
        window.bbAppendScreenshotArtifact({
          screenshotId: "test-shot",
          fileName: "test.png",
          localOnlyCloudState: "local only"
        });
        screenshotArtifactOk = true;
      } catch {
        screenshotArtifactOk = false;
      }
      try {
        window.bbUpdateScreenshotArtifact({
          screenshotId: "test-shot",
          reviewStatus: "approved"
        });
        screenshotUpdateOk = true;
      } catch {
        screenshotUpdateOk = false;
      }

      // Clean up
      window.bbReset();

      const unnamedControls = formControls.filter((control) => {
        const element = control;
        return !element.getAttribute("aria-label") && !element.getAttribute("name") && !element.id;
      });

      return {
        clientWidth: doc.clientWidth,
        scrollWidth: doc.scrollWidth,
        bodyScrollWidth: document.body.scrollWidth,
        horizontalOverflow:
          doc.scrollWidth > doc.clientWidth + 1 ||
          document.body.scrollWidth > doc.clientWidth + 1,
        composerUsable: Boolean(composer && composer.clientWidth >= 200 && !composer.disabled),
        sendReachable: Boolean(
          send &&
          send.getBoundingClientRect().left >= 0 &&
          send.getBoundingClientRect().right <= doc.clientWidth
        ),
        receiptVisible: /Captured and stored locally|local only|local-first/i.test(receiptText),
        disabledStatesVisible: sourceChips.filter((chip) => chip.getAttribute("aria-disabled") === "true").length >= 2,
        unnamedControlCount: unnamedControls.length,
        bridgeTypes,
        bridgeMissingTypes: requiredBridgeTypes.filter((type) => !bridgeTypes.includes(type)),
        bridgeMessageCount: bridgeMessages.length,
        // New verifications
        allCallbacksInstalled: missingCallbacks.length === 0,
        missingCallbacks,
        transcriptShapeOk,
        streamOk: streamTest.ok,
        streamResultingText: streamTest.resultingText,
        streamAssistantMessageCount: streamTest.assistantMessageCount,
        streamDuplicateCount: streamTest.duplicateMessages,
        appendObjectOk,
        chunkStringOk,
        setModelStringOk,
        setModelsArrayOk,
        setScopeStringOk,
        setScopesArrayOk,
        setStatusObjectOk,
        setToolsArrayOk,
        setReceiptsArrayOk,
        setCatalogsObjectOk,
        appendToolResultOk,
        screenshotArtifactOk,
        screenshotUpdateOk,
        failureFinalizeOk: failureFinalize.ok,
        failureUserRecords: failureFinalize.userRecords,
        failureAssistantRecords: failureFinalize.assistantRecords,
        failureEmptyAssistantRecords: failureFinalize.emptyAssistantRecords,
        failureDuplicateAssistantRecords: failureFinalize.duplicateAssistantRecords,
        defensiveAppendOk,
        typingStartIdempotentOk,
        immediateFailureReadbackOk: immediateFailureReadback.ok,
        immediateFailureReadbackUserCount: immediateFailureReadback.userCount,
        immediateFailureReadbackAssistantCount: immediateFailureReadback.assistantCount,
        immediateFailureReadbackAssistantText: immediateFailureReadback.assistantText,
        resetConsistencyOk: resetConsistency.ok,
        resetConsistencyTranscriptLength: resetConsistency.transcriptLength,
        defensiveAppendImmediateOk: defensiveAppendImmediate.ok,
        defensiveAppendImmediateCount: defensiveAppendImmediate.assistantCount,
        defensiveAppendImmediateText: defensiveAppendImmediate.assistantText,
        normalStreamImmediateOk: normalStreamImmediate.ok,
        normalStreamImmediateCount: normalStreamImmediate.assistantCount,
        normalStreamImmediateText: normalStreamImmediate.assistantText,
        twoRoundReplayOk: twoRoundReplay.ok,
        twoRoundReplayRecordCount: twoRoundReplay.recordCount,
        twoRoundReplayRecords: twoRoundReplay.records,
        unexpectedBridgeTypes
      };
    });

    const screenshot = join(outputRoot, `narrow-${width}.png`);
    await page.screenshot({ path: screenshot, fullPage: true });
    rows.push({
      width,
      ...result,
      blockedExternalRequests: blockedRequests.length,
      screenshot
    });

    await page.close();
  }
} finally {
  await browser.close();
  await staticServer.close();
}

const passed = rows.every((row) =>
  !row.horizontalOverflow &&
  row.composerUsable &&
  row.sendReachable &&
  row.receiptVisible &&
  row.disabledStatesVisible &&
  row.unnamedControlCount === 0 &&
  row.bridgeMissingTypes.length === 0 &&
  row.blockedExternalRequests === 0 &&
  row.allCallbacksInstalled &&
  row.transcriptShapeOk &&
  row.streamOk &&
  row.streamDuplicateCount === 0 &&
  row.appendObjectOk &&
  row.chunkStringOk &&
  row.setModelStringOk &&
  row.setModelsArrayOk &&
  row.setScopeStringOk &&
  row.setScopesArrayOk &&
  row.setStatusObjectOk &&
  row.setToolsArrayOk &&
  row.setReceiptsArrayOk &&
  row.setCatalogsObjectOk &&
  row.appendToolResultOk &&
  row.screenshotArtifactOk &&
  row.screenshotUpdateOk &&
  row.failureFinalizeOk &&
  row.defensiveAppendOk &&
  row.typingStartIdempotentOk &&
  row.immediateFailureReadbackOk &&
  row.resetConsistencyOk &&
  row.defensiveAppendImmediateOk &&
  row.normalStreamImmediateOk &&
  row.twoRoundReplayOk &&
  row.twoRoundReplayRecordCount === 4 &&
  Array.isArray(row.unexpectedBridgeTypes) &&
  row.unexpectedBridgeTypes.length === 0
);

const summary = {
  ok: passed,
  schemaVersion: "2026-08-26.bluebrick-assistant-narrow-smoke.v4",
  distIndex: indexPath,
  outputRoot,
  safetyBoundary: {
    loadsOnlyLocalStaticShell: true,
    staticOrigin: staticServer.origin,
    launchesSolidWorks: false,
    registersAddIn: false,
    callsLiveConnectors: false,
    uploadsScreenshots: false,
    executesMutationRoutes: false
  },
  rows
};

writeFileSync(join(outputRoot, "narrow-smoke-summary.json"), JSON.stringify(summary, null, 2), "utf8");
writeFileSync(join(outputRoot, "narrow-smoke-summary.md"), renderMarkdown(summary), "utf8");
console.log(JSON.stringify(summary, null, 2));

if (!passed) process.exit(1);

async function seedShell(page) {
  await page.evaluate(() => {
    const longModel =
      "AionUI manufacturing reasoning profile with extended CAD/PDM/Epicor context";
    window.bbSetModels?.([
      {
        id: "aionui-long-model",
        displayName: longModel,
        available: true,
        supportsVision: true,
        supportsToolCalling: true,
        supportsStructuredOutput: true
      },
      {
        id: "openai-compatible-smoke",
        displayName: "OpenAI-compatible smoke profile",
        available: true,
        supportsVision: true,
        supportsToolCalling: true,
        supportsStructuredOutput: true
      }
    ]);
    window.bbSetStatus?.({
      configured: true,
      mode: "Lab",
      bridge: "local static smoke",
      activeModelDescriptor: {
        id: "aionui-long-model",
        displayName: longModel,
        supportsVision: true,
        supportsToolCalling: true,
        supportsStructuredOutput: true,
        isAvailable: true
      },
      scopeId: "local_vault",
      scopes: [
        { id: "local_vault", label: "Local Vault", enabled: true },
        { id: "pdm", label: "PDM", enabled: false, unavailableReason: "PDM read-only connector unavailable" },
        { id: "epicor", label: "Epicor", enabled: false, unavailableReason: "Epicor read-only connector unavailable" },
        { id: "all", label: "Both/All", enabled: true }
      ]
    });
    window.bbAppendScreenshotArtifact?.({
      screenshotId: "smoke-shot",
      artifactId: "smoke-artifact",
      fileName: "capture_smoke.png",
      capturedUtc: "2026-06-05T12:00:00Z",
      width: 1280,
      height: 720,
      localOnlyCloudState: "local only",
      annotations: [
        { id: "ann-1", label: "Bracket", source: "human", reviewStatus: "pending" }
      ],
      contacts: [
        { id: "contact-1", name: "Pat Engineer", email: "pat@example.invalid", reviewStatus: "pending" }
      ]
    });
  });
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
      const relative = decodeURIComponent(requestPath === "/" ? "/index.html" : requestPath);
      const target = resolve(distResolved, `.${normalize(relative)}`);
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

async function exerciseBridge(page) {
  await page.selectOption("#assistant-model", "openai-compatible-smoke");
  await page.click('[data-scope="all"]');
  await page.getByLabel("Capture local screenshot").click();
  await page.getByLabel("Message BlueBrick Assistant").fill("search released bracket drawings");
  await page.getByLabel("Search the selected scope").click();
  await page.getByLabel("Message BlueBrick Assistant").fill("Summarize this local screenshot receipt.");
  await page.getByLabel("Send message").click();
  await page.getByLabel("Stop streaming response").click();
  await page.getByText("Approve", { exact: true }).first().click();
}

function renderMarkdown(summary) {
  const lines = [
    "# BlueBrick Assistant Narrow Smoke",
    "",
    `- Dist: \`${summary.distIndex}\``,
    `- Static origin: \`${summary.safetyBoundary.staticOrigin}\``,
    `- Output: \`${summary.outputRoot}\``,
    `- Passed: ${summary.ok}`,
    "",
    "| Width | Overflow? | Composer? | Send? | Receipt? | Disabled states? | Bridge events? | External? | All callbacks? | Stream OK? | Transcript shape? |",
    "| ---: | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |"
  ];

  for (const row of summary.rows) {
    lines.push(
      `| ${row.width} | ${row.horizontalOverflow ? "yes" : "no"} | ${row.composerUsable ? "yes" : "no"} | ${row.sendReachable ? "yes" : "no"} | ${row.receiptVisible ? "yes" : "no"} | ${row.disabledStatesVisible ? "yes" : "no"} | ${row.bridgeMissingTypes.length ? `missing ${row.bridgeMissingTypes.join(", ")}` : "ok"} | ${row.blockedExternalRequests} | ${row.allCallbacksInstalled ? "yes" : `missing ${row.missingCallbacks.join(", ")}`} | ${row.streamOk ? "yes" : "no"} | ${row.transcriptShapeOk ? "yes" : "no"} |`
    );
  }

  return `${lines.join("\n")}\n`;
}
