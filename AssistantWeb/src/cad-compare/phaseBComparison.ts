import type { ActiveDocumentComponentDigest, ActiveDocumentContext } from "../activeDocumentContext";
import type { PacketEvidenceRefV2, PacketEvidenceSnapshotV2 } from "../packet-review/packetReview";

export type PhaseBStatus = "exact-match" | "conflict" | "missing-in-cad" | "missing-in-pdf" | "duplicate" | "quantity-conflict" | "hierarchy-conflict" | "configuration-gap" | "unresolved-evidence" | "not-applicable";

export type PhaseBComparison = {
  comparisonId: string;
  category: string;
  status: PhaseBStatus;
  identifier: string;
  packetEvidence: PacketEvidenceRefV2[];
  cadEvidence: Array<{ snapshotId: string; nativePathHash: string; identifierHash: string }>;
  authoritative: boolean;
  ruleId: string;
  limitations: string[];
};

export type PhaseBComparisonReport = {
  schemaVersion: "vira.packet-cad.phase-b.v1";
  comparisons: PhaseBComparison[];
  scorecard: { packetControlledIdentifiers: number; cadControlledIdentifiers: number; matchedIdentifiers: number; precision: number; recall: number };
  limitations: string[];
  mutationActions: 0;
  externalSystemsAccessed: false;
};

async function hash(value: string): Promise<string> {
  const bytes = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
  const hex = [...new Uint8Array(bytes)].map((item) => item.toString(16).padStart(2, "0")).join("");
  return `value_sha256:${hex.slice(0, 16)}`;
}

function normalize(value: string): string {
  return value.trim().toUpperCase().replace(/\.(?:SLDASM|SLDPRT|SLDDRW|PDF)$/i, "").replaceAll("_", "-").replaceAll(" ", "");
}

function cadRefs(items: ActiveDocumentComponentDigest[]): PhaseBComparison["cadEvidence"] {
  return items.map((item) => ({ snapshotId: item.snapshotId, nativePathHash: item.nativePathHash, identifierHash: item.identifierHash }));
}

function finding(identifier: string, category: string, status: PhaseBStatus, packetEvidence: PacketEvidenceRefV2[], cad: ActiveDocumentComponentDigest[], limitations: string[] = []): PhaseBComparison {
  return {
    comparisonId: `CMP-B-${category.toUpperCase().replaceAll(":", "-")}-${identifier}`,
    category,
    status,
    identifier,
    packetEvidence,
    cadEvidence: cadRefs(cad),
    authoritative: status !== "unresolved-evidence",
    ruleId: `VIRA-BOM-${category.split(":").at(-1)?.toUpperCase()}-001`,
    limitations
  };
}

