import { useCallback, useEffect, useRef, useState } from "react";
import { GlobalWorkerOptions, getDocument, type PDFDocumentProxy } from "pdfjs-dist";
// Keep the legacy add-in package to its fixed index/CSS/JS triplet. Vite
// embeds the PDF worker URL in assistant-web.js instead of emitting a fourth
// dist member that the WebView host would not package.
import pdfWorkerUrl from "pdfjs-dist/build/pdf.worker.min.mjs?url&inline";
import type { ActiveDocumentContext } from "../activeDocumentContext";
import { comparePacketEvidenceToActiveDocument, type PhaseAComparison } from "../cad-compare/phaseAComparison";
import { comparePacketBomToAssembly, type PhaseBComparisonReport } from "../cad-compare/phaseBComparison";
import {
  analyzePacketPages,
  buildPacketReviewReceipt,
  comparePacketToActiveDocument,
  createPacketReviewReport,
  projectPacketEvidenceV2,
  type PacketContextComparison,
  type PacketEvidence,
  type PacketReview
} from "./packetReview";
import {
  sanitizePacketFileName,
  type PacketReviewLifecycleEvent,
  type ViraPacketWorkflowState
} from "../vira-lab/packetWorkflow";

GlobalWorkerOptions.workerSrc = pdfWorkerUrl;

type ReviewState = "idle" | "loading" | "ready" | "error";

function saveDerivedFile(fileName: string, content: string, contentType: string): string {
  const url = URL.createObjectURL(new Blob([content], { type: contentType }));
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
  return fileName;
}

function safeBaseName(fileName: string): string {
  return fileName.replace(/\.pdf$/i, "").replace(/[^a-z0-9_-]+/gi, "-").replace(/^-+|-+$/g, "") || "packet";
}

function evidenceLabel(item: PacketEvidence): string {
  return `p.${item.pageNumber} · ${item.text}`;
}

