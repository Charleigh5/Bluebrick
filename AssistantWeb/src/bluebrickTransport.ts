import type { ChatTransport, UIMessage, UIMessageChunk } from "ai";

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void;
      };
    };
    bbSdkStreamEvent?: (envelope: BlueBrickHostStreamEnvelope) => void;
  }
}

export type BlueBrickScopeId = "local_vault" | "pdm" | "epicor" | "all" | string;

export type BlueBrickChatContext = {
  baseUrl: string;
  sessionId?: string;
  modelId?: string;
  scopeId: BlueBrickScopeId;
  uploadConsentApproved?: boolean;
};

export type BlueBrickAttachmentRef = {
  artifactId: string;
  screenshotId?: string;
  localOnly?: boolean;
};

export type BlueBrickChatMessage = {
  id?: string;
  role: "user" | "assistant" | "system";
  content: string;
};

export type BlueBrickMessagePayload = {
  sessionId?: string;
  modelId?: string;
  scopeId: BlueBrickScopeId;
  message: string;
  attachmentPaths: string[];
  attachments: BlueBrickAttachmentRef[];
  uploadConsentApproved: boolean;
};

export type BlueBrickUiMessageLike = {
  id?: string;
  role?: string;
  content?: string;
  parts?: Array<{
    type?: string;
    text?: string;
    content?: string;
  }>;
};

export type BlueBrickAiSdkPrepareRequestOptions = {
  id?: string;
  messages?: BlueBrickUiMessageLike[];
  message?: BlueBrickUiMessageLike;
  body?: Record<string, unknown>;
};

export type BlueBrickAiSdkTransportContext = BlueBrickChatContext & {
  getSessionId?: () => string | undefined;
  getModelId?: () => string | undefined;
  getScopeId?: () => BlueBrickScopeId | undefined;
  getUploadConsentApproved?: () => boolean;
  getAttachments?: () => BlueBrickAttachmentRef[];
};

export type BlueBrickAiSdkTransportConfig = {
  api: string;
  prepareSendMessagesRequest: (options: BlueBrickAiSdkPrepareRequestOptions) => {
    body: BlueBrickMessagePayload;
    headers: { "Content-Type": "application/json" };
  };
};

export type BlueBrickChatTransportOptions = BlueBrickAiSdkTransportContext & {
  postMessage?: (message: unknown) => void;
  requestTimeoutMs?: number;
  createRequestId?: () => string;
  createResponseId?: () => string;
};

export type BlueBrickHostStreamEnvelope = {
  requestId?: string;
  event?: BlueBrickRawStreamEvent | BlueBrickUiStreamEvent;
  done?: boolean;
  error?: {
    code?: string;
    message?: string;
  };
};

type BlueBrickHostStreamRequest = {
  controller: ReadableStreamDefaultController<UIMessageChunk>;
  chunkState: {
    responseId: string;
    textStarted: boolean;
  };
  timeoutId?: ReturnType<typeof setTimeout>;
};

const hostStreamRequests = new Map<string, BlueBrickHostStreamRequest>();

export type BlueBrickRawStreamEvent = {
  type?: string;
  text?: string;
  toolName?: string;
  toolCallId?: string;
  toolArguments?: string;
  toolResultContent?: string;
  screenshotId?: string;
  artifactId?: string;
  receipt?: unknown;
  artifact?: unknown;
  errorCode?: string;
  errorMessage?: string;
  done?: boolean;
  traceId?: string;
};

export type BlueBrickUiStreamEvent =
  | { kind: "message-delta"; text: string; traceId?: string }
  | { kind: "tool-call-start"; toolName: string; toolCallId?: string; argumentsJson?: string; traceId?: string }
  | { kind: "tool-call-result"; toolCallId?: string; content: string; traceId?: string }
  | { kind: "screenshot-receipt"; screenshotId?: string; artifactId?: string; receipt?: unknown; artifact?: unknown; traceId?: string }
  | { kind: "error"; code?: string; message: string; traceId?: string }
  | { kind: "done"; traceId?: string }
  | { kind: "unknown"; raw: BlueBrickRawStreamEvent };