export async function comparePacketBomToAssembly(packet: PacketEvidenceSnapshotV2, context: ActiveDocumentContext): Promise<PhaseBComparisonReport> {
  const comparisons: PhaseBComparison[] = [];
  const controlledRows = packet.bomRows.filter((row) => ["Controlled", "Unknown"].includes(row.responsibility));
  for (const row of packet.bomRows.filter((item) => !["Controlled", "Unknown"].includes(item.responsibility))) {
    comparisons.push(finding(row.identifier, "bom:responsibility", "not-applicable", [row.evidence], [], [`${row.responsibility} is semantic responsibility evidence, not a missing-CAD finding.`]));
  }

  const packetGroups = new Map<string, typeof controlledRows>();
  for (const row of controlledRows) {
    const key = await hash(normalize(row.identifier));
    packetGroups.set(key, [...(packetGroups.get(key) ?? []), row]);
  }
  const cadGroups = new Map<string, ActiveDocumentComponentDigest[]>();
  for (const component of context.componentEvidence) {
    cadGroups.set(component.identifierHash, [...(cadGroups.get(component.identifierHash) ?? []), component]);
  }

  const parentById = new Map(context.componentEvidence.map((item) => [item.snapshotId, item]));
  const allKeys = [...new Set([...packetGroups.keys(), ...cadGroups.keys()])].sort();
  for (const key of allKeys) {
    const rows = packetGroups.get(key) ?? [];
    const components = cadGroups.get(key) ?? [];
    const identifier = rows[0]?.identifier ?? key;
    const packetEvidence = rows.map((item) => item.evidence);
    if (!rows.length) {
      if (components.every((item) => item.kind === "assembly")) continue;
      comparisons.push(finding(identifier, "bom:membership", "missing-in-pdf", [], components));
      continue;
    }
    if (!components.length) {
      comparisons.push(finding(identifier, "bom:membership", "missing-in-cad", packetEvidence, []));
      continue;
    }
    const unresolved = components.filter((item) => item.resolutionState !== "resolved");
    if (unresolved.length) {
      comparisons.push(finding(identifier, "bom:resolution", "unresolved-evidence", packetEvidence, unresolved, unresolved.flatMap((item) => item.limitations).concat("No component resolution was attempted.")));
      continue;
    }
    comparisons.push(finding(identifier, "bom:membership", "exact-match", packetEvidence, components));
    if (rows.length > 1) comparisons.push(finding(identifier, "bom:duplicate", "duplicate", packetEvidence, components));
    const expectedQuantity = rows.reduce((sum, row) => sum + row.quantity, 0);
    comparisons.push(finding(identifier, "bom:quantity", expectedQuantity === components.length ? "exact-match" : "quantity-conflict", packetEvidence, components));

    const expectedParent = rows.find((row) => row.parentIdentifierCandidate)?.parentIdentifierCandidate;
    if (expectedParent) {
      const expectedParentHash = await hash(normalize(expectedParent));
      const matches = components.every((item) => parentById.get(item.parentSnapshotId)?.identifierHash === expectedParentHash);
      comparisons.push(finding(identifier, "bom:hierarchy", matches ? "exact-match" : "hierarchy-conflict", packetEvidence, components));
    }
    const expectedConfiguration = rows.find((row) => row.referencedConfiguration)?.referencedConfiguration;
    if (expectedConfiguration) {
      const expectedConfigurationHash = await hash(expectedConfiguration.trim().toUpperCase());
      const matches = components.every((item) => item.referencedConfigurationHash === expectedConfigurationHash);
      comparisons.push(finding(identifier, "bom:configuration", matches ? "exact-match" : "configuration-gap", packetEvidence, components));
    }
    for (const [field, property] of [["description", "description"], ["revision", "revision"]] as const) {
      const expected = rows.find((row) => row[field])?.[field];
      if (!expected) continue;
      const expectedHash = await hash(expected.trim().toUpperCase());
      const matches = components.every((item) => item.propertyEvidence.some((evidence) => evidence.canonicalField === property && evidence.normalizedValueHash === expectedHash));
      comparisons.push(finding(identifier, `bom:${field}`, matches ? "exact-match" : "conflict", packetEvidence, components));
    }
  }

  const packetIds = new Set(packetGroups.keys());
  const cadIds = new Set([...cadGroups.entries()].filter(([key, items]) => packetIds.has(key) || items.some((item) => item.kind !== "assembly")).map(([key]) => key));
  const matched = [...packetIds].filter((key) => cadIds.has(key)).length;
  return {
    schemaVersion: "vira.packet-cad.phase-b.v1",
    comparisons,
    scorecard: {
      packetControlledIdentifiers: packetIds.size,
      cadControlledIdentifiers: cadIds.size,
      matchedIdentifiers: matched,
      precision: cadIds.size ? matched / cadIds.size : packetIds.size ? 0 : 1,
      recall: packetIds.size ? matched / packetIds.size : 1
    },
    limitations: [
      ...(context.assemblyPayloadStatus === "ok" ? [] : [`Assembly payload status: ${context.assemblyPayloadStatus}.`]),
      ...(context.assemblyTraversal.truncated ? ["Assembly traversal was truncated by a deterministic safety bound."] : []),
      ...context.assemblyTraversal.warnings
    ],
    mutationActions: 0,
    externalSystemsAccessed: false
  };
}
