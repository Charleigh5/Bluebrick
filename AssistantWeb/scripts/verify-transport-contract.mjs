import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { pathToFileURL, fileURLToPath } from "node:url";
import ts from "typescript";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const sourcePath = join(root, "src", "bluebrickTransport.ts");
const source = readFileSync(sourcePath, "utf8");
const bridgePath = join(root, "src", "bridge", "blueBrickWebViewBridge.ts");
const bridgeSource = readFileSync(bridgePath, "utf8");

const forbidden = [
  "X-Agent" + "-Auth",
  ".agent" + "_token",
  "OPENAI" + "_API_KEY",
  "NVIDIA" + "_API_KEY",
  "ANTHROPIC" + "_API_KEY",
  "GEMINI" + "_API_KEY",
  "SALESFORCE" + "_ACCESS_TOKEN",
  "SALESFORCE" + "_REFRESH_TOKEN",
  "DATABASE" + "_URL",
  "Authorization" + ":",
  "Bearer" + " "
];

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function assertEqual(actual, expected, message) {
  if (actual !== expected) {
    throw new Error(`${message} Expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}.`);
  }
}

function assertDeepIncludes(actual, expected, message) {
  for (const [key, value] of Object.entries(expected)) {
    assertEqual(actual?.[key], value, `${message} field ${key} mismatch.`);
  }
}

for (const token of forbidden) {
  assert(!source.includes(token), `Transport source must not expose forbidden token identifier: ${token}`);
}

const expectedHostCallbacks = [
  "bbReset", "bbAppend", "bbTypingStart", "bbAppendChunk", "bbTypingStop",
  "bbSetModel", "bbSetModels", "bbSetScope", "bbSetScopes", "bbSetStatus",
  "bbSetTools", "bbSetToolReceipts", "bbSetProductCatalogs",
  "bbAppendToolResult", "bbAppendScreenshotArtifact",
  "bbUpdateScreenshotArtifact", "bbGetTranscript",
];

const declaredHostCallbacks = [...bridgeSource.matchAll(/^\s*\|\s+"(bb[A-Za-z]+)";?$/gm)]
  .map((match) => match[1]);
assertEqual(declaredHostCallbacks.length, 17, "Bridge must declare exactly 17 host callback names.");
assertEqual(
  JSON.stringify(declaredHostCallbacks),
  JSON.stringify(expectedHostCallbacks),
  "Bridge callback manifest must remain exact and ordered.",
);
for (const callbackName of expectedHostCallbacks) {
  const assignmentCount = (bridgeSource.match(new RegExp(`\\bw\\.${callbackName}\\s*=`, "g")) ?? []).length;
  assertEqual(assignmentCount, 1, `Bridge must register ${callbackName} exactly once.`);
}

const transpiled = ts.transpileModule(source, {
  fileName: sourcePath,
  compilerOptions: {
    target: ts.ScriptTarget.ES2022,
    module: ts.ModuleKind.ES2022,
    moduleResolution: ts.ModuleResolutionKind.Bundler,
    jsx: ts.JsxEmit.ReactJSX,
    importsNotUsedAsValues: ts.ImportsNotUsedAsValues.Remove
  },
  reportDiagnostics: true
});