export function buildBlueBrickMessagePayload(
  context: BlueBrickChatContext,
  message: BlueBrickChatMessage,
  attachments: BlueBrickAttachmentRef[] = []
): BlueBrickMessagePayload {
  return {
    sessionId: context.sessionId || undefined,
    modelId: context.modelId || undefined,
    scopeId: context.scopeId || "local_vault",
    message: message.content || "",
    attachmentPaths: [],
    attachments,
    uploadConsentApproved: context.uploadConsentApproved === true
  };
}

export function buildBlueBrickStreamUrl(context: BlueBrickChatContext): string {
  return `${context.baseUrl.replace(/\/+$/, "")}/assistant/message/stream`;
}

export function parseBlueBrickSseLine(line: string): BlueBrickRawStreamEvent | null {
  const trimmed = (line || "").trim();
  if (!trimmed || trimmed === "data: [DONE]" || trimmed === "[DONE]") return null;

  const json = trimmed.startsWith("data:") ? trimmed.slice(5).trim() : trimmed;
  if (!json || json === "[DONE]") return null;
  try {
    return JSON.parse(json) as BlueBrickRawStreamEvent;
  } catch {
    return {
      type: "error",
      errorCode: "malformed_sse",
      errorMessage: "Assistant stream returned a malformed event."
    };
  }
}

export function mapBlueBrickStreamEvent(raw: BlueBrickRawStreamEvent): BlueBrickUiStreamEvent {
  switch ((raw.type || "").toLowerCase()) {
    case "text_delta":
      return { kind: "message-delta", text: raw.text || "", traceId: raw.traceId };
    case "tool_call":
    case "tool_call_start":
      return {
        kind: "tool-call-start",
        toolName: raw.toolName || "tool",
        toolCallId: raw.toolCallId,
        argumentsJson: raw.toolArguments,
        traceId: raw.traceId
      };
    case "tool_result":
      return {
        kind: "tool-call-result",
        toolCallId: raw.toolCallId,
        content: raw.toolResultContent || "",
        traceId: raw.traceId
      };
    case "screenshot_receipt":
      return {
        kind: "screenshot-receipt",
        screenshotId: raw.screenshotId,
        artifactId: raw.artifactId,
        receipt: raw.receipt,
        artifact: raw.artifact,
        traceId: raw.traceId
      };
    case "error":
      return {
        kind: "error",
        code: raw.errorCode,
        message: raw.errorMessage || "Assistant stream failed.",
        traceId: raw.traceId
      };
    case "done":
      return { kind: "done", traceId: raw.traceId };
    default:
      return { kind: "unknown", raw };
  }
}

export function latestTextFromUiMessage(message?: BlueBrickUiMessageLike): string {
  if (!message) return "";
  if (typeof message.content === "string") return message.content;

  const textParts = (message.parts || [])
    .filter((part) => (part.type || "").toLowerCase().includes("text"))
    .map((part) => part.text || part.content || "")
    .filter((text) => text.length > 0);

  return textParts.join("\n");
}

export function latestUserMessage(messages: BlueBrickUiMessageLike[] = []): BlueBrickUiMessageLike | undefined {
  for (let index = messages.length - 1; index >= 0; index -= 1) {
    if ((messages[index].role || "").toLowerCase() === "user") return messages[index];
  }

  return messages.length > 0 ? messages[messages.length - 1] : undefined;
}

export function resolveBlueBrickContext(context: BlueBrickAiSdkTransportContext): BlueBrickChatContext {
  return {
    baseUrl: context.baseUrl,
    sessionId: context.getSessionId ? context.getSessionId() : context.sessionId,
    modelId: context.getModelId ? context.getModelId() : context.modelId,
    scopeId: (context.getScopeId ? context.getScopeId() : context.scopeId) || "local_vault",
    uploadConsentApproved: context.getUploadConsentApproved
      ? context.getUploadConsentApproved() === true
      : context.uploadConsentApproved === true
  };
}

