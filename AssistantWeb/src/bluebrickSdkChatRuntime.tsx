import { useCallback, useMemo } from "react";
import { useChat } from "@ai-sdk/react";
import type { ChatStatus, UIMessage } from "ai";
import {
  type BlueBrickAttachmentRef,
  type BlueBrickScopeId,
  createBlueBrickChatTransport
} from "./bluebrickTransport";

export type BlueBrickSdkFeatureFlags = Record<string, unknown>;

export type BlueBrickSdkChatRuntimeOptions = {
  enabled: boolean;
  baseUrl: string;
  sessionId?: string;
  modelId?: string;
  scopeId?: BlueBrickScopeId;
  uploadConsentApproved?: boolean;
  throttleMs?: number;
  attachments?: BlueBrickAttachmentRef[];
  postMessage?: (message: unknown) => void;
  onFallbackMessage?: (message: string) => void;
};

export type BlueBrickSdkChatRuntime = {
  enabled: boolean;
  messages: UIMessage[];
  status: ChatStatus;
  error: Error | undefined;
  sendMessage: (message: string) => void;
  stop: () => void;
  clearError: () => void;
};

export function isBlueBrickSdkRuntimeEnabled(flags: BlueBrickSdkFeatureFlags | undefined): boolean {
  if (!flags) return false;
  return flags["Assistant.UseSdkChat"] === true || flags["assistant.useSdkChat"] === true;
}

export function getBlueBrickUiMessageText(message: UIMessage): string {
  return message.parts
    .map((part) => {
      const item = part as {
        type?: string;
        text?: unknown;
        data?: unknown;
        toolName?: unknown;
        output?: unknown;
        errorText?: unknown;
      };
      if (item.type === "text" && typeof item.text === "string") return item.text;
      if (item.type === "data-screenshot-receipt") return "[screenshot receipt]";
      if ((item.type || "").startsWith("tool-")) return `[${String(item.toolName || "tool")}]`;
      if (item.type === "error") return String(item.errorText || "Assistant stream error.");
      return "";
    })
    .filter(Boolean)
    .join("\n");
}

export function useBlueBrickSdkChatRuntime(options: BlueBrickSdkChatRuntimeOptions): BlueBrickSdkChatRuntime {
  const transport = useMemo(
    () =>
      createBlueBrickChatTransport({
        baseUrl: options.baseUrl,
        sessionId: options.sessionId,
        scopeId: options.scopeId || "local_vault",
        uploadConsentApproved: options.uploadConsentApproved === true,
        getSessionId: () => options.sessionId,
        getModelId: () => options.modelId,
        getScopeId: () => options.scopeId || "local_vault",
        getUploadConsentApproved: () => options.uploadConsentApproved === true,
        getAttachments: () => options.attachments || [],
        postMessage: options.postMessage
      }),
    [
      options.baseUrl,
      options.sessionId,
      options.modelId,
      options.scopeId,
      options.uploadConsentApproved,
      options.attachments,
      options.postMessage
    ]
  );

  const chat = useChat<UIMessage>({
    transport,
    experimental_throttle: options.throttleMs ?? 60
  });

  const sendMessage = useCallback(
    (message: string) => {
      const trimmed = (message || "").trim();
      if (!trimmed) return;

      if (!options.enabled) {
        options.onFallbackMessage?.(trimmed);
        return;
      }

      chat.sendMessage({
        text: trimmed,
        metadata: {
          modelId: options.modelId,
          scopeId: options.scopeId || "local_vault",
          uploadConsentApproved: options.uploadConsentApproved === true
        }
      });
    },
    [chat, options.enabled, options.modelId, options.scopeId, options.uploadConsentApproved, options.onFallbackMessage]
  );

  return {
    enabled: options.enabled,
    messages: chat.messages,
    status: chat.status,
    error: chat.error,
    sendMessage,
    stop: chat.stop,
    clearError: chat.clearError
  };
}