const diagnostics = (transpiled.diagnostics || []).filter((diagnostic) => diagnostic.category === ts.DiagnosticCategory.Error);
assert(diagnostics.length === 0, diagnostics.map((diagnostic) => ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n")).join("\n"));

const tempRoot = join(tmpdir(), `bluebrick-transport-${Date.now()}-${Math.random().toString(36).slice(2)}`);
mkdirSync(tempRoot, { recursive: true });
const modulePath = join(tempRoot, "bluebrickTransport.mjs");
writeFileSync(modulePath, transpiled.outputText, "utf8");

try {
  const mod = await import(pathToFileURL(modulePath).href);

  const payload = mod.buildBlueBrickMessagePayload(
    {
      baseUrl: "http://127.0.0.1:17177/",
      sessionId: "session-1",
      modelId: "openai-compatible",
      scopeId: "all",
      uploadConsentApproved: false
    },
    { id: "message-1", role: "user", content: "Find bracket contacts" },
    [{ artifactId: "artifact-1", screenshotId: "shot-1", localOnly: true }]
  );

  assertDeepIncludes(payload, {
    sessionId: "session-1",
    modelId: "openai-compatible",
    scopeId: "all",
    message: "Find bracket contacts",
    uploadConsentApproved: false
  }, "Payload");
  assert(Array.isArray(payload.attachmentPaths), "Payload must include attachmentPaths array.");
  assertEqual(payload.attachmentPaths.length, 0, "Transport must not send arbitrary client file paths.");
  assertEqual(payload.attachments[0].artifactId, "artifact-1", "Transport must send attachment artifact ids.");
  assertEqual(payload.attachments[0].localOnly, true, "Transport must preserve local-only attachment state.");

  assertEqual(
    mod.buildBlueBrickStreamUrl({ baseUrl: "http://127.0.0.1:17177///", scopeId: "local_vault" }),
    "http://127.0.0.1:17177/assistant/message/stream",
    "Stream URL must normalize trailing slashes"
  );

  assertEqual(mod.parseBlueBrickSseLine("data: [DONE]"), null, "DONE sentinel must return null.");
  const malformed = mod.parseBlueBrickSseLine("data: {not-json");
  assertEqual(malformed.type, "error", "Malformed SSE must produce an error raw event.");
  assertEqual(malformed.errorCode, "malformed_sse", "Malformed SSE must identify recoverable error code.");

  assertDeepIncludes(
    mod.mapBlueBrickStreamEvent({ type: "tool_call", toolName: "search_local_vault", toolCallId: "tool-1", toolArguments: "{\"query\":\"abc\"}" }),
    { kind: "tool-call-start", toolName: "search_local_vault", toolCallId: "tool-1", argumentsJson: "{\"query\":\"abc\"}" },
    "tool_call mapping"
  );
  assertDeepIncludes(
    mod.mapBlueBrickStreamEvent({ type: "tool_call_start", toolName: "search_pdm", toolCallId: "tool-2" }),
    { kind: "tool-call-start", toolName: "search_pdm", toolCallId: "tool-2" },
    "tool_call_start mapping"
  );
  assertDeepIncludes(
    mod.mapBlueBrickStreamEvent({ type: "tool_result", toolCallId: "tool-1", toolResultContent: "done" }),
    { kind: "tool-call-result", toolCallId: "tool-1", content: "done" },
    "tool_result mapping"
  );
  assertDeepIncludes(
    mod.mapBlueBrickStreamEvent({ type: "screenshot_receipt", screenshotId: "shot-1", artifactId: "artifact-1", receipt: { localOnlyCloudState: "local only" } }),
    { kind: "screenshot-receipt", screenshotId: "shot-1", artifactId: "artifact-1" },
    "screenshot_receipt mapping"
  );

  const state = { responseId: "response-1", textStarted: false };
  const textChunks = mod.mapBlueBrickEventToUiMessageChunks({ kind: "message-delta", text: "Hello" }, state);
  assertEqual(textChunks[0].type, "text-start", "First text delta must open an AI SDK text part.");
  assertEqual(textChunks[1].type, "text-delta", "Text delta must produce a text-delta chunk.");
  assertEqual(textChunks[1].delta, "Hello", "Text delta content must be preserved.");
  const receiptChunks = mod.mapBlueBrickEventToUiMessageChunks({ kind: "screenshot-receipt", screenshotId: "shot-1", artifactId: "artifact-1" }, state);
  assertEqual(receiptChunks[0].type, "data-screenshot-receipt", "Screenshot receipts must map to reviewable data chunks.");

  const encoder = new TextEncoder();
  const events = [];
  let capturedRequest;
  await mod.streamBlueBrickMessage(
    { baseUrl: "http://127.0.0.1:17177", sessionId: "session-stream", modelId: "model-stream", scopeId: "local_vault", uploadConsentApproved: false },
    { id: "message-stream", role: "user", content: "Stream this" },
    {
      attachments: [{ artifactId: "artifact-stream", localOnly: true }],
      onEvent: (event) => events.push(event),
      fetchImpl: async (url, init) => {
        capturedRequest = { url, init };
        return {
          ok: true,
          body: new ReadableStream({
            start(controller) {
              controller.enqueue(encoder.encode('data: {"type":"text_delta","text":"Hi"}\n'));
              controller.enqueue(encoder.encode('data: {"type":"screenshot_receipt","screenshotId":"shot-stream","artifactId":"artifact-stream"}\n'));
              controller.enqueue(encoder.encode("data: {malformed\n"));
              controller.enqueue(encoder.encode('data: {"type":"done"}\n'));
              controller.close();
            }
          })
        };
      }
    }
  );

  assertEqual(capturedRequest.url, "http://127.0.0.1:17177/assistant/message/stream", "Stream fetch URL mismatch.");
  const capturedBody = JSON.parse(capturedRequest.init.body);
  assertEqual(capturedBody.modelId, "model-stream", "Stream payload must preserve model id.");
  assertEqual(capturedBody.scopeId, "local_vault", "Stream payload must preserve scope id.");
  assertEqual(capturedBody.attachmentPaths.length, 0, "Stream payload must not include arbitrary file paths.");
  assert(events.some((event) => event.kind === "message-delta" && event.text === "Hi"), "Stream must emit text delta.");
  assert(events.some((event) => event.kind === "screenshot-receipt" && event.screenshotId === "shot-stream"), "Stream must emit screenshot receipt.");
  assert(events.some((event) => event.kind === "error" && event.code === "malformed_sse"), "Stream must surface malformed SSE as recoverable error.");
  assert(events.some((event) => event.kind === "done"), "Stream must emit done.");

  const posted = [];
  globalThis.window = {};
  const transport = mod.createBlueBrickChatTransport({
    baseUrl: "http://127.0.0.1:17177",
    sessionId: "session-host",
    scopeId: "local_vault",
    getModelId: () => "model-host",
    getScopeId: () => "all",
    getAttachments: () => [{ artifactId: "artifact-host", localOnly: true }],
    createRequestId: () => "request-host",
    createResponseId: () => "response-host",
    requestTimeoutMs: 0,
    postMessage: (message) => posted.push(message)
  });

  const stream = await transport.sendMessages({
    chatId: "chat-host",
    messageId: "message-host",
    messages: [{ id: "user-host", role: "user", content: "Host stream" }],
    abortSignal: new AbortController().signal
  });
  const reader = stream.getReader();
  const hostChunks = [];
  hostChunks.push((await reader.read()).value);
  hostChunks.push((await reader.read()).value);

  assertEqual(posted.length, 1, "Host transport must post one sdkSendMessage.");
  assertEqual(posted[0].type, "sdkSendMessage", "Host transport message type mismatch.");
  assertEqual(posted[0].requestId, "request-host", "Host transport request id mismatch.");
  assertEqual(posted[0].payload.modelId, "model-host", "Host transport payload must preserve model id.");
  assertEqual(posted[0].payload.scopeId, "all", "Host transport payload must preserve scope id.");
  assertEqual(posted[0].payload.attachments[0].artifactId, "artifact-host", "Host transport payload must preserve attachment artifact id.");

  mod.acceptBlueBrickHostStreamEvent({ requestId: "request-host", event: { type: "text_delta", text: "Host ok" } });
  hostChunks.push((await reader.read()).value);
  hostChunks.push((await reader.read()).value);
  mod.acceptBlueBrickHostStreamEvent({ requestId: "request-host", event: { type: "tool_result", toolCallId: "tool-host", toolResultContent: "complete" } });
  hostChunks.push((await reader.read()).value);
  mod.acceptBlueBrickHostStreamEvent({ requestId: "request-host", done: true });
  while (true) {
    const next = await reader.read();
    if (next.done) break;
    hostChunks.push(next.value);
  }

  assert(hostChunks.some((chunk) => chunk?.type === "start"), "Host transport must emit start chunk.");
  assert(hostChunks.some((chunk) => chunk?.type === "text-delta" && chunk.delta === "Host ok"), "Host transport must emit text delta chunk.");
  assert(hostChunks.some((chunk) => chunk?.type === "tool-output-available" && chunk.output === "complete"), "Host transport must emit tool result chunk.");
  assert(hostChunks.some((chunk) => chunk?.type === "finish"), "Host transport must emit finish chunk.");

  console.log(JSON.stringify({
    ok: true,
    checked: [
      "payload",
      "url",
      "malformed_sse",
      "event_mapping",
      "ui_message_chunks",
      "streamBlueBrickMessage",
      "host_mediated_transport",
      "no_token_identifiers",
      "exact_17_host_callbacks_registered_once"
    ],
    safetyBoundary: {
      writesRepoDist: false,
      installsDependencies: false,
      launchesSolidWorks: false,
      callsLiveConnectors: false,
      sendsScreenshotsExternally: false
    }
  }, null, 2));
} finally {
  rmSync(tempRoot, { recursive: true, force: true });
}