export function createBlueBrickAiSdkTransportConfig(context: BlueBrickAiSdkTransportContext): BlueBrickAiSdkTransportConfig {
  // Source-only staging contract for AI SDK v6 DefaultChatTransport/useChat.
  // Provider keys, tool policy, model routing, connector policy, and screenshot upload authority stay in C#.
  return {
    api: buildBlueBrickStreamUrl(context),
    prepareSendMessagesRequest: (options) => {
      const resolvedContext = resolveBlueBrickContext(context);
      const selectedMessage = options.message || latestUserMessage(options.messages || []);
      const attachments = context.getAttachments ? context.getAttachments() : [];
      return {
        body: buildBlueBrickMessagePayload(
          resolvedContext,
          {
            id: selectedMessage ? selectedMessage.id : options.id,
            role: "user",
            content: latestTextFromUiMessage(selectedMessage)
          },
          attachments
        ),
        headers: { "Content-Type": "application/json" }
      };
    }
  };
}

function parseArgumentsJson(argumentsJson?: string): unknown {
  if (!argumentsJson) return {};
  try {
    return JSON.parse(argumentsJson);
  } catch {
    return { raw: argumentsJson };
  }
}

function createDefaultResponseId(): string {
  return `bluebrick-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}

function createDefaultRequestId(): string {
  return `bb-sdk-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}

function latestBlueBrickUserMessage(messages: BlueBrickUiMessageLike[] = [], fallbackId?: string): BlueBrickChatMessage {
  const selected = latestUserMessage(messages);
  return {
    id: selected?.id || fallbackId,
    role: "user",
    content: latestTextFromUiMessage(selected)
  };
}

export function mapBlueBrickEventToUiMessageChunks(
  event: BlueBrickUiStreamEvent,
  state: {
    responseId: string;
    textStarted: boolean;
  }
): UIMessageChunk[] {
  switch (event.kind) {
    case "message-delta": {
      const chunks: UIMessageChunk[] = [];
      if (!state.textStarted) {
        state.textStarted = true;
        chunks.push({ type: "text-start", id: state.responseId });
      }
      chunks.push({ type: "text-delta", id: state.responseId, delta: event.text });
      return chunks;
    }
    case "tool-call-start":
      return [
        {
          type: "tool-input-start",
          toolCallId: event.toolCallId || `${event.toolName}-${state.responseId}`,
          toolName: event.toolName
        },
        {
          type: "tool-input-available",
          toolCallId: event.toolCallId || `${event.toolName}-${state.responseId}`,
          toolName: event.toolName,
          input: parseArgumentsJson(event.argumentsJson)
        }
      ];
    case "tool-call-result":
      return [
        {
          type: "tool-output-available",
          toolCallId: event.toolCallId || `tool-${state.responseId}`,
          output: event.content
        }
      ];
    case "screenshot-receipt":
      return [
        {
          type: "data-screenshot-receipt",
          data: {
            screenshotId: event.screenshotId,
            artifactId: event.artifactId,
            receipt: event.receipt,
            artifact: event.artifact,
            traceId: event.traceId
          }
        } as UIMessageChunk
      ];
    case "error":
      return [{ type: "error", errorText: event.message }];
    case "done": {
      const chunks: UIMessageChunk[] = [];
      if (state.textStarted) chunks.push({ type: "text-end", id: state.responseId });
      chunks.push({ type: "finish-step" });
      chunks.push({ type: "finish", finishReason: "stop" });
      return chunks;
    }
    default:
      return [];
  }
}

