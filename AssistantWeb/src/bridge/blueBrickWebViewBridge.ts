/**
 * BlueBrick WebView2 Bridge
 *
 * Source-grounded adapter for the legacy BlueBrick host contract.
 * Host payloads are rich JSON blobs whose exact shapes are defined in
 * AssistantPanel.cs and verified at runtime. The frontend must NOT
 * narrow them to guessed DTOs.
 *
 * Browser -> Host: 10 exact message names.
 * Host -> Browser: 17 exact callback names installed on window.bb*.
 */

// ---------------------------------------------------------------------------
// Browser -> Host (10 message names)
// ---------------------------------------------------------------------------
export type BrowserToHostMessageName =
  | "newSession"
  | "captureScreenshot"
  | "attach"
  | "search"
  | "selectModel"
  | "selectScope"
  | "sendMessage"
  | "cancelMessage"
  | "saveScreenshotAnnotation"
  | "reviewScreenshotItem";

export type PayloadFor<TName extends BrowserToHostMessageName> =
  TName extends "newSession"
    ? { type: "newSession" }
    : TName extends "captureScreenshot"
      ? { type: "captureScreenshot" }
      : TName extends "attach"
        ? { type: "attach" }
        : TName extends "search"
          ? { type: "search"; message: string; scopeId?: string }
          : TName extends "selectModel"
            ? { type: "selectModel"; modelId: string }
            : TName extends "selectScope"
              ? { type: "selectScope"; scopeId: string }
              : TName extends "sendMessage"
                ? { type: "sendMessage"; message: string; scopeId?: string; modelId?: string }
                : TName extends "cancelMessage"
                  ? { type: "cancelMessage" }
                  : TName extends "saveScreenshotAnnotation"
                    ? { type: "saveScreenshotAnnotation"; screenshotId?: string; annotation?: unknown }
                    : TName extends "reviewScreenshotItem"
                      ? {
                          type: "reviewScreenshotItem";
                          screenshotId: string;
                          targetType?: string;
                          targetId?: string;
                          reviewStatus?: string;
                          reviewNote?: string;
                        }
                      : never;

// ---------------------------------------------------------------------------
// Host -> Browser (17 callback names)
// ---------------------------------------------------------------------------
export type HostToBrowserCallbackName =
  | "bbReset"
  | "bbAppend"
  | "bbTypingStart"
  | "bbAppendChunk"
  | "bbTypingStop"
  | "bbSetModel"
  | "bbSetModels"
  | "bbSetScope"
  | "bbSetScopes"
  | "bbSetStatus"
  | "bbSetTools"
  | "bbSetToolReceipts"
  | "bbSetProductCatalogs"
  | "bbAppendToolResult"
  | "bbAppendScreenshotArtifact"
  | "bbUpdateScreenshotArtifact"
  | "bbGetTranscript";

// ---------------------------------------------------------------------------
// Bridge interface
// ---------------------------------------------------------------------------
export interface BlueBrickBridge {
  post<TName extends BrowserToHostMessageName>(
    name: TName,
    payload: PayloadFor<TName>,
  ): void;
  isHostAvailable(): boolean;
}

// ---------------------------------------------------------------------------
// Host availability detection
// ---------------------------------------------------------------------------
declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void;
      };
    };
  }
}

function detectHostTransport(): "webview2" | "none" {
  if (typeof window !== "undefined" && window.chrome?.webview?.postMessage) {
    return "webview2";
  }
  return "none";
}

// ---------------------------------------------------------------------------
// Bridge handlers — one per host->browser callback
// ---------------------------------------------------------------------------
export interface BlueBrickBridgeHandlers {
  onReset: () => void;
  onAppend: (payload: {
    role: "user" | "assistant" | string;
    text: string;
    attachment?: string;
  }) => void;
  onTypingStart: () => void;
  onAppendChunk: (text: string) => void;
  onTypingStop: () => void;
  onSetModel: (model: unknown) => void;
  onSetModels: (models: unknown[]) => void;
  onSetScope: (scopeId: unknown) => void;
  onSetScopes: (scopes: unknown[]) => void;
  onSetStatus: (status: unknown) => void;
  onSetTools: (tools: unknown[]) => void;
  onSetToolReceipts: (receipts: unknown[]) => void;
  onSetProductCatalogs: (catalogs: unknown) => void;
  onAppendToolResult: (result: unknown) => void;
  onAppendScreenshotArtifact: (artifact: unknown) => void;
  onUpdateScreenshotArtifact: (update: unknown) => void;
  onGetTranscript: () => Array<{ role: string; text: string }>;
}

