import { useMemo, useState } from "react";
import { isAllowedLocalRelayUrl, routeEngineeringQueryViaLocalRelay } from "./adapters/LocalRelayQueryAdapter";
import { capabilityDecisions, sourceAssets, type ActionPlan, type CapabilityState } from "./fixtures";
import { routeEngineeringQuery, serializeExecutionReceipt, type EngineeringQueryRoute } from "./routing";
import "./execution-board.css";

const sampleRequests = [
  "BB-SRC-1001",
  "Route a SOLIDWORKS metadata request safely",
  "Find PDM part 12345",
  "Show Epicor status for an engineering request"
];

function stateClass(state: CapabilityState) {
  return state.toLowerCase().replace(/_/g, "-");
}

function downloadReceipt(route: EngineeringQueryRoute) {
  const blob = new Blob([serializeExecutionReceipt(route.receipt)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `${route.receipt.id}.json`;
  anchor.click();
  URL.revokeObjectURL(url);
}

function CapabilityPill({ state }: { state: CapabilityState }) {
  return <span className={`eb-pill ${stateClass(state)}`}>{state}</span>;
}

function ActionPreview({ action }: { action: ActionPlan }) {
  return (
    <article className="eb-card action-preview">
      <div className="eb-card-head">
        <div>
          <span className="eb-kicker">{action.previewType}</span>
          <h3>{action.title}</h3>
        </div>
        <span className="eb-lock">{action.executionState}</span>
      </div>
      <ol>
        {action.steps.map((step) => (
          <li key={step}>{step}</li>
        ))}
      </ol>
      <div className="eb-blocked">
        <strong>Blocked live systems</strong>
        <span>{action.blockedLiveSystems.length ? action.blockedLiveSystems.join(", ") : "None. Local fixture only."}</span>
      </div>
    </article>
  );
}

export function ExecutionBoardApp() {
  const params = new URLSearchParams(window.location.search);
  const relayUrl = params.get("relayUrl") ?? "";
  const useLocalRelay = params.get("relay") === "local" && isAllowedLocalRelayUrl(relayUrl);
  const [request, setRequest] = useState(sampleRequests[0]);
  const [route, setRoute] = useState<EngineeringQueryRoute>(() => routeEngineeringQuery(sampleRequests[0]));
  const stateCounts = useMemo(
    () =>
      capabilityDecisions.reduce<Record<CapabilityState, number>>(
        (acc, capability) => {
          acc[capability.state] += 1;
          return acc;
        },
        { MOCK: 0, LOCAL: 0, NOT_CONNECTED: 0, READ_ONLY: 0, APPROVAL_REQUIRED: 0 }
      ),
    []
  );

  const routeRequest = async (nextRequest: string) => {
    const nextRoute = useLocalRelay
      ? await routeEngineeringQueryViaLocalRelay(nextRequest, relayUrl)
      : routeEngineeringQuery(nextRequest);
    setRoute(nextRoute);
  };

  const submit = (event?: { preventDefault: () => void }) => {
    event?.preventDefault();
    void routeRequest(request);
  };

  return (
    <main className="execution-board-shell">
      <header className="eb-header">
        <div>
          <span className="eb-kicker">Level 1/2 local prototype</span>
          <h1>Engineering Query Sandbox</h1>
          <p>
            Route engineering intent through exact IDs, local evidence fixtures, capability states, action previews, and auditable receipts without
            touching live CAD, PDM, Epicor, Salesforce, secrets, or production systems.
          </p>
        </div>
        <div className="eb-receipt-summary" aria-label="Current receipt boundary">
          <strong>{route.receipt.id}</strong>
          <span>{useLocalRelay ? "localhost-relay" : route.mode}</span>
          <span>No external systems accessed</span>
        </div>
      </header>

      <section className="eb-command-band" aria-label="Engineering request routing">
        <form onSubmit={submit}>
          <label htmlFor="engineering-query">Natural-language engineering request or exact ID</label>
          <div className="eb-command-row">
            <input
              id="engineering-query"
              value={request}
              onChange={(event) => setRequest(event.target.value)}
              placeholder="Try BB-SRC-1001 or ask for a PDM/SOLIDWORKS action"
            />
            <button type="submit">Route request</button>
          </div>
        </form>
        <div className="eb-samples" aria-label="Sample requests">
          {sampleRequests.map((sample) => (
            <button
              key={sample}
              type="button"
              onClick={() => {
                setRequest(sample);
                void routeRequest(sample);
              }}
            >
              {sample}
            </button>
          ))}
        </div>
      </section>

      <section className="eb-state-grid" aria-label="Capability state summary">
        {(Object.keys(stateCounts) as CapabilityState[]).map((state) => (
          <div className="eb-state" key={state}>
            <CapabilityPill state={state} />
            <strong>{stateCounts[state]}</strong>
          </div>
        ))}
      </section>

      <section className="eb-layout">
        <div className="eb-main-stack">
          <section className="eb-section">
            <div className="eb-section-title">
              <h2>Sources and Evidence</h2>
              <span>{route.sources.length + route.evidence.length} local cards</span>
            </div>
            <div className="eb-card-grid">
              {route.sources.map((source) => (
                <article className="eb-card" key={source.id}>
                  <div className="eb-card-head">
                    <div>
                      <span className="eb-kicker">{source.id}</span>
                      <h3>{source.title}</h3>
                    </div>
                    <CapabilityPill state={source.state} />
                  </div>
                  <div className="eb-provenance">Authority: {source.authority}</div>
                  <p>{source.summary}</p>
                  <code>{source.path}</code>
                </article>
              ))}
              {route.evidence.map((evidence) => (
                <article className="eb-card evidence" key={evidence.id}>
                  <span className="eb-kicker">{evidence.id}</span>
                  <h3>{evidence.title}</h3>
                  <div className="eb-provenance">Source: {evidence.sourceAssetId} / Authority: {evidence.authority}</div>
                  <p>{evidence.quote}</p>
                </article>
              ))}
              {!route.sources.length && !route.evidence.length ? <div className="eb-empty">No source or evidence card matched. See data gaps.</div> : null}
            </div>
          </section>

          <section className="eb-section">
            <div className="eb-section-title">
              <h2>Claims</h2>
              <span>{route.claims.length} routed</span>
            </div>
            <div className="eb-card-grid compact">
              {route.claims.map((claim) => (
                <article className="eb-card claim" key={claim.id}>
                  <span className="eb-kicker">{claim.id} / {claim.confidence}</span>
                  <h3>{claim.title}</h3>
                  <div className="eb-provenance">Authority: {claim.authority} / Evidence: {claim.evidenceIds.join(", ")}</div>
                  <p>{claim.statement}</p>
                </article>
              ))}
              {!route.claims.length ? <div className="eb-empty">No promoted claim was created from this request.</div> : null}
            </div>
          </section>

          <section className="eb-section">
            <div className="eb-section-title">
              <h2>Typed Action Preview</h2>
              <span>Preview only</span>
            </div>
            <div className="eb-card-grid compact">
              {route.actions.map((action) => (
                <ActionPreview action={action} key={action.id} />
              ))}
              {!route.actions.length ? <div className="eb-empty">No action preview is available for this route.</div> : null}
            </div>
          </section>
        </div>

        <aside className="eb-side-stack">
          <section className="eb-section">
            <div className="eb-section-title">
              <h2>Capability Routing</h2>
              <span>{route.capabilities.length} states</span>
            </div>
            <div className="eb-capabilities">
              {route.capabilities.map((capability) => (
                <article className="eb-capability" key={capability.id}>
                  <div>
                    <strong>{capability.label}</strong>
                    <span>{capability.reason}</span>
                  </div>
                  <CapabilityPill state={capability.state} />
                  <small>{capability.approvalBoundary}</small>
                </article>
              ))}
            </div>
          </section>

          <section className="eb-section">
            <div className="eb-section-title">
              <h2>Receipt Viewer</h2>
              <button type="button" onClick={() => downloadReceipt(route)}>Export JSON</button>
            </div>
            <pre className="eb-receipt">{serializeExecutionReceipt(route.receipt)}</pre>
          </section>

          <section className="eb-section">
            <div className="eb-section-title">
              <h2>Data Gaps</h2>
              <span>{route.gaps.length}</span>
            </div>
            <ul className="eb-gaps">
              {(route.gaps.length ? route.gaps : ["No route gaps for this local fixture result."]).map((gap) => (
                <li key={gap}>{gap}</li>
              ))}
            </ul>
          </section>

          <section className="eb-section">
            <div className="eb-section-title">
              <h2>Fixture Index</h2>
              <span>{sourceAssets.length} sources</span>
            </div>
            <div className="eb-index">
              {sourceAssets.map((source) => (
                <button
                  key={source.id}
                  type="button"
                  onClick={() => {
                    setRequest(source.id);
                    void routeRequest(source.id);
                  }}
                >
                  <strong>{source.id}</strong>
                  <span>{source.title}</span>
                </button>
              ))}
            </div>
          </section>
        </aside>
      </section>
    </main>
  );
}