export function createBlueBrickChatTransport(options: BlueBrickChatTransportOptions): ChatTransport<UIMessage> {
  return {
    sendMessages: async ({ chatId, messageId, messages, abortSignal }) => {
      const resolvedContext = resolveBlueBrickContext({
        ...options,
        getSessionId: options.getSessionId || (() => chatId || options.sessionId)
      });
      const requestId = (options.createRequestId || createDefaultRequestId)();
      const responseId = (options.createResponseId || createDefaultResponseId)();
      const chunkState = { responseId, textStarted: false };
      const userMessage = latestBlueBrickUserMessage(messages as BlueBrickUiMessageLike[], messageId);
      const attachments = options.getAttachments ? options.getAttachments() : [];
      const postMessage = options.postMessage || ((message: unknown) => window.chrome?.webview?.postMessage(message));
      const payload = buildBlueBrickMessagePayload(resolvedContext, userMessage, attachments);

      return new ReadableStream<UIMessageChunk>({
        start(controller) {
          controller.enqueue({ type: "start", messageId: responseId });
          controller.enqueue({ type: "start-step" });

          try {
            installBlueBrickHostTransportBridge();
            const timeoutMs = Math.max(0, options.requestTimeoutMs ?? 120000);
            const timeoutId = timeoutMs
              ? setTimeout(() => {
                  acceptBlueBrickHostStreamEvent({
                    requestId,
                    error: {
                      code: "host_timeout",
                      message: "BlueBrick host stream timed out."
                    }
                  });
                }, timeoutMs)
              : undefined;
            hostStreamRequests.set(requestId, { controller, chunkState, timeoutId });
            postMessage({
              type: "sdkSendMessage",
              requestId,
              payload
            });
          } catch (error) {
            if (abortSignal?.aborted) {
              controller.enqueue({ type: "abort", reason: "aborted" });
            } else {
              controller.enqueue({
                type: "error",
                errorText: error instanceof Error ? error.message : "BlueBrick assistant stream failed."
              });
            }
            controller.close();
          }
        },
        cancel() {
          const pending = hostStreamRequests.get(requestId);
          if (pending?.timeoutId) clearTimeout(pending.timeoutId);
          hostStreamRequests.delete(requestId);
          try {
            postMessage({ type: "sdkCancelMessage", requestId });
          } catch {
            // Host bridge is optional during static browser smoke.
          }
        }
      });
    },
    reconnectToStream: async () => null
  };
}

export function acceptBlueBrickHostStreamEvent(envelope: BlueBrickHostStreamEnvelope): void {
  const requestId = envelope.requestId || "";
  const pending = hostStreamRequests.get(requestId);
  if (!pending) return;

  const close = () => {
    if (pending.timeoutId) clearTimeout(pending.timeoutId);
    hostStreamRequests.delete(requestId);
    pending.controller.close();
  };

  if (envelope.error) {
    pending.controller.enqueue({
      type: "error",
      errorText: envelope.error.message || "BlueBrick host stream failed."
    });
    close();
    return;
  }

  if (envelope.event) {
    const event =
      "kind" in envelope.event
        ? (envelope.event as BlueBrickUiStreamEvent)
        : mapBlueBrickStreamEvent(envelope.event as BlueBrickRawStreamEvent);
    for (const chunk of mapBlueBrickEventToUiMessageChunks(event, pending.chunkState)) {
      pending.controller.enqueue(chunk);
    }
  }

  if (envelope.done) {
    for (const chunk of mapBlueBrickEventToUiMessageChunks({ kind: "done" }, pending.chunkState)) {
      pending.controller.enqueue(chunk);
    }
    close();
  }
}

export function installBlueBrickHostTransportBridge(target: Window = window): void {
  target.bbSdkStreamEvent = acceptBlueBrickHostStreamEvent;
}

export async function streamBlueBrickMessage(
  context: BlueBrickChatContext,
  message: BlueBrickChatMessage,
  handlers: {
    onEvent: (event: BlueBrickUiStreamEvent) => void;
    fetchImpl?: typeof fetch;
    signal?: AbortSignal;
    attachments?: BlueBrickAttachmentRef[];
  }
): Promise<void> {
  const fetchImpl = handlers.fetchImpl || fetch;
  const response = await fetchImpl(buildBlueBrickStreamUrl(context), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(buildBlueBrickMessagePayload(context, message, handlers.attachments || [])),
    signal: handlers.signal
  });

  if (!response.ok) {
    handlers.onEvent({ kind: "error", code: "http_error", message: `Assistant stream returned ${response.status}.` });
    return;
  }

  if (!response.body) {
    handlers.onEvent({ kind: "error", code: "missing_body", message: "Assistant stream response did not include a body." });
    return;
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let pending = "";

  while (true) {
    const { value, done } = await reader.read();
    if (done) break;

    pending += decoder.decode(value, { stream: true });
    const lines = pending.split(/\r?\n/);
    pending = lines.pop() || "";

    for (const line of lines) {
      const parsed = parseBlueBrickSseLine(line);
      if (parsed) handlers.onEvent(mapBlueBrickStreamEvent(parsed));
    }
  }

  if (pending.trim()) {
    const parsed = parseBlueBrickSseLine(pending);
    if (parsed) handlers.onEvent(mapBlueBrickStreamEvent(parsed));
  }
}
