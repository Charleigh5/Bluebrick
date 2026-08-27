import type { ActiveDocumentContext, ActiveDocumentPropertyDigest } from "../activeDocumentContext";
import type { PacketEvidenceRefV2, PacketEvidenceSnapshotV2 } from "../packet-review/packetReview";

export type PhaseAComparisonStatus =
  | "exact-match"
  | "normalized-match"
  | "probable-match"
  | "conflict"
  | "missing-in-pdf"
  | "missing-in-cad"
  | "insufficient-evidence"
  | "unsupported";

export type PhaseAPropertyComparison = {
  comparisonId: string;
  canonicalField: string;
  status: PhaseAComparisonStatus;
  isAuthoritative: boolean;
  packetEvidence: PacketEvidenceRefV2[];
  cadEvidence: ActiveDocumentPropertyDigest[];
  ruleId: string;
};

export type PhaseAComparison = {
  schemaVersion: "vira.packet-cad.phase-a.v1";
  identity: {
    authority: "confirmed" | "strong-candidate" | "conflict" | "unresolved";
    isAuthoritative: boolean;
    matchSources: Array<"CAD_CONTROLLED_PROPERTY" | "DOCUMENT_TITLE_HASH">;
  };
  properties: PhaseAPropertyComparison[];
  mutationActions: 0;
  externalSystemsAccessed: false;
};

const canonicalNames: Record<PacketEvidenceSnapshotV2["properties"][number]["name"], string> = {
  Revision: "revision",
  Description: "description",
  Material: "material",
  Thickness: "thickness",
  CustomerAbbreviation: "customer_abbreviation",
  DrawnBy: "drawn_by",
  Configuration: "configuration"
};

function normalizeText(value: string): string {
  return value.trim().replace(/\s+/g, " ").toUpperCase();
}

async function hash(value: string, prefix = "value_sha256"): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
  const hex = [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, "0")).join("");
  return `${prefix}:${hex.slice(0, 16)}`;
}

function parseInches(value: string): number | null {
  if (/\bGA(?:UGE)?\b/i.test(value)) return null;
  const match = value.match(/(-?\d+(?:\.\d+)?)\s*(?:IN|INCH|INCHES|\")?\b/i);
  if (!match) return null;
  const parsed = Number(match[1]);
  return Number.isFinite(parsed) ? parsed : null;
}

async function packetNormalizedHashes(field: string, value: string): Promise<string[]> {
  if (field !== "thickness") return [await hash(normalizeText(value))];
  const inches = parseInches(value);
  if (inches === null) return [];
  const candidates: string[] = [];
  for (let offset = -5; offset <= 5; offset++) {
    candidates.push(await hash(`INCHES:${(inches + offset / 10000).toFixed(4)}`));
  }
  return candidates;
}

function comparisonStatus(
  packetHashes: string[],
  cad: ActiveDocumentPropertyDigest
): PhaseAComparisonStatus {
  if (cad.readStatus === "unsupported") return "unsupported";
  if (cad.readStatus === "missing") return "missing-in-cad";
  if (cad.readStatus === "cached-unresolved" || !cad.wasResolved) {
    return packetHashes.includes(cad.normalizedValueHash) ? "probable-match" : "insufficient-evidence";
  }
  return packetHashes.includes(cad.normalizedValueHash) ? "normalized-match" : "conflict";
}

export async function comparePacketEvidenceToActiveDocument(
  packet: PacketEvidenceSnapshotV2,
  context: ActiveDocumentContext
): Promise<PhaseAComparison> {
  const identifierHashes = new Set<string>();
  for (const item of packet.identifiers) identifierHashes.add(await hash(normalizeText(item.identifier)));
  const controlledIdentity = context.propertyEvidence.find(
    (item) => item.canonicalField === "part_number" && identifierHashes.has(item.normalizedValueHash)
  );
  const titleIdentity = packet.identifiers.some((item) => item.candidateDocumentTitleHashes.includes(context.titleHash));
  const matchSources: PhaseAComparison["identity"]["matchSources"] = [];
  if (controlledIdentity) matchSources.push("CAD_CONTROLLED_PROPERTY");
  if (titleIdentity) matchSources.push("DOCUMENT_TITLE_HASH");
  const identityConflict =
    identifierHashes.size > 0 &&
    context.propertyEvidence.some(
      (item) => item.canonicalField === "part_number" && item.readStatus === "resolved" && !identifierHashes.has(item.normalizedValueHash)
    );
  const authority = identityConflict
    ? "conflict"
    : matchSources.length === 2
      ? "confirmed"
      : matchSources.length === 1
        ? "strong-candidate"
        : "unresolved";
  const identityAuthoritative = authority === "confirmed";

  const properties: PhaseAPropertyComparison[] = [];
  const packetByField = new Map(packet.properties.map((item) => [canonicalNames[item.name], item]));
  const fields = new Set([...packetByField.keys(), ...context.propertyEvidence.map((item) => item.canonicalField)]);
  fields.delete("part_number");

  for (const field of [...fields].sort()) {
    const packetProperty = packetByField.get(field);
    const cadEvidence = context.propertyEvidence.filter((item) => item.canonicalField === field);
    let status: PhaseAComparisonStatus;
    if (!packetProperty) {
      status = "missing-in-pdf";
    } else if (!cadEvidence.length) {
      status = "missing-in-cad";
    } else {
      const hashes = await packetNormalizedHashes(field, packetProperty.evaluatedValue);
      status = hashes.length ? comparisonStatus(hashes, cadEvidence[0]) : "unsupported";
    }
    properties.push({
      comparisonId: `phase-a:${field}`,
      canonicalField: field,
      status,
      isAuthoritative: identityAuthoritative && status !== "insufficient-evidence" && status !== "unsupported",
      packetEvidence: packetProperty ? [packetProperty.evidence] : [],
      cadEvidence,
      ruleId: field === "thickness" ? "VIRA-COMPARE-THICKNESS-TOLERANCE-001" : "VIRA-COMPARE-PROPERTY-NORMALIZED-001"
    });
  }

  return {
    schemaVersion: "vira.packet-cad.phase-a.v1",
    identity: { authority, isAuthoritative: identityAuthoritative, matchSources },
    properties,
    mutationActions: 0,
    externalSystemsAccessed: false
  };
}