// ---------------------------------------------------------------------------
// Factory: createBlueBrickWindowBridge
// ---------------------------------------------------------------------------
export function createBlueBrickWindowBridge(
  handlers: BlueBrickBridgeHandlers,
): BlueBrickBridge {
  if (typeof window === "undefined") {
    return {
      post: () => {},
      isHostAvailable: () => false,
    };
  }

  const w = window as unknown as Record<string, unknown>;

  w.bbReset = handlers.onReset;
  w.bbAppend = handlers.onAppend;
  w.bbTypingStart = handlers.onTypingStart;
  w.bbAppendChunk = handlers.onAppendChunk;
  w.bbTypingStop = handlers.onTypingStop;
  w.bbSetModel = handlers.onSetModel;
  w.bbSetModels = handlers.onSetModels;
  w.bbSetScope = handlers.onSetScope;
  w.bbSetScopes = handlers.onSetScopes;
  w.bbSetStatus = handlers.onSetStatus;
  w.bbSetTools = handlers.onSetTools;
  w.bbSetToolReceipts = handlers.onSetToolReceipts;
  w.bbSetProductCatalogs = handlers.onSetProductCatalogs;
  w.bbAppendToolResult = handlers.onAppendToolResult;
  w.bbAppendScreenshotArtifact = handlers.onAppendScreenshotArtifact;
  w.bbUpdateScreenshotArtifact = handlers.onUpdateScreenshotArtifact;
  w.bbGetTranscript = handlers.onGetTranscript;

  const transport = detectHostTransport();

  const post = <TName extends BrowserToHostMessageName>(
    _name: TName,
    payload: PayloadFor<TName>,
  ): void => {
    if (transport === "webview2" && window.chrome?.webview?.postMessage) {
      try {
        window.chrome.webview.postMessage(payload);
      } catch {
        /* host transport errors are swallowed by policy */
      }
    }
  };

  return {
    post,
    isHostAvailable: () => transport !== "none",
  };
}

// ---------------------------------------------------------------------------
// Convenience helpers (thin wrappers for the 10 browser->host messages)
// ---------------------------------------------------------------------------
export const sendMessage = (
  bridge: BlueBrickBridge,
  message: string,
  scopeId?: string,
  modelId?: string,
): void => bridge.post("sendMessage", { type: "sendMessage", message, scopeId, modelId });

export const cancelMessage = (bridge: BlueBrickBridge): void =>
  bridge.post("cancelMessage", { type: "cancelMessage" });

export const selectModel = (bridge: BlueBrickBridge, modelId: string): void =>
  bridge.post("selectModel", { type: "selectModel", modelId });

export const selectScope = (bridge: BlueBrickBridge, scopeId: string): void =>
  bridge.post("selectScope", { type: "selectScope", scopeId });

export const newSession = (bridge: BlueBrickBridge): void =>
  bridge.post("newSession", { type: "newSession" });

export const captureScreenshot = (bridge: BlueBrickBridge): void =>
  bridge.post("captureScreenshot", { type: "captureScreenshot" });

export const attach = (bridge: BlueBrickBridge): void =>
  bridge.post("attach", { type: "attach" });

export const search = (bridge: BlueBrickBridge, message: string, scopeId?: string): void =>
  bridge.post("search", { type: "search", message, scopeId });

export const saveScreenshotAnnotation = (bridge: BlueBrickBridge): void =>
  bridge.post("saveScreenshotAnnotation", { type: "saveScreenshotAnnotation" });

export const reviewScreenshotItem = (
  bridge: BlueBrickBridge,
  screenshotId: string,
  targetType?: string,
  targetId?: string,
  reviewStatus?: string,
  reviewNote?: string,
): void =>
  bridge.post("reviewScreenshotItem", {
    type: "reviewScreenshotItem",
    screenshotId,
    targetType,
    targetId,
    reviewStatus,
    reviewNote,
  });

export {};
