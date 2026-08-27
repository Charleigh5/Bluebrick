import type { ActiveDocumentContext } from "./activeDocumentContext";

function stateLabel(state: ActiveDocumentContext["state"]): string {
  switch (state) {
    case "ready":
      return "Read-only context";
    case "no-document":
      return "No active document";
    case "loading":
      return "Reading context";
    case "error":
      return "Context error";
    default:
      return "Context unavailable";
  }
}

function yesNo(value: boolean | undefined): string {
  return value === undefined ? "Unknown" : value ? "Yes" : "No";
}

export function ActiveDocumentContextCard({ context }: { context: ActiveDocumentContext }) {
  const ready = context.state === "ready";

  return (
    <section
      className={`active-document-card ${context.state}`}
      aria-label="Active document context"
      aria-live="polite"
      data-context-state={context.state}
    >
      <div className="active-document-card-head">
        <div>
          <div className="active-document-card-title">Active Document</div>
          <div className="active-document-card-subtitle">{context.documentType}</div>
        </div>
        <span className="active-document-card-badge">{stateLabel(context.state)}</span>
      </div>

      <p className="active-document-card-message">{context.message}</p>

      {ready ? (
        <div className="active-document-card-metrics">
          <ContextMetric label="Document" value={context.titleHash} />
          <ContextMetric label="Unsaved changes" value={yesNo(context.isDirty)} />
          <ContextMetric label="Read-only" value={yesNo(context.isReadOnly)} />
          <ContextMetric label="Custom properties" value={String(context.customPropertyCount)} />
        </div>
      ) : null}

      <div className="active-document-card-boundary">
        Read-only snapshot · {context.mutationActions} mutation actions
      </div>
    </section>
  );
}

function ContextMetric({ label, value }: { label: string; value: string }) {
  return (
    <div className="active-document-card-metric">
      <span>{value}</span>
      <small>{label}</small>
    </div>
  );
}
