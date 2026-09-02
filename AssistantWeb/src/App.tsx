import { useCallback, useLayoutEffect, useRef, useState } from "react";
import "./styles.css";
import {
  createBlueBrickWindowBridge,
  type BlueBrickBridge,
  type BlueBrickBridgeHandlers,
} from "./bridge/blueBrickWebViewBridge";
import { HardwareCadPanel } from "./hardware-cad/HardwareCadPanel";
import { ExecutionBoardApp } from "./execution-board/ExecutionBoardApp";
import { ViraLabApp } from "./vira-lab/ViraLabApp";
import { RuntimeIdentitySurface } from "./runtimeIdentity";
import { resolveAssistantSurface } from "./surfaceRouting";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
type Message = {
  id: string;
  role: "user" | "assistant";
  text: string;
  streaming?: boolean;
  attachment?: string;
};

type Scope = {
  id: string;
  label: string;
  enabled: boolean;
  unavailableReason?: string;
};

type Model = {
  id: string;
  displayName: string;
  available?: boolean;
  supportsVision?: boolean;
  supportsToolCalling?: boolean;
  supportsStructuredOutput?: boolean;
};

type ScreenshotArtifact = {
  screenshotId?: string;
  artifactId?: string;
  fileName?: string;
  localOnlyCloudState?: string;
  width?: number;
  height?: number;
  sourceWindowTitle?: string;
  captureSource?: string;
  annotations?: Array<{ id?: string; label?: string; source?: string; reviewStatus?: string }>;
  contacts?: Array<{ id?: string; name?: string; email?: string; reviewStatus?: string }>;
  [key: string]: unknown;
};

type ToolResult = {
  label?: string;
  query?: string;
  status?: string;
  message?: string;
  items?: unknown[];
  receipt?: unknown;
};

type BridgeStatus = "offline" | "connecting" | "connected" | "error";

