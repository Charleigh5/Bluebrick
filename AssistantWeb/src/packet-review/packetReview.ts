import type { ActiveDocumentContext } from "../activeDocumentContext";

export type PacketPageText = {
  pageNumber: number;
  text: string;
};

export type PacketEvidence = {
  pageNumber: number;
  text: string;
  category: "title-block" | "bom" | "identifier" | "dimension" | "note" | "page";
};

export type PacketFinding = {
  code: string;
  severity: "info" | "warning" | "high";
  title: string;
  summary: string;
  evidence: PacketEvidence[];
  recommendedAction: string;
};

export type PacketReview = {
  schemaVersion: "vira.packet-review.v1";
  fileName: string;
  pageCount: number;
  analyzedUtc: string;
  pages: PacketPageText[];
  titleBlocks: PacketEvidence[];
  drawingGroups: Array<{ label: "Assembly" | "Part" | "Other"; identifiers: string[] }>;
  bomRecords: Array<{ pageNumber: number; item: string; quantity: string; partNumber: string; description: string }>;
  partNumbers: string[];
  dimensions: PacketEvidence[];
  notes: PacketEvidence[];
  findings: PacketFinding[];
  questions: string[];
};

export type PacketContextComparison = {
  status: "confirmed" | "needs-verification" | "unavailable";
  summary: string;
  matchedPartNumber?: string;
  evidence: string[];
};

export type PacketEvidenceRefV2 = {
  evidenceId: string;
  sourceKind: "Pdf";
  sourceId: string;
  pageOrSheet: string;
  regionOrNativePath: string;
  fieldName: string;
  rawValue: string;
  evaluatedValue: string;
  extractionMethod: "deterministic-text-rule" | "deterministic-title-block-field-pair";
  ruleId: string;
  authority: "Observed" | "Derived";
  confidence: number;
  verificationStatus: "Confirmed" | "Candidate";
  limitations: string[];
};

export type PacketEvidenceSnapshotV2 = {
  schemaVersion: "vira.packet-evidence.v2";
  snapshotId: string;
  packetId: string;
  fileNameLabel: string;
  pageCount: number;
  identifiers: Array<{
    identifier: string;
    candidateDocumentTitleHashes: string[];
    evidence: PacketEvidenceRefV2;
    supportingEvidence?: PacketEvidenceRefV2[];
  }>;
  properties: Array<{
    name: "Revision" | "Description" | "Material" | "Thickness" | "CustomerAbbreviation" | "DrawnBy" | "Configuration";
    rawValue: string;
    evaluatedValue: string;
    evidence: PacketEvidenceRefV2;
  }>;
  bomRows: Array<{
    itemNumber: string;
    rawQuantity: string;
    quantity: number;
    identifier: string;
    description: string;
    revision: string;
    referencedConfiguration: string;
    parentIdentifierCandidate: string;
    responsibility: "Unknown" | "Controlled" | "PurchasedByOthers" | "Reference" | "Existing" | "StoreSupplied";
    evidence: PacketEvidenceRefV2;
    limitations: string[];
  }>;
  limitations: string[];
};

const viraIdentifierPrefixes = "ASY|ASM|SUB|MPM|MPP|MPW|HWD|GRA|LVL|PBO|REF|EXIST|PRT|DRW";
const partNumberPattern = new RegExp(`\\b(?:${viraIdentifierPrefixes})[A-Z0-9]*[-_][A-Z0-9][A-Z0-9_-]*\\b`, "gi");
const titleBlockPartValuePattern = new RegExp(`^(?:${viraIdentifierPrefixes})[A-Z0-9]{5,12}$`, "i");
const titleBlockDocumentValuePattern = /^\d{8}$/;
const sheetPattern = /\bSHEET\s+\d+\s+OF\s+\d+\b/i;
const titleBlockPattern = /\b(?:DRAWING\s+(?:NO|NUMBER)|REV(?:ISION)?|SHEET\s+\d+\s+OF\s+\d+)\b/i;
const dimensionPattern = /(?:\b\d+(?:\.\d+)?\s*(?:±|\+\/-)\s*\d+(?:\.\d+)?\b|(?:\b\d+X\s*)?(?:Ø|DIA\.?)[\s.]?\d+(?:\.\d+)?|\b\d+(?:\.\d+)?°\b)/i;
const notePattern = /^(?:GENERAL\s+NOTES?|NOTES?|\d+[.)])\s*/i;
const bomRowPattern = new RegExp(`^\\s*(\\d+)\\s+(\\d+(?:\\.\\d+)?)\\s+((?:${viraIdentifierPrefixes})[A-Z0-9]*[-_][A-Z0-9][A-Z0-9_-]*)\\s+(.+)$`, "i");

