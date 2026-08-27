import { useCallback, useEffect, useRef, useState } from "react";
import { ActiveDocumentContextCard } from "../ActiveDocumentContextCard";
import { normalizeActiveDocumentContext, type ActiveDocumentContext } from "../activeDocumentContext";
import { PacketReviewPanel } from "../packet-review/PacketReviewPanel";
import {
  type PacketReviewLifecycleEvent,
  type ViraPacketWorkflowState
} from "./packetWorkflow";
import {
  createViraLabRuntimeFromSearch,
  type ViraDiagnostic,
  type ViraLabRuntime,
  type ViraRuntimeKind
} from "./viraLabRuntime";
import { buildViraDiagnosticsExport, buildViraLabReceipt } from "./viraLabReceipt";
import "./vira-lab.css";

type WorkbenchState = "loading" | "ready" | "unavailable" | "error";

const runtimeLabels: Record<ViraRuntimeKind, string> = {
  fixture: "Fixture",
  "localhost-relay": "Local Relay",
  "embedded-host": "SOLIDWORKS Host",
  unavailable: "Unavailable"
};

function loadingContext(): ActiveDocumentContext {
  return normalizeActiveDocumentContext({
    status: "loading",
    message: "Loading the active-document context from the selected VIRA Lab runtime."
  });
}

function unavailableContext(message: string): ActiveDocumentContext {
  return normalizeActiveDocumentContext({ status: "unavailable", message });
}

function errorContext(message: string): ActiveDocumentContext {
  return normalizeActiveDocumentContext({ status: "error", message });
}

function stateFromContext(context: ActiveDocumentContext): WorkbenchState {
  if (context.state === "loading") return "loading";
  if (context.state === "unavailable") return "unavailable";
  if (context.state === "error") return "error";
  return "ready";
}

function RuntimeBadge({ kind }: { kind: ViraRuntimeKind }) {
  return <span className={`vira-lab-runtime ${kind}`}>{runtimeLabels[kind]}</span>;
}