// ---------------------------------------------------------------------------
// App
// ---------------------------------------------------------------------------
export function App() {
  const [modelId, setModelId] = useState<string>("UNKNOWN");
  const [models, setModels] = useState<Model[]>([]);
  const [scopeId, setScopeId] = useState<string>("UNKNOWN");
  const [scopes, setScopes] = useState<Scope[]>([]);
  const [statusBlob, setStatusBlob] = useState<unknown>({ connection: "CONNECTING" });
  const [tools, setTools] = useState<unknown[]>([]);
  const [toolReceipts, setToolReceipts] = useState<unknown[]>([]);
  const [productCatalogs, setProductCatalogs] = useState<unknown>({});
  const [messages, setMessages] = useState<Message[]>([]);
  const [streaming, setStreaming] = useState(false);
  const [input, setInput] = useState("");
  const [screenshots, setScreenshots] = useState<ScreenshotArtifact[]>([]);
  const [toolResults, setToolResults] = useState<ToolResult[]>([]);
  const [screenshotReviews, setScreenshotReviews] = useState<Record<string, string>>({});
  const [bridgeStatus, setBridgeStatus] = useState<BridgeStatus>("offline");

  const bridgeRef = useRef<BlueBrickBridge | null>(null);
  const streamingIdRef = useRef<string | null>(null);
  const messagesRef = useRef<Message[]>([]);
  const screenshotsRef = useRef<ScreenshotArtifact[]>([]);

  // Synchronous bridge-state mirror: messagesRef.current is the canonical
  // transcript store for imperative host callbacks; React state is the
  // rendered representation of it. Every transcript mutation MUST go through
  // commitMessages so an immediately-following bbGetTranscript() observes the
  // final state without waiting for effects or paint.
  const commitMessages = useCallback(
    (
      updater:
        | Message[]
        | ((current: Message[]) => Message[]),
    ) => {
      const current = messagesRef.current;
      const next =
        typeof updater === "function"
          ? (updater as (current: Message[]) => Message[])(current)
          : updater;
      messagesRef.current = next;
      setMessages(next);
      return next;
    },
    [],
  );

  const syncScreenshots = useCallback(() => {
    setScreenshots([...screenshotsRef.current]);
  }, []);

  // -------------------------------------------------------------------------
  // Bridge handlers — all 17 host->browser callbacks
  // -------------------------------------------------------------------------
  const handlers: BlueBrickBridgeHandlers = {
    onReset: () => {
      streamingIdRef.current = null;
      commitMessages([]);
    },

    onAppend: (payload) => {
      const p =
        typeof payload === "string"
          ? safeParse(payload, { role: "", text: "", attachment: "" })
          : payload;
      const normalizedRole = p.role === "assistant" ? "assistant" : "user";

      // Defensive compatibility (frozen contract preserved): an assistant
      // bbAppend arriving while a pending record exists finalizes THAT
      // record instead of creating a second assistant message.
      const pendingId = streamingIdRef.current;
      if (normalizedRole === "assistant" && pendingId) {
        commitMessages((current) =>
          current.map((m) =>
            m.id === pendingId
              ? {
                  ...m,
                  text: typeof p.text === "string" ? p.text : "",
                  streaming: false,
                }
              : m,
          ),
        );
        setStreaming(false);
        streamingIdRef.current = null;
        return;
      }

      commitMessages((current) => [
        ...current,
        {
          id: cryptoId(),
          role: normalizedRole,
          text: p.text ?? "",
          attachment: p.attachment,
        },
      ]);
    },

    onTypingStart: () => {
      // Idempotence: handleSend already created the pending assistant record
      // for this logical request; never create a second one while active.
      if (streamingIdRef.current) return;

      setStreaming(true);
      const id = cryptoId();
      streamingIdRef.current = id;
      commitMessages((current) => [
        ...current,
        { id, role: "assistant", text: "", streaming: true },
      ]);
    },

    onAppendChunk: (text: string) => {
      const chunk = typeof text === "string" ? text : String(text ?? "");
      const sid = streamingIdRef.current;
      // Chunks update ONLY the pending assistant record created by
      // handleSend/onTypingStart; they must never spawn a new message.
      if (!sid) return;

      commitMessages((current) =>
        current.map((m) => (m.id === sid ? { ...m, text: m.text + chunk } : m)),
      );
    },

    onTypingStop: () => {
      setStreaming(false);
      const sid = streamingIdRef.current;
      if (sid) {
        commitMessages((current) =>
          current.map((m) => (m.id === sid ? { ...m, streaming: false } : m)),
        );
        streamingIdRef.current = null;
      }
    },

    onSetModel: (model: unknown) => {
      const m = model as string | { id?: string; name?: string; displayName?: string };
      if (typeof m === "string") setModelId(m);
      else if (m && typeof m === "object")
        setModelId(m.id ?? m.name ?? m.displayName ?? "UNKNOWN");
      else setModelId(String(m ?? "UNKNOWN"));
    },

    onSetModels: (rawModels: unknown[]) => {
      setModels(
        (rawModels ?? []).map((m) => {
          const mo = m as Model & { Id?: string; Name?: string; DisplayName?: string; Available?: boolean; SupportsVision?: boolean };
          const id = mo.id ?? mo.Id ?? String(mo.displayName ?? mo.DisplayName ?? mo.id ?? "unknown");
          return {
            id,
            displayName: mo.displayName ?? mo.DisplayName ?? id,
            available: mo.available ?? mo.Available,
            supportsVision: mo.supportsVision ?? mo.SupportsVision,
            supportsToolCalling: mo.supportsToolCalling,
            supportsStructuredOutput: mo.supportsStructuredOutput,
          };
        }),
      );
    },

    onSetScope: (rawScopeId: unknown) => {
      setScopeId(typeof rawScopeId === "string" ? rawScopeId : String(rawScopeId ?? "UNKNOWN"));
    },

    onSetScopes: (rawScopes: unknown[]) => {
      setScopes(
        (rawScopes ?? []).map((s) => {
          const sc = s as Scope & { Id?: string; Label?: string; Enabled?: boolean; UnavailableReason?: string | null };
          const id = sc.id ?? sc.Id ?? "unknown";
          return {
            id,
            label: sc.label ?? sc.Label ?? id ?? "Unknown",
            enabled: sc.enabled ?? sc.Enabled ?? true,
            unavailableReason: sc.unavailableReason ?? sc.UnavailableReason ?? undefined,
          };
        }),
      );
    },

    onSetStatus: (rawStatus: unknown) => {
      const s = rawStatus as Record<string, unknown>;
      setStatusBlob(s ?? {});
      if (s && typeof s === "object") {
        if (s.scopes && Array.isArray(s.scopes)) {
          setScopes(
            (s.scopes as unknown[]).map((sc) => {
              const scope = sc as Scope & { Id?: string; Label?: string; Enabled?: boolean; UnavailableReason?: string | null };
              const id = scope.id ?? scope.Id ?? "unknown";
              return {
                id,
                label: scope.label ?? scope.Label ?? id,
                enabled: scope.enabled ?? scope.Enabled ?? true,
                unavailableReason: scope.unavailableReason ?? scope.UnavailableReason ?? undefined,
              };
            }),
          );
        }
        if (typeof s.scopeId === "string") setScopeId(s.scopeId);
        if (typeof s.ScopeId === "string") setScopeId(s.ScopeId);
        if (typeof s.model === "string") setModelId(s.model);
        if (typeof s.activeModel === "string") setModelId(s.activeModel);
        if (s.activeModelDescriptor && typeof s.activeModelDescriptor === "object") {
          const md = s.activeModelDescriptor as { id?: string; displayName?: string };
          if (md.id) setModelId(md.id);
        }
        if (s.activeModel && typeof s.activeModel === "object") {
          const am = s.activeModel as { id?: string; Id?: string };
          const activeId = am.id ?? am.Id;
          if (activeId) setModelId(activeId);
        }
      }
    },

    onSetTools: (rawTools: unknown[]) => {
      setTools(rawTools ?? []);
    },

    onSetToolReceipts: (rawReceipts: unknown[]) => {
      setToolReceipts(rawReceipts ?? []);
    },

    onSetProductCatalogs: (catalogs: unknown) => {
      const c = (catalogs ?? {}) as Record<string, unknown> & {
        Integrations?: unknown;
        Documents?: unknown;
      };
      setProductCatalogs({
        integrations: c.integrations ?? c.Integrations ?? {},
        documents: c.documents ?? c.Documents ?? {},
      });
    },

    onAppendToolResult: (result: unknown) => {
      const r = result as ToolResult;
      setToolResults((prev) => [...prev, r ?? {}]);
    },

    onAppendScreenshotArtifact: (artifact: unknown) => {
      const a = artifact as ScreenshotArtifact;
      if (a) {
        screenshotsRef.current = [...screenshotsRef.current, a];
        syncScreenshots();
      }
    },

    onUpdateScreenshotArtifact: (update: unknown) => {
      const u = update as ScreenshotArtifact;
      if (u && u.screenshotId) {
        screenshotsRef.current = screenshotsRef.current.map((s) =>
          s.screenshotId === u.screenshotId ? { ...s, ...u } : s,
        );
        syncScreenshots();
      }
    },

    onGetTranscript: () => {
      return messagesRef.current.map((m) => ({
        role: m.role,
        text: m.text ?? "",
      }));
    },
  };

  const finalizePendingBridgeFailure = useCallback(() => {
    const pendingId = streamingIdRef.current;
    streamingIdRef.current = null;
    setStreaming(false);
    if (!pendingId) return;

    commitMessages((current) =>
      current.map((m) =>
        m.id === pendingId
          ? { ...m, text: "Bridge transport failed. Please try again.", streaming: false }
          : m,
      ),
    );
  }, [commitMessages]);

  // -------------------------------------------------------------------------
  // Install bridge on mount
  // -------------------------------------------------------------------------
  useLayoutEffect(() => {
    let mounted = true;
    setBridgeStatus("connecting");

    try {
      const bridge = createBlueBrickWindowBridge(handlers, {
        onTransportError: () => {
          if (!mounted) return;
          setBridgeStatus("error");
          finalizePendingBridgeFailure();
        },
      });
      bridgeRef.current = bridge;
      setBridgeStatus(bridge.isHostAvailable() ? "connected" : "offline");
    } catch {
      bridgeRef.current = null;
      setBridgeStatus("error");
    }

    return () => {
      bridgeRef.current = null;
      setBridgeStatus("offline");
      mounted = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // -------------------------------------------------------------------------
  // Actions
  // -------------------------------------------------------------------------
  const handleSelectModel = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      const id = e.target.value;
      setModelId(id);
      bridgeRef.current?.post("selectModel", { type: "selectModel", modelId: id });
    },
    [],
  );

  const handleSelectScope = useCallback(
    (s: Scope) => {
      if (!s.enabled) return;
      setScopeId(s.id);
      bridgeRef.current?.post("selectScope", { type: "selectScope", scopeId: s.id });
    },
    [],
  );

  const handleCapture = useCallback(() => {
    bridgeRef.current?.post("captureScreenshot", { type: "captureScreenshot" });
  }, []);

  const handleAttach = useCallback(() => bridgeRef.current?.post("attach", { type: "attach" }), []);

  const handleSearch = useCallback(() => {
    const msg = input.trim();
    if (msg) {
      bridgeRef.current?.post("search", { type: "search", message: msg, scopeId });
    }
  }, [input, scopeId]);

  const handleSend = useCallback(() => {
    const msg = input.trim();
    if (!msg) return;
    // Single transaction: the ref must contain the exact user + pending
    // assistant records BEFORE the host can later call bbGetTranscript.
    const pendingAssistantId = cryptoId();
    streamingIdRef.current = pendingAssistantId;
    commitMessages((current) => [
      ...current,
      { id: cryptoId(), role: "user", text: msg },
      { id: pendingAssistantId, role: "assistant", text: "", streaming: true },
    ]);
    setInput("");
    setStreaming(true);
    bridgeRef.current?.post("sendMessage", { type: "sendMessage", message: msg, scopeId });
  }, [input, scopeId, commitMessages]);

  const handleStop = useCallback(() => {
    bridgeRef.current?.post("cancelMessage", { type: "cancelMessage" });
    setStreaming(false);
    const sid = streamingIdRef.current;
    if (sid) {
      commitMessages((current) =>
        current.map((m) => (m.id === sid ? { ...m, streaming: false } : m)),
      );
      streamingIdRef.current = null;
    }
  }, [commitMessages]);

  const handleNewSession = useCallback(() => {
    bridgeRef.current?.post("newSession", { type: "newSession" });
    streamingIdRef.current = null;
    screenshotsRef.current = [];
    commitMessages([]);
    syncScreenshots();
    setToolResults([]);
  }, [commitMessages, syncScreenshots]);

  const handleApprove = useCallback(
    (screenshotId: string) => {
      setScreenshotReviews((prev) => ({ ...prev, [screenshotId]: "approved" }));
      bridgeRef.current?.post("reviewScreenshotItem", {
        type: "reviewScreenshotItem",
        screenshotId,
        reviewStatus: "approved",
      });
    },
    [],
  );

  // -------------------------------------------------------------------------
  // Derived state
  // -------------------------------------------------------------------------
  const availableModels = models.length > 0 ? models : [{ id: "UNKNOWN", displayName: "UNKNOWN" }];
  const statusObj = statusBlob as Record<string, unknown>;
  const connectionState =
    statusObj?.connection ?? (statusObj?.configured ? "READY" : "CONNECTING");
  const modeLabel =
    typeof statusObj?.mode === "string"
      ? String(statusObj.mode)
      : typeof statusObj?.AssistantMode === "string"
        ? String(statusObj.AssistantMode)
        : "UNKNOWN";
  const surface = resolveAssistantSurface(typeof window !== "undefined" ? window.location.search : "");

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------
  if (surface === "execution-board") {
    return <ExecutionBoardApp />;
  }
  if (surface === "vira-lab") {
    return <ViraLabApp search={typeof window !== "undefined" ? window.location.search : ""} />;
  }
  if (surface === "hardware-cad") {
    return (
      <main className="shell vira-command-surface">
        <header className="top"><div className="brand-row"><div className="brand"><div className="mark" aria-hidden="true" /><div className="brand-text"><span className="brand-title">VIRA Hardware Intelligence</span><span className="brand-sub">MCM CAD Acquisition</span></div></div><RuntimeIdentitySurface /></div></header>
        <HardwareCadPanel />
      </main>
    );
  }
  return (
    <main className="shell vira-command-surface">
      <header className="top">
        <div className="brand-row">
          <div className="brand">
            <div className="mark" aria-hidden="true" />
            <div className="brand-text">
              <span className="brand-title">BlueBrick Assistant</span>
              <span className="brand-sub">{modeLabel}</span>
            </div>
          </div>
          <RuntimeIdentitySurface />
          <div className="chip-row">
            <span
              className={"chip conn " + (connectionState === "READY" ? "ok" : "warn")}
              title={"Connection state: " + String(connectionState)}
            >
              ● {String(connectionState)}
            </span>
            <span className="chip" title={"Tools available in the selected scope: " + tools.length}>
              tools {tools.length}
            </span>
            <span className="chip" title={"Tool receipts recorded: " + toolReceipts.length}>
              receipts {toolReceipts.length}
            </span>
            <span
              className="chip"
              title={"Product catalogs: " + (productCatalogs && Object.keys(productCatalogs).length > 0 ? "loaded from host" : "not loaded")}
            >
              catalogs {productCatalogs && Object.keys(productCatalogs).length > 0 ? "loaded" : "none"}
            </span>
          </div>
        </div>

        <div className="controls">
          <div className="select-row">
            <select
              id="assistant-model"
              className="select"
              aria-label="Select assistant model"
              value={modelId}
              onChange={handleSelectModel}
            >
              {availableModels.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.displayName ?? formatModelLabel(m.id)}
                </option>
              ))}
            </select>
            <span
              className="cap-count"
              title={
                "Active model: " + (availableModels.find((m) => m.id === modelId)?.displayName ?? formatModelLabel(modelId)) +
                " · Models available from host: " + models.length
              }
            >
              {models.length} models
            </span>
          </div>

          <div className="scope-chips">
            {scopes.map((s) => (
              <button
                key={s.id}
                className={"scope" + (s.id === scopeId ? " selected" : "")}
                data-scope={s.id}
                disabled={!s.enabled}
                onClick={() => handleSelectScope(s)}
                aria-label={"Select scope " + s.label}
                aria-disabled={!s.enabled}
                title={"Scope " + s.label + " — " + (s.enabled ? (s.unavailableReason ?? "available") : (s.unavailableReason ?? "unavailable"))}
              >
                <span>{s.label}</span>
              </button>
            ))}
          </div>

          <div className="primary-action-rail">
            <button className="action" aria-label="New session" title="New session — reset the conversation" onClick={handleNewSession}>
              <span className="action-symbol new" />
              <span className="action-label">New</span>
            </button>
            <button className="action" aria-label="Capture local screenshot" title="Capture — grab a local screenshot for analysis" onClick={handleCapture}>
              <span className="action-symbol capture" />
              <span className="action-label">Capture</span>
            </button>
            <button
              className="action"
              aria-label="Attach image or PDF"
              title="Attach — attach an image or PDF to your next message"
              onClick={handleAttach}
            >
              <span className="action-symbol attach" />
              <span className="action-label">Attach</span>
            </button>
            <button className="action" aria-label="Search the selected scope" title="Search — search the selected scope" onClick={handleSearch}>
              <span className="action-symbol search" />
              <span className="action-label">Search</span>
            </button>
            <button className="action primary" aria-label="More actions" title="More actions">
              <span className="action-symbol more" />
              <span className="action-label">More</span>
            </button>
          </div>
        </div>
      </header>

      <section className="thread" aria-live="polite">
        {messages.length === 0 && screenshots.length === 0 && toolResults.length === 0 && (
          <div className="empty">
            <div className="empty-title">BlueBrick is ready</div>
            <p className="empty-copy">
              Ask about the active model, capture the screen, or attach engineering context.
            </p>
            <div className="empty-actions">
              <button className="empty-action" aria-label="Capture local screenshot" title="Capture a local screenshot for analysis" onClick={handleCapture}>
                Capture
              </button>
              <button className="empty-action" aria-label="Attach image or PDF" title="Attach an image or PDF to your next message" onClick={handleAttach}>
                Attach
              </button>
            </div>
          </div>
        )}

        {messages.map((m) => (
          <div key={m.id} className={"msg" + (m.role === "user" ? " user" : "")}>
            <div className="role">{m.role}</div>
            <div className="text">
              {m.text}
              {m.streaming && <span className="streaming-cursor">▋</span>}
            </div>
            {m.attachment && (
              <div className="meta">
                <span>Attachment: {m.attachment}</span>
              </div>
            )}
          </div>
        ))}

        {screenshots.map((s) => (
          <div key={s.screenshotId ?? s.artifactId ?? s.fileName ?? "screenshot"} className="shot">
            <div className="shot-head">
              <strong className="badge">Screenshot captured</strong>
              <span className="badge">
                {s.width && s.height ? `${s.width} x ${s.height}` : "size unknown"}
              </span>
            </div>
            {s.fileName && <div className="catalog-sub">{s.fileName}</div>}
            {s.sourceWindowTitle && <div className="catalog-sub">{s.sourceWindowTitle}</div>}
            <div className="meta">
              <span>
                local only: {String(s.localOnlyCloudState ?? "local only")}
              </span>
              <span>
                annotations: {s.annotations?.length ?? 0} · contacts: {s.contacts?.length ?? 0}
              </span>
            </div>
            {s.annotations && s.annotations.length > 0 && (
              <div className="list">
                {s.annotations.map((a, i) => (
                  <div key={a.id ?? i} className="receipt-summary">
                    <strong>{a.label ?? "Annotation"}</strong>
                    <span>{a.source ?? "unknown"} · {a.reviewStatus ?? "pending"}</span>
                  </div>
                ))}
              </div>
            )}
            {s.contacts && s.contacts.length > 0 && (
              <div className="list">
                {s.contacts.map((c, i) => (
                  <div key={c.id ?? i} className="receipt-summary">
                    <strong>{c.name ?? "Contact"}</strong>
                    <span>{c.email ?? ""} · {c.reviewStatus ?? "pending"}</span>
                  </div>
                ))}
              </div>
            )}
            <div className="review-actions">
              <button
                aria-label={"Approve screenshot " + (s.screenshotId ?? "")}
                disabled={screenshotReviews[s.screenshotId ?? ""] === "approved"}
                onClick={() => s.screenshotId && handleApprove(s.screenshotId)}
              >
                Approve
              </button>
              <button aria-label="Reject screenshot review">
                Reject
              </button>
            </div>
          </div>
        ))}

        {toolResults.map((t, i) => (
          <div key={i} className="tool-card">
            <div className="tool-head">
              <strong className="badge">{t.label ?? "Tool result"}</strong>
              <span className={"badge " + (t.status === "done" ? "ok" : "warn")}>
                {t.status ?? "done"}
              </span>
            </div>
            {t.message && <div className="catalog-sub">{t.message}</div>}
            {t.query && <div className="catalog-sub">Query: {t.query}</div>}
          </div>
        ))}
      </section>

      <footer className="footer">
        <div className="composer">
          <textarea
            aria-label="Message BlueBrick Assistant"
            placeholder="Type a message..."
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                if (streaming) {
                  handleStop();
                } else {
                  handleSend();
                }
              }
            }}
          />
          <div className="composer-actions">
            {streaming ? (
              <button
                className="stop-button"
                aria-label="Stop streaming response"
                onClick={handleStop}
              >
                Stop
              </button>
            ) : (
              <button
                className="send-button"
                aria-label="Send message"
                onClick={handleSend}
                disabled={!input.trim()}
              >
                Send
              </button>
            )}
          </div>
        </div>
        <div
          className="safety-footer"
          title="Local-first: screenshots stay local unless explicitly approved. Bridge status reflects the local host connection."
        >
          <span className={"safety-dot" + (bridgeStatus === "connected" ? "" : " off")} aria-hidden="true">●</span>
          <span className="safety-label">Local-first</span>
          <span className="safety-spacer" aria-hidden="true" />
          <span className="safety-bridge">Bridge {bridgeStatus}</span>
        </div>
      </footer>
    </main>
  );
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
function cryptoId(): string {
  // crypto.randomUUID when available, deterministic fallback otherwise.
  try {
    if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
      return crypto.randomUUID();
    }
  } catch {
    /* fall through */
  }
  return "id-" + Date.now().toString(36) + "-" + Math.random().toString(36).slice(2, 10);
}

/**
 * Presentation-only label formatting for model identifiers that arrive
 * without backend display metadata. Model IDs crossing the bridge are
 * never modified.
 */
function formatModelLabel(id: string): string {
  if (!id) return id;
  return id
    .split(/[-_]+/)
    .filter(Boolean)
    .map((part) => {
      if (/^\d+(\.\d+)*$/.test(part)) return part;
      if (part.toLowerCase() === "gpt") return part.toUpperCase();
      if (part.length <= 3 && !/\d/.test(part)) return part.toUpperCase();
      return part.charAt(0).toUpperCase() + part.slice(1);
    })
    .join(" ");
}

function safeParse<T>(raw: string, fallback: T): T {
  try {
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
}
