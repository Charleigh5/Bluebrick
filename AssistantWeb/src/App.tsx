import { useCallback, useEffect, useLayoutEffect, useRef, useState, type CSSProperties } from "react";
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
  unavailableReason?: string;
  supportsVision?: boolean;
  supportsToolCalling?: boolean;
  supportsStructuredOutput?: boolean;
};

type ScreenshotArtifact = {
  thumbnailDataUrl?: string;
  attachedToConversation?: boolean;
  approvalMode?: string;
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
// Theme (accent + secondary, persisted default)
// ---------------------------------------------------------------------------
const BUILT_IN_ACCENT = "#c8ff2e";
const BUILT_IN_SECONDARY = "#2dd4bf";
const ACCENT_PRESETS = ["#c8ff2e", "#22b8d6", "#fbbf24", "#a78bfa", "#fb923c"];
const SECONDARY_PRESETS = ["#2dd4bf", "#22b8d6", "#38bdf8", "#fb7185", "#94a3b8"];

function readThemeDefault(key: "accent" | "secondary", fallback: string): string {
  try {
    return window.localStorage.getItem("bb.theme." + key) ?? fallback;
  } catch {
    return fallback;
  }
}

function inkFor(hex: string): string {
  const m = /^#([0-9a-f]{6})$/i.exec((hex ?? "").trim());
  if (!m) return "#111318";
  const v = parseInt(m[1], 16);
  const r = (v >> 16) & 255;
  const g = (v >> 8) & 255;
  const b = v & 255;
  return (0.299 * r + 0.587 * g + 0.114 * b) / 255 > 0.55 ? "#111318" : "#f4f7fa";
}

// ---------------------------------------------------------------------------
// App
// ---------------------------------------------------------------------------
export function App() {
  const [modelId, setModelId] = useState<string>("UNKNOWN");
  const [pendingModel, setPendingModel] = useState<string | null>(null);
  const pendingModelRef = useRef<{ id: string; operationId: string } | null>(null);
  const latestModelOperationRef = useRef<string | null>(null);
  const [operationError, setOperationError] = useState("");
  const [models, setModels] = useState<Model[]>([]);
  const [scopeId, setScopeId] = useState<string>("UNKNOWN");
  const [scopes, setScopes] = useState<Scope[]>([]);
  const [statusBlob, setStatusBlob] = useState<unknown>({});
  const [tools, setTools] = useState<unknown[]>([]);
  const [toolReceipts, setToolReceipts] = useState<unknown[]>([]);
  const [productCatalogs, setProductCatalogs] = useState<unknown>({});
  const [accent, setAccent] = useState(() => readThemeDefault("accent", BUILT_IN_ACCENT));
  const [secondary, setSecondary] = useState(() => readThemeDefault("secondary", BUILT_IN_SECONDARY));
  const [themeOpen, setThemeOpen] = useState(false);
  const [reviewNotes, setReviewNotes] = useState<Record<string, string>>({});

  useLayoutEffect(() => {
    const root = document.documentElement;
    root.style.setProperty("--accent", accent);
    root.style.setProperty("--accent-ink", inkFor(accent));
    root.style.setProperty("--secondary", secondary);
    root.style.setProperty("--secondary-ink", inkFor(secondary));
  }, [accent, secondary]);

  const saveThemeDefault = useCallback(() => {
    try {
      window.localStorage.setItem("bb.theme.accent", accent);
      window.localStorage.setItem("bb.theme.secondary", secondary);
    } catch {
      /* storage unavailable: theme still applies for this session */
    }
    setThemeOpen(false);
  }, [accent, secondary]);

  const resetTheme = useCallback(() => {
    try {
      window.localStorage.removeItem("bb.theme.accent");
      window.localStorage.removeItem("bb.theme.secondary");
    } catch {
      /* ignore */
    }
    setAccent(BUILT_IN_ACCENT);
    setSecondary(BUILT_IN_SECONDARY);
  }, []);
  const [messages, setMessages] = useState<Message[]>([]);
  const [streaming, setStreaming] = useState(false);
  const [input, setInput] = useState("");
  const [screenshots, setScreenshots] = useState<ScreenshotArtifact[]>([]);
  const [toolResults, setToolResults] = useState<ToolResult[]>([]);
  const [screenshotReviews, setScreenshotReviews] = useState<Record<string, string>>({});
  const [bridgeStatus, setBridgeStatus] = useState<BridgeStatus>("offline");

  const reviewOperationsRef = useRef<Record<string, string>>({});
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
      screenshotsRef.current = [];
      reviewOperationsRef.current = {};
      commitMessages([]);
      syncScreenshots();
      setScreenshotReviews({});
      setToolResults([]);
      setStreaming(false);
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
      const m = model as { id?: string; Id?: string; operationId?: string; error?: string };
      const id = typeof model === "string" ? model : m?.id ?? m?.Id;
      if (m?.operationId && m.operationId !== latestModelOperationRef.current) return;
      const pending = pendingModelRef.current;
      if (pending) {
        if (m?.operationId !== pending.operationId) return;
        pendingModelRef.current = null;
        setPendingModel(null);
        if (m.error || id !== pending.id) {
          setOperationError(m.error ?? "The host did not acknowledge the requested model.");
          return;
        }
      }
      if (id) setModelId(id);
    },

    onSetModels: (rawModels: unknown[]) => {
      setModels(
        (rawModels ?? []).map((m) => {
          const mo = m as Model & { Id?: string; Name?: string; DisplayName?: string; Available?: boolean; UnavailableReason?: string; Enabled?: boolean; SupportsTools?: boolean; SupportsJsonMode?: boolean; SupportsVision?: boolean };
          const id = mo.id ?? mo.Id ?? String(mo.displayName ?? mo.DisplayName ?? mo.id ?? "unknown");
          return {
            id,
            displayName: mo.displayName ?? mo.DisplayName ?? mo.Name ?? id,
            available: mo.available ?? mo.Available ?? mo.Enabled,
            unavailableReason: mo.unavailableReason ?? mo.UnavailableReason,
            supportsVision: mo.supportsVision ?? mo.SupportsVision,
            supportsToolCalling: mo.supportsToolCalling ?? mo.SupportsTools,
            supportsStructuredOutput: mo.supportsStructuredOutput ?? mo.SupportsJsonMode,
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
        // Status refreshes carry display labels too; only authoritative IDs may select a model.
        if (!pendingModelRef.current) {
          const descriptor = s.activeModelDescriptor as { id?: string; Id?: string } | undefined;
          const active = s.activeModel as { id?: string; Id?: string } | undefined;
          const confirmedId = descriptor?.id ?? descriptor?.Id ?? active?.id ?? active?.Id;
          if (confirmedId) setModelId(confirmedId);
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
        if (u.reviewOperationId) {
          if (u.reviewOperationId !== reviewOperationsRef.current[u.screenshotId]) return;
          delete reviewOperationsRef.current[u.screenshotId];
          setScreenshotReviews((previous) => ({ ...previous, [u.screenshotId!]: u.reviewError ? "failed" : "acknowledged" }));
          if (u.reviewError) setOperationError(String(u.reviewError));
        }
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
  const requestModel = useCallback(
    (id: string) => {
      if (pendingModelRef.current) return;
      if (!bridgeRef.current?.isHostAvailable()) { setOperationError("Model selection unavailable: host is offline."); return; }
      const operationId = cryptoId();
      latestModelOperationRef.current = operationId;
      pendingModelRef.current = { id, operationId };
      setPendingModel(id);
      setOperationError("");
      bridgeRef.current.post("selectModel", { type: "selectModel", modelId: id, operationId });
      window.setTimeout(() => {
        if (pendingModelRef.current?.operationId !== operationId) return;
        pendingModelRef.current = null;
        setPendingModel(null);
        setOperationError("Model selection timed out. The confirmed selection has not changed.");
      }, 30000);
    },
    [],
  );

  const handleSelectModel = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      lastAutoTargetRef.current = null;
      requestModel(e.target.value);
    },
    [requestModel],
  );

  // Auto-select the first available model when the current selection is
  // unknown or unavailable (e.g. its API key is missing). One attempt per
  // models payload; an explicit user choice always wins.
  const lastAutoTargetRef = useRef<string | null>(null);
  useEffect(() => {
    lastAutoTargetRef.current = null;
  }, [models]);
  useEffect(() => {
    if (bridgeStatus !== "connected" || pendingModelRef.current || models.length === 0) return;
    const current = models.find((m) => m.id === modelId);
    if (current && current.available !== false) return;
    const target = models.find((m) => m.available !== false) ?? models[0];
    if (!target || target.id === modelId || target.id === lastAutoTargetRef.current) return;
    lastAutoTargetRef.current = target.id;
    requestModel(target.id);
  }, [models, bridgeStatus, modelId, requestModel]);

  const handleSelectScope = useCallback(
    (s: Scope) => {
      if (!s.enabled) return;
      if (!bridgeRef.current?.isHostAvailable()) { setOperationError("Scope selection unavailable: host is offline."); return; }
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
    if (!bridgeRef.current?.isHostAvailable()) { setOperationError("Send unavailable: host is offline."); return; }
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
    if (!bridgeRef.current?.isHostAvailable()) { setOperationError("New session unavailable: host is offline."); return; }
    bridgeRef.current.post("newSession", { type: "newSession" });
  }, [commitMessages, syncScreenshots]);

  const handleReview = useCallback(
    (screenshotId: string, reviewStatus: "approved" | "rejected", targetType = "screenshot", note = "") => {
      if (!bridgeRef.current?.isHostAvailable()) { setOperationError("Review unavailable: host is offline."); return; }
      const operationId = cryptoId();
      reviewOperationsRef.current[screenshotId] = operationId;
      setScreenshotReviews((prev) => ({ ...prev, [screenshotId]: "pending" }));
      setOperationError("");
      bridgeRef.current.post("reviewScreenshotItem", {
        type: "reviewScreenshotItem", screenshotId, targetType, targetId: screenshotId, reviewStatus, operationId,
        reviewNote: note,
      });
      setReviewNotes((prev) => {
        if (!(screenshotId in prev)) return prev;
        const next = { ...prev };
        delete next[screenshotId];
        return next;
      });
      window.setTimeout(() => {
        if (reviewOperationsRef.current[screenshotId] !== operationId) return;
        delete reviewOperationsRef.current[screenshotId];
        setScreenshotReviews((prev) => ({ ...prev, [screenshotId]: "failed" }));
        setOperationError("Screenshot review timed out. The saved decision is unknown; no success is assumed.");
      }, 30000);
    }, [],
  );

  // -------------------------------------------------------------------------
  // Derived state
  // -------------------------------------------------------------------------
  const availableModels = models.length > 0 ? models : [{ id: "UNKNOWN", displayName: "UNKNOWN" }];
  const selectedModel = availableModels.find((m) => m.id === modelId);
  const selectedModelIssue =
    selectedModel && selectedModel.available === false
      ? (selectedModel.unavailableReason ?? "This model is currently unavailable.")
      : null;
  const statusObj = statusBlob as Record<string, unknown>;
  const connectionState =
    bridgeStatus !== "connected"
      ? "HOST OFFLINE"
      : typeof (statusObj?.configured ?? statusObj?.Configured) === "boolean"
        ? "HOST CONNECTED"
        : "HOST CONNECTING";
  const modeLabel =
    typeof statusObj?.mode === "string"
      ? String(statusObj.mode)
      : typeof statusObj?.AssistantMode === "string"
        ? String(statusObj.AssistantMode)
        : "UNKNOWN";
  const providerConfigured = statusObj?.configured ?? statusObj?.Configured;
  const providerState = connectionState !== "HOST CONNECTED"
    ? "NOT VERIFIED"
    : providerConfigured !== true
      ? "UNAVAILABLE"
      : modeLabel.toLowerCase() === "mock" ? "MOCK MODE" : "CONFIGURED";
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
              className={"chip conn " + (connectionState === "HOST CONNECTED" ? "ok" : "warn")}
              title={"Host connection: " + connectionState}
            >
              ● {String(connectionState)}
            </span>
            <span className="chip" title={"Model provider: " + providerState}>
              MODEL {providerState}
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
            <div className="theme-wrap">
              <button
                className="chip"
                aria-label="Theme settings"
                title="Adjust accent and secondary colors"
                aria-expanded={themeOpen}
                onClick={() => setThemeOpen((v) => !v)}
              >
                🎨 theme
              </button>
              {themeOpen && (
                <div className="theme-pop" role="dialog" aria-label="Theme settings">
                  <span className="theme-group-label">Accent</span>
                  <div className="theme-row">
                    <div className="swatches">
                      {ACCENT_PRESETS.map((c) => (
                        <button
                          key={c}
                          className="swatch"
                          style={{ "--swatch": c } as CSSProperties}
                          title={c}
                          aria-label={"Accent " + c}
                          aria-pressed={accent.toLowerCase() === c}
                          onClick={() => setAccent(c)}
                        />
                      ))}
                    </div>
                    <input type="color" className="theme-custom" value={accent} onChange={(e) => setAccent(e.target.value)} aria-label="Custom accent color" />
                  </div>
                  <span className="theme-group-label">Secondary</span>
                  <div className="theme-row">
                    <div className="swatches">
                      {SECONDARY_PRESETS.map((c) => (
                        <button
                          key={c}
                          className="swatch"
                          style={{ "--swatch": c } as CSSProperties}
                          title={c}
                          aria-label={"Secondary " + c}
                          aria-pressed={secondary.toLowerCase() === c}
                          onClick={() => setSecondary(c)}
                        />
                      ))}
                    </div>
                    <input type="color" className="theme-custom" value={secondary} onChange={(e) => setSecondary(e.target.value)} aria-label="Custom secondary color" />
                  </div>
                  <div className="theme-actions">
                    <button className="save-default" onClick={saveThemeDefault}>Set as default</button>
                    <button onClick={resetTheme}>Reset</button>
                  </div>
                  <span className="theme-hint">Defaults are saved on this machine only.</span>
                </div>
              )}
            </div>
          </div>
        </div>

        {operationError && <p role="alert">{operationError}</p>}
        {pendingModel && <p role="status">Waiting for the host to acknowledge model selection…</p>}
        <div className="controls">
          <div className="select-row">
            <select
              id="assistant-model"
              className="select"
              aria-label="Select assistant model"
              value={modelId}
              onChange={handleSelectModel}
              disabled={!!pendingModel || bridgeStatus !== "connected" || models.length === 0}
            >
              {availableModels.map((m) => (
                <option
                  key={m.id}
                  value={m.id}
                  disabled={m.available === false}
                  title={m.available === false ? (m.unavailableReason ?? "Unavailable") : (m.displayName ?? m.id)}
                >
                  {(m.displayName ?? formatModelLabel(m.id)) + (m.available === false ? " (unavailable)" : "")}
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
          {selectedModelIssue && <p className="model-note" role="status">Selected model unavailable: {selectedModelIssue}</p>}

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
            <button className="action primary" aria-label="More actions" title="More actions are unavailable" disabled>
              <span className="action-symbol more" />
              <span className="action-label">More</span>
            </button>
          </div>
        </div>
      </header>

      <section className="thread" aria-live="polite">
        {messages.length === 0 && screenshots.length === 0 && toolResults.length === 0 && (
          <div className="empty">
            <div className="empty-title">{bridgeStatus === "connected" ? "Start a conversation" : "BlueBrick host is offline"}</div>
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
            {s.thumbnailDataUrl?.startsWith("data:image/jpeg;base64,") && (
              <img src={s.thumbnailDataUrl} alt={s.attachedToConversation ? "Screenshot attached to this conversation" : "Locally captured screenshot"} style={{ maxWidth: "100%", maxHeight: 220, objectFit: "contain" }} />
            )}
            <div role="status">{s.attachedToConversation ? "Attached to this conversation" : "Local capture — not attached"} · {s.approvalMode ?? "MANUAL"} local review · Upload consent is separate</div>
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
              <input
                className="note-input"
                aria-label={"Review note for screenshot " + (s.screenshotId ?? "")}
                placeholder="Add a review note (optional)…"
                value={reviewNotes[s.screenshotId ?? ""] ?? ""}
                onChange={(e) => setReviewNotes((prev) => ({ ...prev, [s.screenshotId ?? ""]: e.target.value }))}
              />
              <button
                aria-label={"Approve screenshot " + (s.screenshotId ?? "")}
                disabled={screenshotReviews[s.screenshotId ?? ""] === "pending" || s.reviewStatus === "approved"}
                onClick={() => s.screenshotId && handleReview(s.screenshotId, "approved", "screenshot", reviewNotes[s.screenshotId ?? ""] ?? "")}
              >
                Approve
              </button>
              <button aria-label="Reject screenshot review"
                disabled={screenshotReviews[s.screenshotId ?? ""] === "pending"}
                onClick={() => s.screenshotId && handleReview(s.screenshotId, "rejected", "screenshot", reviewNotes[s.screenshotId ?? ""] ?? "")}>
                Reject
              </button>
              <button
                disabled={s.reviewStatus !== "approved" || s.cloudSendApproved === true || screenshotReviews[s.screenshotId ?? ""] === "pending"}
                onClick={() => s.screenshotId && handleReview(s.screenshotId, "approved", "screenshot-upload", reviewNotes[s.screenshotId ?? ""] ?? "")}>
                Allow image upload
              </button>
              <span role="status">{screenshotReviews[s.screenshotId ?? ""] === "pending" ? "Saving decision…" : String(s.reviewStatus ?? "pending")}
                {s.cloudSendApproved === true ? " · upload allowed" : " · local only"}</span>
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
          <span className="safety-bridge">{connectionState}</span>
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