function lines(text: string): string[] {
  return text
    .split(/\r?\n/)
    .map((line) => line.replace(/\s+/g, " ").trim())
    .filter(Boolean);
}

function unique(values: string[]): string[] {
  return [...new Set(values.map((value) => value.toUpperCase()))].sort();
}

function evidence(pageNumber: number, text: string, category: PacketEvidence["category"]): PacketEvidence {
  return { pageNumber, text, category };
}

type PacketIdentifierCandidate = {
  identifier: string;
  pageNumber: number;
  lineStart: number;
  lineEnd: number;
  extractionMethod: PacketEvidenceRefV2["extractionMethod"];
  ruleId: string;
  authority: PacketEvidenceRefV2["authority"];
  confidence: number;
  verificationStatus: PacketEvidenceRefV2["verificationStatus"];
  limitations: string[];
};

function titleBlockLabel(line: string): "document" | "part" | null {
  const normalized = line.replace(/[.:#]/g, "").replace(/\s+/g, " ").trim().toUpperCase();
  if (/^(?:DOC|DOCUMENT) (?:NO|NUMBER)$/.test(normalized)) return "document";
  if (/^PART (?:NO|NUMBER)$/.test(normalized)) return "part";
  return null;
}

function extractPacketIdentifierCandidates(pages: PacketPageText[]): PacketIdentifierCandidate[] {
  const candidates: PacketIdentifierCandidate[] = [];

  for (const page of pages) {
    const pageLines = lines(page.text);
    for (let index = 0; index < pageLines.length; index++) {
      for (const match of pageLines[index].match(partNumberPattern) ?? []) {
        candidates.push({
          identifier: match.toUpperCase(),
          pageNumber: page.pageNumber,
          lineStart: index + 1,
          lineEnd: index + 1,
          extractionMethod: "deterministic-text-rule",
          ruleId: "VIRA-PACKET-IDENTIFIER-COMPLETE-TOKEN-001",
          authority: "Observed",
          confidence: 1,
          verificationStatus: "Confirmed",
          limitations: []
        });
      }
    }

    for (let index = 0; index <= pageLines.length - 4; index++) {
      const firstLabel = titleBlockLabel(pageLines[index]);
      const secondLabel = titleBlockLabel(pageLines[index + 1]);
      if (!firstLabel || !secondLabel || firstLabel === secondLabel) continue;

      const values = {
        [firstLabel]: pageLines[index + 2],
        [secondLabel]: pageLines[index + 3]
      } as Record<"document" | "part", string>;
      if (!titleBlockDocumentValuePattern.test(values.document)) continue;
      if (!titleBlockPartValuePattern.test(values.part)) continue;

      candidates.push({
        identifier: `${values.part.toUpperCase()}-${values.document}`,
        pageNumber: page.pageNumber,
        lineStart: index + 1,
        lineEnd: index + 4,
        extractionMethod: "deterministic-title-block-field-pair",
        ruleId: "VIRA-PACKET-IDENTIFIER-TITLE-BLOCK-PAIR-001",
        authority: "Derived",
        confidence: 0.98,
        verificationStatus: "Candidate",
        limitations: ["Reconstructed from explicit PART NO and DOC NO fields on the same page; corroborate against controlled CAD identity before release."]
      });
    }
  }

  const observed = new Set<string>();
  return candidates.filter((candidate) => {
    const key = `${candidate.pageNumber}|${candidate.identifier}|${candidate.ruleId}`;
    if (observed.has(key)) return false;
    observed.add(key);
    return true;
  });
}

export function analyzePacketPages(fileName: string, pages: PacketPageText[], analyzedUtc = new Date().toISOString()): PacketReview {
  const titleBlocks: PacketEvidence[] = [];
  const dimensions: PacketEvidence[] = [];
  const notes: PacketEvidence[] = [];
  const bomRecords: PacketReview["bomRecords"] = [];
  const identifierCandidates = extractPacketIdentifierCandidates(pages);

  for (const page of pages) {
    const pageLines = lines(page.text);
    const titleLine = pageLines.find((line) => sheetPattern.test(line)) ?? pageLines.find((line) => titleBlockPattern.test(line));
    if (titleLine) titleBlocks.push(evidence(page.pageNumber, titleLine, "title-block"));

    for (const line of pageLines) {
      const bom = line.match(bomRowPattern);
      if (bom) {
        bomRecords.push({
          pageNumber: page.pageNumber,
          item: bom[1],
          quantity: bom[2],
          partNumber: bom[3].toUpperCase(),
          description: bom[4]
        });
      }
      if (dimensionPattern.test(line)) dimensions.push(evidence(page.pageNumber, line, "dimension"));
      if (notePattern.test(line) || /\b(?:FINISH|MATERIAL|SHARP EDGES|TOLERANCE)\b/i.test(line)) {
        notes.push(evidence(page.pageNumber, line, "note"));
      }
    }
  }

  const partNumbers = unique(identifierCandidates.map((candidate) => candidate.identifier));
  const assemblyIdentifiers = partNumbers.filter((value) => /^(?:ASY|ASM|SUB)/.test(value));
  const partIdentifiers = partNumbers.filter((value) => /^(?:PRT|MPM|MPP|MPW|HWD|GRA|LVL)/.test(value));
  const otherIdentifiers = partNumbers.filter((value) => !/^(?:ASY|ASM|SUB|PRT|MPM|MPP|MPW|HWD|GRA|LVL)/.test(value));
  const drawingGroups: PacketReview["drawingGroups"] = [];
  if (assemblyIdentifiers.length) drawingGroups.push({ label: "Assembly", identifiers: assemblyIdentifiers });
  if (partIdentifiers.length) drawingGroups.push({ label: "Part", identifiers: partIdentifiers });
  if (otherIdentifiers.length) drawingGroups.push({ label: "Other", identifiers: otherIdentifiers });

  const findings: PacketFinding[] = [];
  if (pages.length && partNumbers.length) {
    findings.push({
      code: "PACKET_EVIDENCE_READY",
      severity: "info",
      title: "Packet evidence indexed",
      summary: `${pages.length} page${pages.length === 1 ? "" : "s"} yielded ${partNumbers.length} drawing identifier${partNumbers.length === 1 ? "" : "s"}.`,
      evidence: [evidence(identifierCandidates[0].pageNumber, partNumbers.slice(0, 3).join(", "), "identifier")],
      recommendedAction: "Review the indexed identifiers and page evidence before release decisions."
    });
  }
  if (!titleBlocks.length) {
    findings.push({
      code: "TITLE_BLOCK_EVIDENCE_MISSING",
      severity: "warning",
      title: "Title-block evidence not detected",
      summary: "No revision, drawing number, or sheet marker was detected in extracted packet text.",
      evidence: pages.length ? [evidence(pages[0].pageNumber, "No title-block marker detected in extracted text.", "page")] : [],
      recommendedAction: "Open the page image and verify the title block manually; OCR may be required for image-only pages."
    });
  }
  if (!bomRecords.length) {
    findings.push({
      code: "BOM_RECORDS_NOT_DETECTED",
      severity: "warning",
      title: "BOM records not detected",
      summary: "No deterministic item, quantity, part-number, description rows were found.",
      evidence: pages.length ? [evidence(pages[0].pageNumber, "No matching BOM row structure detected.", "bom")] : [],
      recommendedAction: "Verify whether the packet requires a BOM and inspect image-only or non-tabular BOM content."
    });
  }

  const questions: string[] = [];
  if (!partNumbers.length) questions.push("Which controlled drawing or part number identifies this packet?");
  if (!titleBlocks.length) questions.push("Is the title block image-only, or is title-block evidence absent?");
  if (!bomRecords.length) questions.push("Does this packet require a BOM, and if so, is it image-only or non-tabular?");

  return {
    schemaVersion: "vira.packet-review.v1",
    fileName,
    pageCount: pages.length,
    analyzedUtc,
    pages,
    titleBlocks,
    drawingGroups,
    bomRecords,
    partNumbers,
    dimensions,
    notes,
    findings,
    questions
  };
}

async function hashForOutput(value: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
  const hex = [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, "0")).join("");
  return `sha256:${hex.slice(0, 16)}`;
}

const packetPropertyPatterns: Array<{
  name: PacketEvidenceSnapshotV2["properties"][number]["name"];
  pattern: RegExp;
}> = [
  { name: "Revision", pattern: /\bREV(?:ISION)?\s*[:#-]?\s*([A-Z0-9]+)\b/i },
  { name: "Description", pattern: /\b(?:DESCRIPTION|DESC)\s*[:#-]\s*(.+)$/i },
  { name: "Material", pattern: /\bMATERIAL\s*[:#-]\s*(.+)$/i },
  { name: "Thickness", pattern: /\bTHICKNESS\s*[:#-]\s*(.+)$/i },
  { name: "CustomerAbbreviation", pattern: /\bCUSTOMER\s+(?:ABBREVIATION|ABBR)\s*[:#-]\s*(.+)$/i },
  { name: "DrawnBy", pattern: /\bDRAWN\s+BY\s*[:#-]\s*(.+)$/i },
  { name: "Configuration", pattern: /\bCONFIG(?:URATION)?\s*[:#-]\s*(.+)$/i }
];

export async function projectPacketEvidenceV2(review: PacketReview): Promise<PacketEvidenceSnapshotV2> {
  const snapshotHash = await hashForOutput(`${review.fileName}|${review.pageCount}`);
  const snapshotId = `packet-${snapshotHash}`;
  const fileNameLabel = review.fileName.split(/[\\/]/).pop() || "local-packet.pdf";
  const candidateGroups = new Map<string, PacketIdentifierCandidate[]>();
  for (const candidate of extractPacketIdentifierCandidates(review.pages)) {
    const group = candidateGroups.get(candidate.identifier) ?? [];
    group.push(candidate);
    candidateGroups.set(candidate.identifier, group);
  }
  const identifiers = await Promise.all(review.partNumbers.map(async (identifier) => {
    const candidatesForIdentifier = candidateGroups.get(identifier) ?? [];
    const primaryCandidate = candidatesForIdentifier[0];
    const candidates = [identifier, `${identifier}.SLDASM`, `${identifier}.SLDPRT`, `${identifier}.SLDDRW`];
    const supportingEvidence = candidatesForIdentifier.map((candidate) => packetEvidenceRef(
      snapshotId,
      candidate.pageNumber,
      "PartNumber",
      identifier,
      `line:${candidate.lineStart}-${candidate.lineEnd}`,
      {
        extractionMethod: candidate.extractionMethod,
        ruleId: candidate.ruleId,
        authority: candidate.authority,
        confidence: candidate.confidence,
        verificationStatus: candidate.verificationStatus,
        limitations: candidate.limitations
      }
    ));
    return {
      identifier,
      candidateDocumentTitleHashes: await Promise.all(candidates.map(hashForOutput)),
      evidence: supportingEvidence[0] ?? packetEvidenceRef(snapshotId, primaryCandidate?.pageNumber ?? 0, "PartNumber", identifier, `identifier:${identifier}`),
      supportingEvidence
    };
  }));
  const properties: PacketEvidenceSnapshotV2["properties"] = [];
  const observedNames = new Set<string>();

  for (const page of review.pages) {
    const pageLines = lines(page.text);
    for (let index = 0; index < pageLines.length; index++) {
      const line = pageLines[index];
      for (const property of packetPropertyPatterns) {
        if (observedNames.has(property.name)) continue;
        const match = line.match(property.pattern);
        const rawValue = match?.[1]?.trim();
        if (!rawValue) continue;
        observedNames.add(property.name);
        properties.push({
          name: property.name,
          rawValue,
          evaluatedValue: rawValue,
          evidence: packetEvidenceRef(snapshotId, page.pageNumber, property.name, rawValue, `line:${index + 1}`)
        });
      }
    }
  }

  const bomRows: PacketEvidenceSnapshotV2["bomRows"] = review.bomRecords.map((row) => {
    const page = review.pages.find((item) => item.pageNumber === row.pageNumber);
    const lineIndex = Math.max(0, lines(page?.text ?? "").findIndex((line) => line.toUpperCase().includes(row.partNumber.toUpperCase())));
    const responsibility = bomResponsibility(row.partNumber, row.description);
    return {
      itemNumber: row.item,
      rawQuantity: row.quantity,
      quantity: Number(row.quantity),
      identifier: row.partNumber,
      description: row.description,
      revision: "",
      referencedConfiguration: "",
      parentIdentifierCandidate: "",
      responsibility,
      evidence: packetEvidenceRef(snapshotId, row.pageNumber, "BomRow", `${row.item}|${row.quantity}|${row.partNumber}|${row.description}`, `bom-row:${lineIndex + 1}`),
      limitations: ["Revision, configuration, and parent hierarchy are unstated unless separately evidenced by a governed parser rule."]
    };
  });

  return {
    schemaVersion: "vira.packet-evidence.v2",
    snapshotId,
    packetId: review.partNumbers.find((identifier) => /^(?:ASY|ASM)/i.test(identifier)) ?? snapshotId,
    fileNameLabel,
    pageCount: review.pageCount,
    identifiers,
    properties,
    bomRows,
    limitations: ["Text-rule projection only; image-only evidence remains unsupported without an approved OCR workflow."]
  };
}

function bomResponsibility(identifier: string, description: string): PacketEvidenceSnapshotV2["bomRows"][number]["responsibility"] {
  if (/\bSTORE\s+SUPPLIED\b/i.test(description)) return "StoreSupplied";
  if (/^PBO/i.test(identifier)) return "PurchasedByOthers";
  if (/^REF/i.test(identifier)) return "Reference";
  if (/^EXIST/i.test(identifier)) return "Existing";
  return "Controlled";
}

function packetEvidenceRef(
  snapshotId: string,
  pageNumber: number,
  fieldName: string,
  value: string,
  region: string,
  options: Partial<Pick<
    PacketEvidenceRefV2,
    "extractionMethod" | "ruleId" | "authority" | "confidence" | "verificationStatus" | "limitations"
  >> = {}
): PacketEvidenceRefV2 {
  return {
    evidenceId: `${snapshotId}:p${pageNumber}:${fieldName.toLowerCase()}:${region}`,
    sourceKind: "Pdf",
    sourceId: snapshotId,
    pageOrSheet: String(pageNumber),
    regionOrNativePath: region,
    fieldName,
    rawValue: value,
    evaluatedValue: value,
    extractionMethod: options.extractionMethod ?? "deterministic-text-rule",
    ruleId: options.ruleId ?? `VIRA-PACKET-PROPERTY-${fieldName.toUpperCase()}-001`,
    authority: options.authority ?? "Observed",
    confidence: options.confidence ?? 1,
    verificationStatus: options.verificationStatus ?? "Confirmed",
    limitations: options.limitations ?? []
  };
}

function titleCandidates(partNumber: string, documentType: string): string[] {
  const extension = documentType === "Assembly" ? ".SLDASM" : documentType === "Part" ? ".SLDPRT" : documentType === "Drawing" ? ".SLDDRW" : "";
  return unique([partNumber, extension ? `${partNumber}${extension}` : partNumber]);
}

export async function comparePacketToActiveDocument(review: PacketReview, context: ActiveDocumentContext): Promise<PacketContextComparison> {
  if (context.state !== "ready") {
    return {
      status: "needs-verification",
      summary: `Active document context is ${context.state}; packet-to-model identity needs verification.`,
      evidence: [context.message]
    };
  }
  if (!context.titleHash || context.titleHash === "redacted") {
    return {
      status: "needs-verification",
      summary: "The active document title is redacted without a comparable hash.",
      evidence: [`Active document type: ${context.documentType}`, `Packet identifiers: ${review.partNumbers.length}`]
    };
  }

  for (const partNumber of review.partNumbers) {
    for (const candidate of titleCandidates(partNumber, context.documentType)) {
      if ((await hashForOutput(candidate)) === context.titleHash.toLowerCase()) {
        return {
          status: "confirmed",
          summary: `${partNumber} matches the redacted active-document title hash.`,
          matchedPartNumber: partNumber,
          evidence: [`Active document type: ${context.documentType}`, `Hash comparison: confirmed`, `Packet identifier: ${partNumber}`]
        };
      }
    }
  }

  return {
    status: "needs-verification",
    summary: "No packet identifier matched the redacted active-document title hash. This is not treated as a conflict without controlled identity evidence.",
    evidence: [`Active document type: ${context.documentType}`, `Packet identifiers checked: ${review.partNumbers.length}`]
  };
}

function markdownList(values: string[], empty: string): string {
  return values.length ? values.map((value) => `- ${value}`).join("\n") : `- ${empty}`;
}

export function createPacketReviewReport(review: PacketReview, comparison: PacketContextComparison): string {
  const findings = review.findings.map((finding) =>
    `### ${finding.code} — ${finding.title}\n\nSeverity: ${finding.severity.toUpperCase()}\n\n${finding.summary}\n\nEvidence: ${finding.evidence.map((item) => `p.${item.pageNumber}: ${item.text}`).join("; ")}\n\nRecommended action: ${finding.recommendedAction}`
  ).join("\n\n");
  return `# VIRA Packet Review\n\nGenerated: ${review.analyzedUtc}\n\nSource: ${review.fileName} (local file only)\n\nPages analyzed: ${review.pageCount}\n\n## Active document comparison\n\nStatus: ${comparison.status}\n\n${comparison.summary}\n\n${markdownList(comparison.evidence, "No comparison evidence available.")}\n\n## Drawing identifiers\n\n${markdownList(review.partNumbers, "No identifiers detected.")}\n\n## BOM records\n\n${markdownList(review.bomRecords.map((row) => `p.${row.pageNumber} item ${row.item}, qty ${row.quantity}, ${row.partNumber}, ${row.description}`), "No BOM records detected.")}\n\n## Dimensions\n\n${markdownList(review.dimensions.map((item) => `p.${item.pageNumber}: ${item.text}`), "No dimension text detected.")}\n\n## Notes\n\n${markdownList(review.notes.map((item) => `p.${item.pageNumber}: ${item.text}`), "No note text detected.")}\n\n## Findings\n\n${findings || "No findings generated."}\n\n## Review questions\n\n${markdownList(review.questions, "No deterministic review questions generated.")}\n`;
}

export function buildPacketReviewReceipt(review: PacketReview, comparison: PacketContextComparison, createdUtc = new Date().toISOString()) {
  return {
    schemaVersion: "vira.packet-review.receipt.v1",
    createdUtc,
    source: {
      fileName: review.fileName,
      localOnly: true,
      pageCount: review.pageCount,
      fileContentPersisted: false
    },
    result: {
      titleBlockEvidence: review.titleBlocks.length,
      drawingGroups: review.drawingGroups.length,
      bomRecords: review.bomRecords.length,
      partNumbers: review.partNumbers.length,
      dimensions: review.dimensions.length,
      notes: review.notes.length,
      findings: review.findings.length,
      comparisonStatus: comparison.status
    },
    actions: {
      localAnalysis: 1,
      derivedOutputs: 1,
      engineeringMutations: 0,
      connectorCalls: 0
    },
    verification: {
      localPdfTextExtraction: true,
      activeDocumentHashComparison: comparison.status === "confirmed",
      liveSolidWorksMutation: false,
      pdmEpicorSalesforceAccess: false
    }
  };
}
