import { useEffect, useRef, useState } from "react";
import type { ViewerDerivative, VendorCadAcquisitionReceipt } from "./hardwareCadTypes";

type Props = {
  receipt: VendorCadAcquisitionReceipt | null;
  glbUrl?: string;
};

export function MiniCadViewer({ receipt, glbUrl }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [status, setStatus] = useState<string>("idle");

  useEffect(() => {
    if (!receipt?.viewerDerivative) {
      setStatus(receipt ? `status: ${receipt.status}` : "idle");
      return;
    }
    const d = receipt.viewerDerivative;
    setStatus(`GLB ${d.glbByteLength} bytes sha:${d.glbSha256.slice(0, 8)} converter:${d.converterId}@${d.converterVersion}`);
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = "#0f172a";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.strokeStyle = "#38bdf8";
    ctx.lineWidth = 1.5;
    ctx.strokeRect(8, 8, canvas.width - 16, canvas.height - 16);
    ctx.fillStyle = "#e2e8f0";
    ctx.font = "11px ui-monospace, monospace";
    ctx.fillText(`VIRA Mini CAD Viewer`, 14, 22);
    ctx.fillText(`Part: ${receipt.partNumber}`, 14, 36);
    ctx.fillText(`Source SHA: ${d.sourceSha256.slice(0, 12)}...`, 14, 50);
    ctx.fillText(`GLB SHA: ${d.glbSha256.slice(0, 12)}...`, 14, 64);
    ctx.fillText(d.isValid ? "GLB valid (gltf 2.0 magic 0x46546C67)" : "GLB invalid", 14, 78);
    if (glbUrl) ctx.fillText(`url: ${glbUrl.slice(0, 40)}`, 14, 92);
    if (d.warnings.length > 0) ctx.fillText(`warn: ${d.warnings[0].slice(0, 36)}`, 14, 106);
  }, [receipt, glbUrl]);

  if (!receipt) {
    return (
      <div className="mini-cad-viewer empty" style={containerStyle}>
        <div style={emptyStyle}>No hardware CAD binding</div>
        <div style={subStyle}>Bind a HardwareRecord → McMaster 3-D STEP → GLB</div>
      </div>
    );
  }

  const derivative: ViewerDerivative | undefined = receipt.viewerDerivative;
  const hasGlb = Boolean(derivative?.isValid);

  return (
    <div className="mini-cad-viewer" style={containerStyle} data-testid="mini-cad-viewer" data-status={receipt.status} data-cache-hit={String(receipt.cacheHit)}>
      <div style={headerStyle}>
        <span style={badgeStyle}>Mini CAD Viewer</span>
        <span style={chipStyle}>{receipt.status}</span>
        {receipt.cacheHit && <span style={chipStyle}>cache hit</span>}
      </div>
      <canvas ref={canvasRef} width={360} height={160} style={canvasStyle} aria-label="Mini CAD Viewer canvas" />
      <div style={metaStyle}>
        <div>Part: <code>{receipt.partNumber}</code></div>
        {receipt.binding && <div>CAD link: <code>{receipt.binding.authoritativeCadLink}</code></div>}
        {receipt.asset && <div>SHA256: <code>{receipt.asset.sha256.slice(0, 16)}…</code> · {receipt.asset.byteLength} bytes</div>}
        {derivative && <div>Derivative: <code>{derivative.converterId}@{derivative.converterVersion}</code> · {derivative.glbByteLength} bytes · valid: {String(derivative.isValid)}</div>}
        {receipt.limitations.length > 0 && <div style={warnStyle}>Limitations: {receipt.limitations.join("; ")}</div>}
        {receipt.errors.length > 0 && <div style={errorStyle}>Errors: {receipt.errors.map(e => `${e.code}: ${e.message}`).join("; ")}</div>}
      </div>
      <div style={footerStyle}>
        <span>Source: McMaster Product Links[&quot;3-D STEP&quot;] (authoritative)</span>
        {hasGlb && glbUrl && <a href={glbUrl} target="_blank" rel="noreferrer" style={linkStyle}>Open GLB</a>}
      </div>
    </div>
  );
}

const containerStyle: React.CSSProperties = { border: "1px solid #1e293b", borderRadius: 8, padding: 12, background: "#020617", color: "#e2e8f0", fontFamily: "ui-sans-system, system-ui, sans-serif", maxWidth: 400 };
const headerStyle: React.CSSProperties = { display: "flex", gap: 8, alignItems: "center", marginBottom: 8 };
const badgeStyle: React.CSSProperties = { background: "#0ea5e9", color: "#fff", padding: "2px 6px", borderRadius: 4, fontSize: 12, fontWeight: 600 };
const chipStyle: React.CSSProperties = { background: "#1e293b", color: "#94a3b8", padding: "2px 6px", borderRadius: 4, fontSize: 11 };
const canvasStyle: React.CSSProperties = { width: "100%", borderRadius: 6, border: "1px solid #1e293b", display: "block" };
const metaStyle: React.CSSProperties = { marginTop: 8, fontSize: 11, lineHeight: 1.5, color: "#94a3b8" };
const warnStyle: React.CSSProperties = { color: "#f59e0b" };
const errorStyle: React.CSSProperties = { color: "#f87171" };
const footerStyle: React.CSSProperties = { marginTop: 8, display: "flex", justifyContent: "space-between", fontSize: 10, color: "#64748b" };
const linkStyle: React.CSSProperties = { color: "#38bdf8", textDecoration: "underline" };
const emptyStyle: React.CSSProperties = { fontWeight: 600, fontSize: 13 };
const subStyle: React.CSSProperties = { fontSize: 11, color: "#64748b", marginTop: 4 };
