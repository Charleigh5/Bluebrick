import { useCallback, useState } from "react";
import type { HardwareRecord, VendorCadAcquisitionReceipt } from "./hardwareCadTypes";

export function useHardwareCadBinding(apiBase: string = "") {
  const [receipt, setReceipt] = useState<VendorCadAcquisitionReceipt | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const bind = useCallback(async (record: HardwareRecord) => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBase}/hardware/cad-binding`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(record),
      });
      if (!response.ok) {
        const text = await response.text();
        throw new Error(`${response.status} ${text.slice(0, 300)}`);
      }
      const data = (await response.json()) as VendorCadAcquisitionReceipt;
      setReceipt(data);
      return data;
    } catch (e) {
      const message = e instanceof Error ? e.message : String(e);
      setError(message);
      throw e;
    } finally {
      setLoading(false);
    }
  }, [apiBase]);

  return { receipt, loading, error, bind, setReceipt };
}