function saveJson(fileName: string, value: unknown): void {
  const url = URL.createObjectURL(new Blob([`${JSON.stringify(value, null, 2)}\n`], { type: "application/json;charset=utf-8" }));
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

export function ViraLabApp({ search }: { search: string }) {
  const sessionStartedAt = useRef(Date.now());
  const [runtime] = useState<ViraLabRuntime>(() => createViraLabRuntimeFromSearch(search));
  const [state, setState] = useState<WorkbenchState>("loading");
  const [context, setContext] = useState<ActiveDocumentContext>(() => loadingContext());
  const [diagnostics, setDiagnostics] = useState<ViraDiagnostic[]>([]);
  const [packetState, setPacketState] = useState<ViraPacketWorkflowState>("idle");
  const [packetSummary, setPacketSummary] = useState<PacketReviewLifecycleEvent | null>(null);
  const [exportStatus, setExportStatus] = useState("");

  const loadContext = useCallback(async () => {
    setState("loading");
    setContext(loadingContext());
    const result = await runtime.getActiveDocumentContext();
    if (result.status === "ok") {
      setContext(result.value);
      setState(stateFromContext(result.value));
      return;
    }

    const nextContext = result.status === "error" ? errorContext(result.message) : unavailableContext(result.message);
    setContext(nextContext);
    setState(result.status);
  }, [runtime]);

  useEffect(() => runtime.subscribeDiagnostics((diagnostic) => {
    setDiagnostics((current) => [...current.slice(-19), diagnostic]);
  }), [runtime]);

  useEffect(() => {
    void loadContext();
  }, [loadContext]);

  const handlePacketLifecycle = useCallback((event: PacketReviewLifecycleEvent) => {
    setPacketState(event.state);
    setPacketSummary(event);
    setDiagnostics((current) => [
      ...current.slice(-19),
      {
        id: `packet-${Date.now()}`,
        level: event.state === "failed" ? "error" : event.state === "partial" ? "warning" : "info",
        code: `PACKET_${event.state.toUpperCase().replaceAll("-", "_")}`,
        message: event.message
      }
    ]);
  }, []);

  const latestDiagnostic = diagnostics.at(-1);
  const packetComplete = packetState === "complete" || packetState === "partial";
  const comparisonActive = packetState === "evaluating";
  const canExportReceipt = packetComplete && packetSummary?.retainsEvidence === true;

  const exportLabReceipt = () => {
    if (!canExportReceipt) return;
    saveJson(
      "vira-lab-session-receipt.json",
      buildViraLabReceipt({
        sessionId: runtime.sessionId,
        runtimeKind: runtime.kind,
        workbenchState: state,
        packetState,
        packetSummary,
        context,
        diagnostics,
        durationMs: Date.now() - sessionStartedAt.current
      })
    );
    setExportStatus("Sanitized VIRA Lab receipt exported.");
  };

  const exportDiagnostics = () => {
    saveJson("vira-lab-diagnostics.json", buildViraDiagnosticsExport(runtime.sessionId, diagnostics));
    setExportStatus("Bounded diagnostics exported.");
  };

  return (
    <main className="vira-lab-shell" data-workbench-state={state} data-packet-state={packetState}>
      <header className="vira-lab-header">
        <div className="vira-lab-brand">
          <div className="vira-lab-mark" aria-hidden="true">V</div>
          <div>
            <h1>VIRA Lab</h1>
            <p>Engineering evidence workbench</p>
          </div>
        </div>
        <div className="vira-lab-runtime-cluster" aria-label="Runtime boundary">
          <RuntimeBadge kind={runtime.kind} />
          <span className="vira-lab-state">{state}</span>
          <span className="vira-lab-state packet">{packetState}</span>
        </div>
      </header>

      <section className="vira-lab-boundary" aria-label="Read-only safety boundary">
        <strong>Read-only browser lab</strong>
        <span>Deterministic fixture · no live SOLIDWORKS · no PDM · no save or mutation</span>
      </section>

      <nav className="vira-lab-stages" aria-label="VIRA Lab workflow">
        <div className={state === "ready" ? "complete" : "active"}><span>01</span><strong>Context</strong><small>{state}</small></div>
        <div className={packetComplete ? "complete" : ["packet-loading", "ready-to-compare", "evaluating"].includes(packetState) ? "active" : ""}><span>02</span><strong>Packet</strong><small>{packetState}</small></div>
        <div className={packetComplete ? "complete" : comparisonActive ? "active" : ""}><span>03</span><strong>Compare</strong><small>{comparisonActive ? "evaluating" : packetComplete ? packetState : "waiting"}</small></div>
        <div className={packetComplete ? "active" : ""}><span>04</span><strong>Receipt</strong><small>{packetComplete ? "available" : "waiting"}</small></div>
      </nav>

      <section className="vira-lab-layout">
        <div className="vira-lab-main">
          <section className="vira-lab-panel context-panel">
            <div className="vira-lab-panel-heading">
              <div>
                <span>Current engineering context</span>
                <h2>Active Document Context</h2>
              </div>
              <button type="button" onClick={() => void loadContext()} disabled={state === "loading"}>
                {state === "loading" ? "Loading…" : "Reload fixture"}
              </button>
            </div>
            <ActiveDocumentContextCard context={context} />
          </section>

          <section className="vira-lab-panel packet-workflow-panel">
            <PacketReviewPanel context={context} onLifecycleEvent={handlePacketLifecycle} />
          </section>

          <section className="vira-lab-panel validation-panel">
            <div className="vira-lab-panel-heading">
              <div>
                <span>Runnable milestone</span>
                <h2>What you can validate now</h2>
              </div>
            </div>
            <div className="vira-lab-checks">
              <article>
                <strong>Surface isolation</strong>
                <p>VIRA Lab opens at <code>?mode=vira-lab</code> without replacing the existing assistant or execution board.</p>
              </article>
              <article>
                <strong>Local packet evidence</strong>
                <p>PDF parsing, page rendering, findings, and comparison stay in the browser with no upload or connector call.</p>
              </article>
              <article>
                <strong>Failure visibility</strong>
                <p>Invalid replacements retain the last successful packet, while cancellation clears only local packet state.</p>
              </article>
            </div>
          </section>
        </div>

        <aside className="vira-lab-side">
          <section className="vira-lab-panel session-panel">
            <div className="vira-lab-panel-heading">
              <div>
                <span>Execution trace</span>
                <h2>Session</h2>
              </div>
            </div>
            <dl>
              <div><dt>Session ID</dt><dd>{runtime.sessionId}</dd></div>
              <div><dt>Runtime</dt><dd>{runtimeLabels[runtime.kind]}</dd></div>
              <div><dt>Workbench state</dt><dd>{state}</dd></div>
              <div><dt>Packet state</dt><dd>{packetState}</dd></div>
              <div><dt>Packet pages</dt><dd>{packetSummary?.pageCount ?? 0}</dd></div>
              <div><dt>Packet findings</dt><dd>{packetSummary?.findingCount ?? 0}</dd></div>
              <div><dt>Mutation actions</dt><dd>{context.mutationActions}</dd></div>
              <div><dt>External systems</dt><dd>Not accessed</dd></div>
            </dl>
            <div className="vira-lab-export-actions" aria-label="VIRA Lab session exports">
              <button type="button" onClick={exportLabReceipt} disabled={!canExportReceipt}>Export VIRA receipt</button>
              <button type="button" onClick={exportDiagnostics}>Export diagnostics</button>
            </div>
            {exportStatus ? <p className="vira-lab-export-status" role="status">{exportStatus}</p> : null}
          </section>

          <section className="vira-lab-panel diagnostic-panel" aria-live="polite">
            <div className="vira-lab-panel-heading">
              <div>
                <span>Bounded local log</span>
                <h2>Latest diagnostic</h2>
              </div>
              <span className="diagnostic-count">{diagnostics.length}/20</span>
            </div>
            {latestDiagnostic ? (
              <div className={`diagnostic-entry ${latestDiagnostic.level}`}>
                <strong>{latestDiagnostic.code}</strong>
                <p>{latestDiagnostic.message}</p>
              </div>
            ) : (
              <p className="diagnostic-empty">No diagnostic event has been emitted.</p>
            )}
          </section>
        </aside>
      </section>
    </main>
  );
}