export function PacketReviewPanel({
  context,
  onLifecycleEvent
}: {
  context: ActiveDocumentContext;
  onLifecycleEvent?: (event: PacketReviewLifecycleEvent) => void;
}) {
  const [state, setState] = useState<ReviewState>("idle");
  const [error, setError] = useState("");
  const [review, setReview] = useState<PacketReview | null>(null);
  const [comparison, setComparison] = useState<PacketContextComparison | null>(null);
  const [phaseAComparison, setPhaseAComparison] = useState<PhaseAComparison | null>(null);
  const [phaseBComparison, setPhaseBComparison] = useState<PhaseBComparisonReport | null>(null);
  const [phaseBFilter, setPhaseBFilter] = useState<"all" | "issues" | "unresolved">("all");
  const [document, setDocument] = useState<PDFDocumentProxy | null>(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [outputStatus, setOutputStatus] = useState("");
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const pageFrameRef = useRef<HTMLDivElement | null>(null);
  const loadGenerationRef = useRef(0);

  const emitLifecycle = useCallback((
    state: ViraPacketWorkflowState,
    message: string,
    nextReview: PacketReview | null,
    retainsEvidence = Boolean(nextReview),
    nextPhaseA: PhaseAComparison | null = null,
    nextPhaseB: PhaseBComparisonReport | null = null
  ) => {
    onLifecycleEvent?.({
      state,
      fileName: sanitizePacketFileName(nextReview?.fileName ?? ""),
      message,
      retainsEvidence,
      pageCount: nextReview?.pageCount ?? 0,
      findingCount: nextReview?.findings.length ?? 0,
      phaseAStatus: nextPhaseA?.identity.authority ?? "",
      phaseBComparisonCount: nextPhaseB?.comparisons.length ?? 0
    });
  }, [onLifecycleEvent]);

  useEffect(() => () => {
    void document?.cleanup();
  }, [document]);

  useEffect(() => {
    if (!review) return;
    let cancelled = false;
    const packetProjection = projectPacketEvidenceV2(review);
    emitLifecycle("evaluating", "Evaluating packet evidence against the active-document fixture.", review, true);
    void Promise.allSettled([
      comparePacketToActiveDocument(review, context),
      packetProjection.then((packet) => comparePacketEvidenceToActiveDocument(packet, context)),
      packetProjection.then((packet) => comparePacketBomToAssembly(packet, context))
    ]).then(([contextResult, phaseAResult, phaseBResult]) => {
      if (cancelled) return;
      const nextComparison = contextResult.status === "fulfilled" ? contextResult.value : null;
      const nextPhaseA = phaseAResult.status === "fulfilled" ? phaseAResult.value : null;
      const nextPhaseB = phaseBResult.status === "fulfilled" ? phaseBResult.value : null;
      if (nextComparison) setComparison(nextComparison);
      if (nextPhaseA) setPhaseAComparison(nextPhaseA);
      if (nextPhaseB) setPhaseBComparison(nextPhaseB);

      const failures = [contextResult, phaseAResult, phaseBResult].filter((result) => result.status === "rejected");
      if (failures.length) {
        const message = `${failures.length} comparison stage${failures.length === 1 ? "" : "s"} failed; packet evidence was retained.`;
        setError(message);
        emitLifecycle("partial", message, review, true, nextPhaseA, nextPhaseB);
        return;
      }

      const incompleteEvidence =
        context.state !== "ready" ||
        context.assemblyPayloadStatus !== "ok" ||
        context.assemblyTraversal.truncated ||
        Boolean(nextPhaseB?.limitations.length);
      emitLifecycle(
        incompleteEvidence ? "partial" : "complete",
        incompleteEvidence
          ? "Packet review completed with explicit incomplete CAD evidence."
          : "Packet review and Phase A/B comparison completed.",
        review,
        true,
        nextPhaseA,
        nextPhaseB
      );
    });
    return () => { cancelled = true; };
  }, [context, emitLifecycle, review]);

  useEffect(() => {
    const canvas = canvasRef.current;
    const frame = pageFrameRef.current;
    if (!document || !canvas || !frame) return;
    let cancelled = false;
    let renderTask: { cancel: () => void; promise: Promise<unknown> } | null = null;
    void document.getPage(pageNumber).then((page) => {
      if (cancelled) return;
      const base = page.getViewport({ scale: 1 });
      const availableWidth = Math.max(240, frame.clientWidth - 18);
      const scale = Math.min(1.5, availableWidth / base.width);
      const viewport = page.getViewport({ scale });
      const ratio = window.devicePixelRatio || 1;
      canvas.width = Math.floor(viewport.width * ratio);
      canvas.height = Math.floor(viewport.height * ratio);
      canvas.style.width = `${Math.floor(viewport.width)}px`;
      canvas.style.height = `${Math.floor(viewport.height)}px`;
      const context2d = canvas.getContext("2d");
      if (!context2d) return;
      context2d.setTransform(ratio, 0, 0, ratio, 0, 0);
      renderTask = page.render({ canvas, canvasContext: context2d, viewport });
      return renderTask.promise;
    }).catch((reason) => {
      if (!cancelled && reason?.name !== "RenderingCancelledException") setError(`Page render failed: ${reason instanceof Error ? reason.message : String(reason)}`);
    });
    return () => {
      cancelled = true;
      renderTask?.cancel();
    };
  }, [document, pageNumber]);

  const openEvidence = (targetPage: number) => {
    setPageNumber(Math.max(1, Math.min(review?.pageCount || 1, targetPage)));
    pageFrameRef.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  };

  const loadPacket = async (file?: File) => {
    if (!file) {
      emitLifecycle("cancelled", "Packet selection was cancelled.", review, Boolean(review), phaseAComparison, phaseBComparison);
      return;
    }
    const loadGeneration = ++loadGenerationRef.current;
    const safeFileName = sanitizePacketFileName(file.name);
    setState("loading");
    setError("");
    setOutputStatus("");
    setPageNumber(1);
    emitLifecycle("packet-loading", `Loading ${safeFileName} locally.`, review, Boolean(review), phaseAComparison, phaseBComparison);
    try {
      if (file.type && file.type !== "application/pdf" && !file.name.toLowerCase().endsWith(".pdf")) {
        throw new Error("Choose a PDF packet.");
      }
      if (file.size > 150 * 1024 * 1024) throw new Error("PDF exceeds the 150 MB local review limit.");
      const bytes = new Uint8Array(await file.arrayBuffer());
      const nextDocument = await getDocument({ data: bytes }).promise;
      const pages = [];
      for (let index = 1; index <= nextDocument.numPages; index += 1) {
        const page = await nextDocument.getPage(index);
        const content = await page.getTextContent();
        const text = content.items
          .map((item) => ("str" in item ? item.str : ""))
          .filter(Boolean)
          .join("\n");
        pages.push({ pageNumber: index, text });
      }
      if (loadGenerationRef.current !== loadGeneration) {
        await nextDocument.cleanup();
        return;
      }
      const nextReview = analyzePacketPages(file.name, pages);
      const nextComparison = await comparePacketToActiveDocument(nextReview, context);
      if (loadGenerationRef.current !== loadGeneration) {
        await nextDocument.cleanup();
        return;
      }
      if (document) await document.cleanup();
      setDocument(nextDocument);
      setReview(nextReview);
      setComparison(nextComparison);
      setPhaseAComparison(null);
      setPhaseBComparison(null);
      setState("ready");
      emitLifecycle("ready-to-compare", "Packet evidence is ready for Phase A/B evaluation.", nextReview, true, null, null);
    } catch (reason) {
      if (loadGenerationRef.current !== loadGeneration) return;
      const message = reason instanceof Error ? reason.message : String(reason);
      const retainsEvidence = Boolean(review && comparison);
      setState(retainsEvidence ? "ready" : "error");
      setError(message);
      emitLifecycle(retainsEvidence ? "partial" : "failed", message, review, retainsEvidence, phaseAComparison, phaseBComparison);
    }
  };

  const cancelPacket = async () => {
    loadGenerationRef.current += 1;
    await document?.cleanup();
    setDocument(null);
    setReview(null);
    setComparison(null);
    setPhaseAComparison(null);
    setPhaseBComparison(null);
    setState("idle");
    setError("");
    setOutputStatus("");
    setPageNumber(1);
    emitLifecycle("cancelled", "Packet review was cancelled and local packet state was cleared.", null, false, null, null);
  };

  const exportReport = () => {
    if (!review || !comparison) return;
    const fileName = saveDerivedFile(`${safeBaseName(review.fileName)}-vira-review.md`, createPacketReviewReport(review, comparison), "text/markdown;charset=utf-8");
    setOutputStatus(`Derived output ready: ${fileName}`);
  };

  const exportReceipt = () => {
    if (!review || !comparison) return;
    const fileName = saveDerivedFile(`${safeBaseName(review.fileName)}-vira-receipt.json`, `${JSON.stringify(buildPacketReviewReceipt(review, comparison), null, 2)}\n`, "application/json;charset=utf-8");
    setOutputStatus(`Derived output ready: ${fileName}`);
  };

  const phaseBRows = phaseBComparison?.comparisons.filter((item) => {
    if (phaseBFilter === "unresolved") return item.status === "unresolved-evidence";
    if (phaseBFilter === "issues") return !["exact-match", "not-applicable"].includes(item.status);
    return true;
  }) ?? [];

  return (
    <section className={`packet-review-panel ${state}`} aria-labelledby="packet-review-title">
      <div className="packet-review-head">
        <div>
          <span className="eyebrow">Reviewer expert mode</span>
          <h2 id="packet-review-title">Drawing &amp; Packet Reviewer</h2>
        </div>
        <span className="local-only">Local file only</span>
      </div>

      <label className="packet-file-picker">
        <span>{state === "loading" ? "Analyzing packet…" : review ? "Replace PDF packet" : "Load engineering PDF packet"}</span>
        <input
          type="file"
          accept="application/pdf,.pdf"
          aria-label="Choose engineering PDF packet"
          disabled={state === "loading"}
          onChange={(event) => {
            void loadPacket(event.currentTarget.files?.[0]);
            event.currentTarget.value = "";
          }}
        />
      </label>

      {state === "loading" || review ? (
        <button type="button" className="packet-cancel-button" onClick={() => void cancelPacket()}>
          {state === "loading" ? "Cancel packet load" : "Clear packet"}
        </button>
      ) : null}

      {state === "idle" ? (
        <p className="packet-review-guidance">Select a local PDF to index page text, drawing identifiers, title-block evidence, BOM rows, dimensions, notes, and review findings. Nothing is uploaded and no engineering data is modified.</p>
      ) : null}
      {state === "loading" ? <div className="packet-review-status" role="status">Extracting page evidence and rendering the packet locally…</div> : null}
      {error ? <div className="packet-review-error" role="alert">{error || "Packet analysis failed."}</div> : null}

      {review && comparison ? (
        <div className="packet-review-workbench">
          <div className="packet-summary" aria-label="Packet evidence summary">
            <span><strong>{review.pageCount}</strong> pages</span>
            <span><strong>{review.partNumbers.length}</strong> identifiers</span>
            <span><strong>{review.bomRecords.length}</strong> BOM rows</span>
            <span><strong>{review.findings.length}</strong> findings</span>
          </div>

          <article className={`packet-comparison ${comparison.status}`} aria-live="polite">
            <div><span className="eyebrow">Active document comparison</span><strong>{comparison.status.replace("-", " ")}</strong></div>
            <p>{comparison.summary}</p>
          </article>

          {phaseAComparison ? (
            <details className="packet-section packet-cad-phase-a" open>
              <summary>Packet ↔ CAD Phase A <span>Static build · read only</span></summary>
              <div className="packet-cad-identity">
                <div>
                  <span className="eyebrow">Identity authority</span>
                  <strong>{phaseAComparison.identity.authority.replace("-", " ")}</strong>
                </div>
                <small>
                  {phaseAComparison.identity.matchSources.length
                    ? phaseAComparison.identity.matchSources.join(" + ").replaceAll("_", " ")
                    : "No independent identity match source"}
                </small>
              </div>
              <div className="packet-table-wrap">
                <table aria-label="Packet to CAD Phase A property comparisons">
                  <thead><tr><th>Property</th><th>Status</th><th>PDF evidence</th><th>CAD evidence</th></tr></thead>
                  <tbody>{phaseAComparison.properties.map((item) => (
                    <tr key={item.comparisonId}>
                      <td>{item.canonicalField.replaceAll("_", " ")}</td>
                      <td><span className={`packet-cad-status ${item.status}`}>{item.status.replaceAll("-", " ")}</span></td>
                      <td>{item.packetEvidence.length}</td>
                      <td>{item.cadEvidence.length}</td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
              <p className="packet-cad-boundary">Digest-only CAD evidence. Zero CAD mutations, external requests, rebuilds, saves, or configuration changes.</p>
            </details>
          ) : null}

          {phaseBComparison ? (
            <details className="packet-section packet-cad-phase-b" open>
              <summary>Packet ↔ CAD Phase B <span>BOM ↔ active assembly</span></summary>
              <div className="packet-phase-b-score" aria-label="Phase B match scorecard">
                <span><small>Precision</small><strong>{Math.round(phaseBComparison.scorecard.precision * 100)}%</strong></span>
                <span><small>Recall</small><strong>{Math.round(phaseBComparison.scorecard.recall * 100)}%</strong></span>
                <span><small>CAD records</small><strong>{context.componentEvidence.length}</strong></span>
                <span><small>Component payload</small><strong>{context.assemblyPayloadStatus}</strong></span>
              </div>
              <div className="packet-phase-b-filters" role="group" aria-label="Filter Phase B findings">
                <button type="button" aria-pressed={phaseBFilter === "all"} onClick={() => setPhaseBFilter("all")}>All</button>
                <button type="button" aria-pressed={phaseBFilter === "issues"} onClick={() => setPhaseBFilter("issues")}>Issues only</button>
                <button type="button" aria-pressed={phaseBFilter === "unresolved"} onClick={() => setPhaseBFilter("unresolved")}>Unresolved only</button>
              </div>
              {context.assemblyPayloadStatus !== "ok" || context.assemblyTraversal.truncated ? (
                <p className="packet-cad-boundary" role="status">
                  Assembly evidence is incomplete: payload {context.assemblyPayloadStatus}{context.assemblyTraversal.truncated ? "; traversal limit reached" : ""}. No missing-component claim is authoritative from incomplete evidence.
                </p>
              ) : null}
              <div className="packet-table-wrap">
                <table aria-label="Packet BOM to active assembly comparisons">
                  <thead><tr><th>Identifier</th><th>Check</th><th>Status</th><th>PDF</th><th>CAD</th></tr></thead>
                  <tbody>{phaseBRows.map((item) => (
                    <tr key={item.comparisonId}>
                      <td>{item.identifier}</td>
                      <td>{item.category.replace("bom:", "")}</td>
                      <td><span className={`packet-cad-status ${item.status}`}>{item.status.replaceAll("-", " ")}</span></td>
                      <td>{item.packetEvidence.length}</td>
                      <td>{item.cadEvidence.length}</td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
              {!phaseBRows.length ? <p className="packet-cad-boundary">No comparisons match this filter.</p> : null}
              <p className="packet-cad-boundary">Hash-redacted component evidence only. Suppressed, lightweight, unloaded, and missing references remain visible and are never resolved automatically.</p>
            </details>
          ) : null}

          <div className="packet-page-toolbar" aria-label="Packet page navigation">
            <button type="button" disabled={pageNumber <= 1} onClick={() => setPageNumber((value) => Math.max(1, value - 1))}>Previous</button>
            <span>Page {pageNumber} of {review.pageCount}</span>
            <button type="button" disabled={pageNumber >= review.pageCount} onClick={() => setPageNumber((value) => Math.min(review.pageCount, value + 1))}>Next</button>
          </div>
          <div className="packet-page-frame" ref={pageFrameRef} aria-label={`Rendered packet page ${pageNumber}`}>
            <canvas ref={canvasRef} />
          </div>

          <div className="packet-derived-actions" aria-label="Derived review outputs">
            <button type="button" onClick={exportReport}>Export report</button>
            <button type="button" onClick={exportReceipt}>Export receipt</button>
          </div>
          {outputStatus ? <div className="packet-output-status" role="status">{outputStatus}</div> : null}

          <details className="packet-section" open>
            <summary>Review findings <span>{review.findings.length}</span></summary>
            <div className="packet-finding-list">
              {review.findings.map((finding) => (
                <article key={finding.code} className={`packet-finding ${finding.severity}`}>
                  <div><strong>{finding.title}</strong><span>{finding.severity}</span></div>
                  <p>{finding.summary}</p>
                  {finding.evidence.map((item, index) => <button type="button" key={`${finding.code}-${index}`} onClick={() => openEvidence(item.pageNumber)}>{evidenceLabel(item)}</button>)}
                  <small>{finding.recommendedAction}</small>
                </article>
              ))}
            </div>
          </details>

          <details className="packet-section">
            <summary>Title blocks &amp; drawing groups <span>{review.titleBlocks.length + review.drawingGroups.length}</span></summary>
            <div className="packet-evidence-list">
              {review.titleBlocks.map((item, index) => <button type="button" key={`title-${index}`} onClick={() => openEvidence(item.pageNumber)}>{evidenceLabel(item)}</button>)}
              {review.drawingGroups.map((group) => <div key={group.label}><strong>{group.label}</strong><span>{group.identifiers.join(", ")}</span></div>)}
            </div>
          </details>

          <details className="packet-section">
            <summary>BOM records <span>{review.bomRecords.length}</span></summary>
            <div className="packet-table-wrap">
              <table>
                <thead><tr><th>Page</th><th>Item</th><th>Qty</th><th>Part number</th><th>Description</th></tr></thead>
                <tbody>{review.bomRecords.map((row, index) => <tr key={`${row.pageNumber}-${row.item}-${index}`}><td><button type="button" onClick={() => openEvidence(row.pageNumber)}>{row.pageNumber}</button></td><td>{row.item}</td><td>{row.quantity}</td><td>{row.partNumber}</td><td>{row.description}</td></tr>)}</tbody>
              </table>
            </div>
          </details>

          <details className="packet-section">
            <summary>Dimensions &amp; notes <span>{review.dimensions.length + review.notes.length}</span></summary>
            <div className="packet-evidence-list">
              {[...review.dimensions, ...review.notes].map((item, index) => <button type="button" key={`${item.category}-${index}`} onClick={() => openEvidence(item.pageNumber)}>{evidenceLabel(item)}</button>)}
            </div>
          </details>

          {review.questions.length ? (
            <details className="packet-section">
              <summary>Review questions <span>{review.questions.length}</span></summary>
              <ul>{review.questions.map((question) => <li key={question}>{question}</li>)}</ul>
            </details>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
