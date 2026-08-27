import { useState } from "react";
import { MiniCadViewer } from "./MiniCadViewer";
import { useHardwareCadBinding } from "./useHardwareCadBinding";
import type { HardwareRecord } from "./hardwareCadTypes";

export function HardwareCadPanel({ apiBase }: { apiBase?: string }) {
  const { receipt, loading, error, bind } = useHardwareCadBinding(apiBase);
  const [partNumber, setPartNumber] = useState("91251A632");
  const [hwdNumber, setHwdNumber] = useState("HWD-TEST-001");

  const handleBind = async () => {
    const record: HardwareRecord = {
      hardwareRecordId: hwdNumber,
      hwdNumber,
      mcMasterPartNumber: partNumber,
      vendor: "MCM",
    };
    await bind(record);
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 12, padding: 12 }}>
      <div style={{ display: "flex", gap: 8, alignItems: "end" }}>
        <label style={labelStyle}>
          HWD #
          <input value={hwdNumber} onChange={e => setHwdNumber(e.target.value)} style={inputStyle} placeholder="HWD-..." />
        </label>
        <label style={labelStyle}>
          McMaster #
          <input value={partNumber} onChange={e => setPartNumber(e.target.value)} style={inputStyle} placeholder="91251A632" />
        </label>
        <button onClick={handleBind} disabled={loading || !partNumber.trim()} style={buttonStyle}>
          {loading ? "Binding…" : "Bind & Acquire CAD"}
        </button>
      </div>
      {error && <div style={errorStyle}>Error: {error}</div>}
      <MiniCadViewer receipt={receipt} glbUrl={receipt?.viewerDerivative?.glbContentAddressedPath} />
      {receipt && (
        <details style={{ fontSize: 11, color: "#94a3b8" }}>
          <summary>Receipt JSON</summary>
          <pre style={{ whiteSpace: "pre-wrap", wordBreak: "break-all", background: "#0f172a", padding: 8, borderRadius: 6 }}>{JSON.stringify(receipt, null, 2)}</pre>
        </details>
      )}
    </div>
  );
}

const labelStyle: React.CSSProperties = { display: "flex", flexDirection: "column", gap: 4, fontSize: 11, color: "#94a3b8" };
const inputStyle: React.CSSProperties = { padding: "6px 8px", borderRadius: 6, border: "1px solid #334155", background: "#0f172a", color: "#e2e8f0", minWidth: 140 };
const buttonStyle: React.CSSProperties = { padding: "8px 12px", borderRadius: 6, border: "none", background: "#0ea5e9", color: "#fff", fontWeight: 600, cursor: "pointer" };
const errorStyle: React.CSSProperties = { color: "#f87171", fontSize: 12, background: "#450a0a", padding: 8, borderRadius: 6 };
