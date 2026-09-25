import { useCallback, useEffect, useLayoutEffect, useRef, useState, type CSSProperties, type DragEvent } from "react";
import "./styles.css";
import {
  createBlueBrickWindowBridge,
  type BlueBrickBridge,
  type BlueBrickBridgeHandlers,
} from "./bridge/blueBrickWebViewBridge";
import { HardwareCadPanel } from "./hardware-cad/HardwareCadPanel";
import { ExecutionBoardApp } from "./execution-board/ExecutionBoardApp";
import { ViraLabApp } from "./vira-lab/ViraLabApp";
import { RuntimeIdentitySurface } from "./runtimeIdentity";
import { resolveAssistantSurface } from "./surfaceRouting";
import { analyzePacketFile, renderPacketPage } from "./packet-review/analyzePacketFile";
import type { PacketReview } from "./packet-review/packetReview";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
type Message = {
  id: string;
  role: "user" | "assistant";
  text: string;
  streaming?: boolean;
  attachment?: string;
  attachments?: string[];
  evidence?: EvidenceLink[];
  /** Rendered only in the packet-demo surface; never exposed to the host transcript. */
  presentationOnly?: boolean;
  /** A local packet-demo note that must not be dispatched through the host bridge. */
  localDemoOnly?: boolean;
};

type EvidenceLink = {
  packetIndex: number;
  pageNumber: number;
  label: string;
};

type PacketPagePreview = {
  packetIndex: number;
  fileName: string;
  pageNumber: number;
  pageCount: number;
  label: string;
  imageUrl: string;
  zoom: number;
  fitMode: "custom" | "width" | "page";
};

type PendingAttachment = {
  id: string;
  name: string;
  mimeType: string;
  size: number;
  base64Data: string;
  previewUrl?: string;
  extraction: string;
};

type Scope = {
  id: string;
  label: string;
  enabled: boolean;
  unavailableReason?: string;
};

type Model = {
  id: string;
  providerId?: string;
  displayName: string;
  available?: boolean;
  unavailableReason?: string;
  supportsVision?: boolean;
  supportsToolCalling?: boolean;
  supportsStructuredOutput?: boolean;
};

type ScreenshotArtifact = {
  thumbnailDataUrl?: string;
  attachedToConversation?: boolean;
  approvalMode?: string;
  screenshotId?: string;
  artifactId?: string;
  fileName?: string;
  localOnlyCloudState?: string;
  width?: number;
  height?: number;
  sourceWindowTitle?: string;
  captureSource?: string;
  annotations?: Array<{ id?: string; label?: string; source?: string; reviewStatus?: string }>;
  contacts?: Array<{ id?: string; name?: string; email?: string; reviewStatus?: string }>;
  [key: string]: unknown;
};

type ToolResult = {
  label?: string;
  query?: string;
  status?: string;
  message?: string;
  items?: unknown[];
  receipt?: unknown;
};

type BridgeStatus = "offline" | "connecting" | "connected" | "error";

type AnnotationPin = {
  id: string;
  selector: string;
  excerpt: string;
  note: string;
};

type ToolbarItem = {
  id: string;
  label: string;
  description: string;
  symbol: string;
  category: "workspace" | "generator" | "macro";
  hostRequired?: boolean;
  source?: string;
};

type ToolbarPreferences = {
  itemIds: string[];
  rows: 1 | 2;
  locked: boolean;
  customItems: ToolbarItem[];
};

// ---------------------------------------------------------------------------
// Theme (accent + secondary, persisted default)
// ---------------------------------------------------------------------------
const BUILT_IN_ACCENT = "#5de1ff";
const BUILT_IN_SECONDARY = "#8da9ff";
const ACCENT_PRESETS = ["#5de1ff", "#8da9ff", "#72e7c0", "#f7c873", "#f58da8"];
const SECONDARY_PRESETS = ["#8da9ff", "#5de1ff", "#72e7c0", "#f58da8", "#c5d4ff"];
const ACCEPTED_ATTACHMENT_TYPES = ".pdf,.png,.jpg,.jpeg,.bmp,application/pdf,image/png,image/jpeg,image/bmp";
const MAX_ATTACHMENT_COUNT = 8;
const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024;
const MAX_ATTACHMENT_TOTAL_BYTES = 24 * 1024 * 1024;
const TOOLBAR_STORAGE_KEY = "bb.assistant.toolbar.v1";
const BUILT_IN_TOOLBAR_ITEMS: ToolbarItem[] = [
  { id: "new", label: "New", description: "Start a new assistant session", symbol: "new", category: "workspace" },
  { id: "capture", label: "Capture", description: "Capture the current local screen", symbol: "capture", category: "workspace" },
  { id: "search", label: "Search", description: "Search the selected engineering scope", symbol: "search", category: "workspace" },
  { id: "packet-pdf", label: "Packet PDF", description: "Generate a governed drawing packet PDF", symbol: "pdf", category: "generator", hostRequired: true },
  { id: "sheet-png", label: "Sheet PNG", description: "Export drawing sheets as PNG images", symbol: "png", category: "generator", hostRequired: true },
  { id: "step-export", label: "STEP", description: "Export the active model as a STEP file", symbol: "step", category: "generator", hostRequired: true },
  { id: "dxf-export", label: "DXF", description: "Export supported geometry as DXF", symbol: "dxf", category: "generator", hostRequired: true },
];
const DEFAULT_TOOLBAR_IDS = ["new", "capture", "search"];

function readToolbarPreferences(): ToolbarPreferences {
  const fallback: ToolbarPreferences = { itemIds: DEFAULT_TOOLBAR_IDS, rows: 1, locked: false, customItems: [] };
  try {
    const parsed: unknown = JSON.parse(window.localStorage.getItem(TOOLBAR_STORAGE_KEY) ?? "null");
    if (!parsed || typeof parsed !== "object") return fallback;
    const candidate = parsed as Partial<ToolbarPreferences>;
    const customItems = Array.isArray(candidate.customItems)
      ? candidate.customItems.filter((item): item is ToolbarItem => Boolean(
        item && typeof item.id === "string" && typeof item.label === "string" &&
        typeof item.description === "string" && typeof item.symbol === "string" &&
        item.category === "macro" && typeof item.source === "string",
      ))
      : [];
    const validIds = new Set([...BUILT_IN_TOOLBAR_ITEMS.map((item) => item.id), ...customItems.map((item) => item.id)]);
    const itemIds = Array.isArray(candidate.itemIds)
      ? Array.from(new Set(candidate.itemIds.filter((id): id is string => typeof id === "string" && validIds.has(id))))
      : DEFAULT_TOOLBAR_IDS;
    return {
      itemIds: itemIds.length ? itemIds : DEFAULT_TOOLBAR_IDS,
      rows: candidate.rows === 2 ? 2 : 1,
      locked: candidate.locked === true,
      customItems,
    };
  } catch {
    return fallback;
  }
}

type PromptTemplate = { id: string; label: string; prompt: string };
const TEMPLATE_STORAGE_KEY = "bb.assistant.templates.v1";
const BUILT_IN_PROMPT_TEMPLATES: PromptTemplate[] = [
  { id: "tpl-packet-review", label: "Review packet", prompt: "Review the attached drawing packet: list part numbers, BOM rows, dimensions, and notes. Call out anything missing or inconsistent, and separate confirmed facts from unknowns." },
  { id: "tpl-bom-audit", label: "Audit BOM", prompt: "Audit the BOM in the attached drawings: compare quantities and part numbers against the title blocks and flag discrepancies with page references." },
  { id: "tpl-dim-check", label: "Check dimensions", prompt: "Check the dimensions in the attached drawings against the stated tolerances and title-block data. List each deviation with its page number." },
  { id: "tpl-release-summary", label: "Release summary", prompt: "Draft a release summary for this session: what was reviewed, key findings, open questions, and the recommended next actions." },
];

function readCustomTemplates(): PromptTemplate[] {
  try {
    const parsed: unknown = JSON.parse(window.localStorage.getItem(TEMPLATE_STORAGE_KEY) ?? "null");
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((item): item is PromptTemplate => Boolean(
      item && typeof item === "object" && typeof (item as PromptTemplate).id === "string" &&
      typeof (item as PromptTemplate).label === "string" && typeof (item as PromptTemplate).prompt === "string",
    ));
  } catch {
    return [];
  }
}

const TOOLBAR_ICON_PATHS: Record<string, string[]> = {
  new: [
    "M14 2.5H6A1.5 1.5 0 0 0 4.5 4v16A1.5 1.5 0 0 0 6 21.5h12a1.5 1.5 0 0 0 1.5-1.5V8z",
    "M14 2.5V8h5.5",
    "M12 11.5v6",
    "M9 14.5h6",
  ],
  capture: [
    "M3 8.5A1.5 1.5 0 0 1 4.5 7h2.6l1.7-2.5h6.4L16.9 7h2.6A1.5 1.5 0 0 1 21 8.5v10a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 18.5z",
    "M12 16.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7z",
  ],
  attach: [
    "m21.44 11.05-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48",
  ],
  search: [
    "M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16z",
    "m21 21-4.35-4.35",
  ],
  pdf: [
    "M14 2.5H6A1.5 1.5 0 0 0 4.5 4v16A1.5 1.5 0 0 0 6 21.5h12a1.5 1.5 0 0 0 1.5-1.5V8z",
    "M14 2.5V8h5.5",
    "M9 13h6",
    "M9 17h4",
  ],
  png: [
    "M5 3.5h14A1.5 1.5 0 0 1 20.5 5v14a1.5 1.5 0 0 1-1.5 1.5H5A1.5 1.5 0 0 1 3.5 19V5A1.5 1.5 0 0 1 5 3.5z",
    "M9.5 10.25a1.75 1.75 0 1 0 0-3.5 1.75 1.75 0 0 0 0 3.5z",
    "m20.5 14.5-4.5-4.5L5.5 20.5",
  ],
  step: [
    "M12 2.5 20.5 7.1v10L12 21.5l-8.5-4.4v-10z",
    "m20.5 7.1-8.5 4.9-8.5-4.9",
    "M12 12v9.5",
  ],
  dxf: [
    "M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5z",
    "m15 5 4 4",
  ],
  macro: [
    "m8 5.5 11 6.5-11 6.5z",
  ],
};

function ToolbarIcon({ symbol }: { symbol: string }) {
  const paths = TOOLBAR_ICON_PATHS[symbol] ?? TOOLBAR_ICON_PATHS.new;
  return (
    <svg
      className="toolbar-icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {paths.map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  );
}

function toolbarTipEdge(index: number, columns: number, count: number): string {
  const col = index % columns;
  const row = Math.floor(index / columns);
  const rowLast = Math.min((row + 1) * columns - 1, count - 1);
  if (col === 0 && rowLast !== 0) return "start";
  if (index === rowLast && col !== 0) return "end";
  return "";
}
const PACKET_DEMO_MODELS: Model[] = [
  { id: "nvidia-llama-3-1-70b", providerId: "nvidia", displayName: "NVIDIA Llama 3.1 70B", available: true },
  { id: "openai-gpt-4-1-mini", providerId: "openai", displayName: "OpenAI GPT-4.1 Mini", available: true },
  { id: "aionui-default", providerId: "aionui_broker", displayName: "AionUI Default", available: true },
];
const PACKET_DEMO_INTRO: Message[] = [
  {
    id: "packet-demo-intro",
    role: "assistant",
    text: "DEMO · local packet intake ready\n\nSelect or drop exactly two PDF fixtures. BlueBrick will read both locally, preserve their order, and generate the intake receipt and evidence findings from the actual page text.",
  },
];
const PACKET_DEMO_INITIAL_MESSAGES: Message[] = [
  ...PACKET_DEMO_INTRO,
  {
    id: "packet-demo-example-user-prompt",
    role: "user",
    text: BUILT_IN_PROMPT_TEMPLATES[0].prompt,
    presentationOnly: true,
  },
];

function packetEvidenceSummary(reviews: PacketReview[]): string {
  return reviews.map((review, index) => {
    const identifiers = review.partNumbers.length ? review.partNumbers.join(", ") : "none detected";
    const bom = review.bomRecords.length
      ? review.bomRecords.map((row) => `p.${row.pageNumber} item ${row.item} · qty ${row.quantity} · ${row.partNumber} · ${row.description}`).join("\n  ")
      : "none detected";
    const dimensions = review.dimensions.length
      ? review.dimensions.map((item) => `p.${item.pageNumber} ${item.text}`).join("; ")
      : "none detected";
    const notes = review.notes.filter((item) => !review.dimensions.some((dimension) => dimension.pageNumber === item.pageNumber && dimension.text === item.text));
    return `PACKET ${index + 1} · ${review.fileName}\n${review.pageCount} pages · ${review.titleBlocks.length} title-block markers · ${review.bomRecords.length} BOM rows\nIdentifiers: ${identifiers}\nBOM:\n  ${bom}\nDimensions: ${dimensions}\nNotes: ${notes.length ? notes.map((item) => `p.${item.pageNumber} ${item.text}`).join("; ") : "none detected"}`;
  }).join("\n\n");
}

function packetEvidenceLinks(reviews: PacketReview[]): EvidenceLink[] {
  const links: EvidenceLink[] = [];
  const seen = new Set<string>();
  reviews.forEach((review, packetIndex) => {
    const add = (pageNumber: number, label: string) => {
      const key = `${packetIndex}:${pageNumber}:${label}`;
      if (seen.has(key)) return;
      seen.add(key);
      links.push({ packetIndex, pageNumber, label: `Packet ${packetIndex + 1} · p.${pageNumber} · ${label}` });
    };
    review.titleBlocks.forEach((item) => add(item.pageNumber, "title block"));
    review.bomRecords.forEach((row) => add(row.pageNumber, `BOM item ${row.item}`));
    review.dimensions.forEach((item) => add(item.pageNumber, item.text));
    review.notes.forEach((item) => add(item.pageNumber, item.text));
  });
  return links;
}

function attachmentExtraction(name: string, mimeType: string): string {
  if (mimeType === "application/pdf" || name.toLowerCase().endsWith(".pdf")) {
    return "PDF: page text, drawing identifiers, title blocks, BOM rows, dimensions, notes, and deterministic findings.";
  }
  return "Image: visual evidence for the session; OCR and interpretation depend on the selected model's vision support.";
}

function fileToAttachment(file: File): Promise<PendingAttachment> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error(`Could not read ${file.name}.`));
    reader.onload = () => {
      const result = typeof reader.result === "string" ? reader.result : "";
      const comma = result.indexOf(",");
      if (comma < 0) return reject(new Error(`Could not encode ${file.name}.`));
      const mimeType = file.type || (file.name.toLowerCase().endsWith(".pdf") ? "application/pdf" : "application/octet-stream");
      resolve({
        id: cryptoId(),
        name: file.name,
        mimeType,
        size: file.size,
        base64Data: result.slice(comma + 1),
        previewUrl: mimeType.startsWith("image/") ? result : undefined,
        extraction: attachmentExtraction(file.name, mimeType),
      });
    };
    reader.readAsDataURL(file);
  });
}

function readableBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function readThemeDefault(key: "accent" | "secondary", fallback: string): string {
  try {
    return window.localStorage.getItem("bb.theme." + key) ?? fallback;
  } catch {
    return fallback;
  }
}

function inkFor(hex: string): string {
  const m = /^#([0-9a-f]{6})$/i.exec((hex ?? "").trim());
  if (!m) return "#111318";
  const v = parseInt(m[1], 16);
  const r = (v >> 16) & 255;
  const g = (v >> 8) & 255;
  const b = v & 255;
  return (0.299 * r + 0.587 * g + 0.114 * b) / 255 > 0.55 ? "#111318" : "#f4f7fa";
}

// ---------------------------------------------------------------------------
// App
// ---------------------------------------------------------------------------
export function App() {
  const [modelId, setModelId] = useState<string>("UNKNOWN");
  const [pendingModel, setPendingModel] = useState<string | null>(null);
  const pendingModelRef = useRef<{ id: string; operationId: string } | null>(null);
  const latestModelOperationRef = useRef<string | null>(null);
  const [operationError, setOperationError] = useState("");
  const [models, setModels] = useState<Model[]>([]);
  const [scopeId, setScopeId] = useState<string>("UNKNOWN");
  const [scopes, setScopes] = useState<Scope[]>([]);
  const [statusBlob, setStatusBlob] = useState<unknown>({});
  const [tools, setTools] = useState<unknown[]>([]);
  const [toolReceipts, setToolReceipts] = useState<unknown[]>([]);
  const [productCatalogs, setProductCatalogs] = useState<unknown>({});
  const [accent, setAccent] = useState(() => readThemeDefault("accent", BUILT_IN_ACCENT));
  const [secondary, setSecondary] = useState(() => readThemeDefault("secondary", BUILT_IN_SECONDARY));
  const [themeOpen, setThemeOpen] = useState(false);
  const [toolbarPreferences, setToolbarPreferences] = useState<ToolbarPreferences>(readToolbarPreferences);
  const [toolbarOpen, setToolbarOpen] = useState(false);
  const [toolbarNotice, setToolbarNotice] = useState("");
  const [draggedToolbarId, setDraggedToolbarId] = useState<string | null>(null);
  const [macroName, setMacroName] = useState("");
  const [macroSource, setMacroSource] = useState("");
  const [customTemplates, setCustomTemplates] = useState<PromptTemplate[]>(readCustomTemplates);
  const [templatesOpen, setTemplatesOpen] = useState(false);
  const [templateName, setTemplateName] = useState("");
  const [templatePrompt, setTemplatePrompt] = useState("");
  const [reviewNotes, setReviewNotes] = useState<Record<string, string>>({});
  const [annotate, setAnnotate] = useState(false);
  const [pins, setPins] = useState<AnnotationPin[]>(() => {
    try {
      const raw: unknown = JSON.parse(window.localStorage.getItem("bb.pins") ?? "[]");
      const ids = new Set<string>();
      return Array.isArray(raw) ? raw.filter((p): p is AnnotationPin => {
        if (!p || ![p.id, p.selector, p.excerpt, p.note].every((v) => typeof v === "string") || !p.id || !p.selector || ids.has(p.id)) return false;
        ids.add(p.id);
        return true;
      }) : [];
    } catch { return []; }
  });
  const [pinPositions, setPinPositions] = useState<Record<string, { x: number; y: number }>>({});
  const [pinsExported, setPinsExported] = useState(false);
  const threadRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    try {
      window.localStorage.setItem(TOOLBAR_STORAGE_KEY, JSON.stringify(toolbarPreferences));
    } catch {
      /* The toolbar remains usable for the current session when storage is unavailable. */
    }
  }, [toolbarPreferences]);

  useEffect(() => {
    try {
      window.localStorage.setItem(TEMPLATE_STORAGE_KEY, JSON.stringify(customTemplates));
    } catch {}
  }, [customTemplates]);

  useEffect(() => {
    try {
      window.localStorage.setItem("bb.pins", JSON.stringify(pins));
    } catch {}
    setPinsExported(false);
  }, [pins]);

  const layoutPins = useCallback(() => {
    const root = threadRef.current;
    if (!root || !annotate) return;
    const frame = root.getBoundingClientRect();
    const next: Record<string, { x: number; y: number }> = {};
    for (const pin of pins) {
      try {
        const el = root.querySelector(pin.selector) as HTMLElement | null;
        if (!el) continue;
        const box = el.getBoundingClientRect();
        next[pin.id] = {
          x: box.left - frame.left,
          y: box.top - frame.top,
        };
      } catch {}
    }
    setPinPositions(next);
  }, [pins, annotate]);

  useLayoutEffect(() => {
    layoutPins();
  }, [layoutPins, annotate]);

  useEffect(() => {
    const thread = threadRef.current;
    if (!thread) return;
    const observer = new ResizeObserver(layoutPins);
    observer.observe(thread);
    Array.from(thread.children).forEach((child) => {
      if (!child.classList.contains("pin-layer")) observer.observe(child);
    });
    thread.addEventListener("scroll", layoutPins, true);
    thread.addEventListener("load", layoutPins, true);
    window.addEventListener("resize", layoutPins);
    if (!annotate) thread.querySelectorAll(".annotate-hover").forEach((node) => node.classList.remove("annotate-hover"));
    return () => {
      observer.disconnect();
      thread.removeEventListener("scroll", layoutPins, true);
      thread.removeEventListener("load", layoutPins, true);
      window.removeEventListener("resize", layoutPins);
    };
  }, [layoutPins, annotate]);

  const handleAnnotateClick = useCallback(
    (e: React.MouseEvent) => {
      if (!annotate) return;
      const target = e.target as HTMLElement;
      if (target.closest(".pin-editor,.pin-layer,[data-annotation-control]")) return;
      e.preventDefault();
      e.stopPropagation();
      const el = target.closest(".msg,.shot,.tool-card,.empty,.catalog-card,.composer,.select-row,.scope-chips,.chip-row") ?? target;
      const selector = cssPath(el);
      const excerpt = (el.textContent ?? "").trim().replace(/\s+/g, " ").slice(0, 120);
      setPins((prev) => [...prev, { id: cryptoId(), selector, excerpt, note: "" }]);
      setPinsExported(false);
    },
    [annotate],
  );

  const handleAnnotateHover = useCallback(
    (e: React.MouseEvent) => {
      if (!annotate) return;
      const root = threadRef.current;
      if (!root) return;
      root.querySelectorAll(".annotate-hover").forEach((n) => n.classList.remove("annotate-hover"));
      const target = e.target as HTMLElement;
      const el = target.closest(".msg,.shot,.tool-card,.empty,.catalog-card,.composer,.select-row,.scope-chips,.chip-row");
      if (el && root.contains(el)) el.classList.add("annotate-hover");
    },
    [annotate],
  );

  const updatePinNote = useCallback((id: string, note: string) => {
    setPins((prev) => prev.map((p) => (p.id === id ? { ...p, note } : p)));
    setPinsExported(false);
  }, []);

  const removePin = useCallback((id: string) => {
    setPins((prev) => prev.filter((p) => p.id !== id));
    setPinsExported(false);
  }, []);

  const exportPins = useCallback(() => {
    const bundle = {
      app: "bluebrick-assistant",
      exportedUtc: new Date().toISOString(),
      viewport: { width: window.innerWidth, height: window.innerHeight },
      pins,
    };
    const text = JSON.stringify(bundle, null, 2);
    const showJson = () => {
      const area = document.querySelector<HTMLTextAreaElement>(".pin-export-area");
      if (area) {
        area.value = text;
        area.hidden = false;
      }
    };
    try {
      const nav = window.navigator as Navigator & { clipboard?: { writeText?: (t: string) => Promise<void> } };
      if (nav.clipboard?.writeText) {
        nav.clipboard.writeText(text).then(
          () => setPinsExported(true),
          () => { showJson(); },
        );
        return;
      }
    } catch { /* fall through to textarea */ }
    const area = document.createElement("textarea");
    area.value = text;
    area.className = "pin-export-area";
    document.body.appendChild(area);
    area.select();
    let copied = false;
    try { copied = document.execCommand("copy"); } catch { copied = false; }
    document.body.removeChild(area);
    if (copied) setPinsExported(true);
    else showJson();
  }, [pins]);

  useLayoutEffect(() => {
    const root = document.documentElement;
    root.style.setProperty("--accent", accent);
    root.style.setProperty("--accent-ink", inkFor(accent));
    root.style.setProperty("--secondary", secondary);
    root.style.setProperty("--secondary-ink", inkFor(secondary));
  }, [accent, secondary]);

  const saveThemeDefault = useCallback(() => {
    try {
      window.localStorage.setItem("bb.theme.accent", accent);
      window.localStorage.setItem("bb.theme.secondary", secondary);
    } catch {
      /* storage unavailable: theme still applies for this session */
    }
    setThemeOpen(false);
  }, [accent, secondary]);

  const resetTheme = useCallback(() => {
    try {
      window.localStorage.removeItem("bb.theme.accent");
      window.localStorage.removeItem("bb.theme.secondary");
    } catch {
      /* ignore */
    }
    setAccent(BUILT_IN_ACCENT);
    setSecondary(BUILT_IN_SECONDARY);
  }, []);
  const [messages, setMessages] = useState<Message[]>(() =>
    new URLSearchParams(window.location.search).get("demo") === "packet-upload"
      ? PACKET_DEMO_INITIAL_MESSAGES
      : [],
  );
  const [streaming, setStreaming] = useState(false);
  const [input, setInput] = useState("");
  const [screenshots, setScreenshots] = useState<ScreenshotArtifact[]>([]);
  const [toolResults, setToolResults] = useState<ToolResult[]>([]);
  const [screenshotReviews, setScreenshotReviews] = useState<Record<string, string>>({});
  const [bridgeStatus, setBridgeStatus] = useState<BridgeStatus>("offline");
  const [pendingAttachments, setPendingAttachments] = useState<PendingAttachment[]>([]);
  const [isDraggingFiles, setIsDraggingFiles] = useState(false);
  const [packetPagePreview, setPacketPagePreview] = useState<PacketPagePreview | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [packetDemoNotice, setPacketDemoNotice] = useState("");
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const modelPickerRef = useRef<HTMLDetailsElement | null>(null);
  const demoPacketFilesRef = useRef<File[]>([]);
  const isPacketDemo = new URLSearchParams(window.location.search).get("demo") === "packet-upload";

  const reviewOperationsRef = useRef<Record<string, string>>({});
  const bridgeRef = useRef<BlueBrickBridge | null>(null);
  const streamingIdRef = useRef<string | null>(null);
  const messagesRef = useRef<Message[]>(messages);
  const screenshotsRef = useRef<ScreenshotArtifact[]>([]);

  // Synchronous bridge-state mirror: messagesRef.current is the canonical
  // transcript store for imperative host callbacks; React state is the
  // rendered representation of it. Every transcript mutation MUST go through
  // commitMessages so an immediately-following bbGetTranscript() observes the
  // final state without waiting for effects or paint.
  const commitMessages = useCallback(
    (
      updater:
        | Message[]
        | ((current: Message[]) => Message[]),
    ) => {
      const current = messagesRef.current;
      const next =
        typeof updater === "function"
          ? (updater as (current: Message[]) => Message[])(current)
          : updater;
      messagesRef.current = next;
      setMessages(next);
      return next;
    },
    [],
  );

  const syncScreenshots = useCallback(() => {
    setScreenshots([...screenshotsRef.current]);
  }, []);

  // -------------------------------------------------------------------------
  // Bridge handlers — all 17 host->browser callbacks
  // -------------------------------------------------------------------------
  const handlers: BlueBrickBridgeHandlers = {
    onReset: () => {
      streamingIdRef.current = null;
      screenshotsRef.current = [];
      reviewOperationsRef.current = {};
      commitMessages(isPacketDemo ? PACKET_DEMO_INITIAL_MESSAGES : []);
      syncScreenshots();
      setScreenshotReviews({});
      setToolResults([]);
      setPendingAttachments([]);
      setPacketDemoNotice("");
      setStreaming(false);
    },

    onAppend: (payload) => {
      const p =
        typeof payload === "string"
          ? safeParse(payload, { role: "", text: "", attachment: "" })
          : payload;
      const normalizedRole = p.role === "assistant" ? "assistant" : "user";

      // Defensive compatibility (frozen contract preserved): an assistant
      // bbAppend arriving while a pending record exists finalizes THAT
      // record instead of creating a second assistant message.
      const pendingId = streamingIdRef.current;
      if (normalizedRole === "assistant" && pendingId) {
        commitMessages((current) =>
          current.map((m) =>
            m.id === pendingId
              ? {
                  ...m,
                  text: typeof p.text === "string" ? p.text : "",
                  streaming: false,
                }
              : m,
          ),
        );
        setStreaming(false);
        streamingIdRef.current = null;
        return;
      }

      commitMessages((current) => [
        ...current,
        {
          id: cryptoId(),
          role: normalizedRole,
          text: p.text ?? "",
          attachment: p.attachment,
        },
      ]);
    },

    onTypingStart: () => {
      // Idempotence: handleSend already created the pending assistant record
      // for this logical request; never create a second one while active.
      if (streamingIdRef.current) return;

      setStreaming(true);
      const id = cryptoId();
      streamingIdRef.current = id;
      commitMessages((current) => [
        ...current,
        { id, role: "assistant", text: "", streaming: true },
      ]);
    },

    onAppendChunk: (text: string) => {
      const chunk = typeof text === "string" ? text : String(text ?? "");
      const sid = streamingIdRef.current;
      // Chunks update ONLY the pending assistant record created by
      // handleSend/onTypingStart; they must never spawn a new message.
      if (!sid) return;

      commitMessages((current) =>
        current.map((m) => (m.id === sid ? { ...m, text: m.text + chunk } : m)),
      );
    },

    onTypingStop: () => {
      setStreaming(false);
      const sid = streamingIdRef.current;
      if (sid) {
        commitMessages((current) =>
          current.map((m) => (m.id === sid ? { ...m, streaming: false } : m)),
        );
        streamingIdRef.current = null;
      }
    },

    onSetModel: (model: unknown) => {
      const m = model as { id?: string; Id?: string; operationId?: string; error?: string };
      const id = typeof model === "string" ? model : m?.id ?? m?.Id;
      if (m?.operationId && m.operationId !== latestModelOperationRef.current) return;
      const pending = pendingModelRef.current;
      if (pending) {
        if (m?.operationId !== pending.operationId) return;
        pendingModelRef.current = null;
        setPendingModel(null);
        if (m.error || id !== pending.id) {
          setOperationError(m.error ?? "The host did not acknowledge the requested model.");
          return;
        }
      }
      if (id) setModelId(id);
    },

    onSetModels: (rawModels: unknown[]) => {
      setModels(
        (rawModels ?? []).map((m) => {
          const mo = m as Model & { Id?: string; ProviderId?: string; Name?: string; DisplayName?: string; Available?: boolean; UnavailableReason?: string; Enabled?: boolean; SupportsTools?: boolean; SupportsJsonMode?: boolean; SupportsVision?: boolean };
          const id = mo.id ?? mo.Id ?? String(mo.displayName ?? mo.DisplayName ?? mo.id ?? "unknown");
          return {
            id,
            providerId: mo.providerId ?? mo.ProviderId,
            displayName: mo.displayName ?? mo.DisplayName ?? mo.Name ?? id,
            available: mo.available ?? mo.Available ?? mo.Enabled,
            unavailableReason: mo.unavailableReason ?? mo.UnavailableReason,
            supportsVision: mo.supportsVision ?? mo.SupportsVision,
            supportsToolCalling: mo.supportsToolCalling ?? mo.SupportsTools,
            supportsStructuredOutput: mo.supportsStructuredOutput ?? mo.SupportsJsonMode,
          };
        }),
      );
    },

    onSetScope: (rawScopeId: unknown) => {
      setScopeId(typeof rawScopeId === "string" ? rawScopeId : String(rawScopeId ?? "UNKNOWN"));
    },

    onSetScopes: (rawScopes: unknown[]) => {
      setScopes(
        (rawScopes ?? []).map((s) => {
          const sc = s as Scope & { Id?: string; Label?: string; Enabled?: boolean; UnavailableReason?: string | null };
          const id = sc.id ?? sc.Id ?? "unknown";
          return {
            id,
            label: sc.label ?? sc.Label ?? id ?? "Unknown",
            enabled: sc.enabled ?? sc.Enabled ?? true,
            unavailableReason: sc.unavailableReason ?? sc.UnavailableReason ?? undefined,
          };
        }),
      );
    },

    onSetStatus: (rawStatus: unknown) => {
      const s = rawStatus as Record<string, unknown>;
      setStatusBlob(s ?? {});
      if (s && typeof s === "object") {
        if (s.scopes && Array.isArray(s.scopes)) {
          setScopes(
            (s.scopes as unknown[]).map((sc) => {
              const scope = sc as Scope & { Id?: string; Label?: string; Enabled?: boolean; UnavailableReason?: string | null };
              const id = scope.id ?? scope.Id ?? "unknown";
              return {
                id,
                label: scope.label ?? scope.Label ?? id,
                enabled: scope.enabled ?? scope.Enabled ?? true,
                unavailableReason: scope.unavailableReason ?? scope.UnavailableReason ?? undefined,
              };
            }),
          );
        }
        if (typeof s.scopeId === "string") setScopeId(s.scopeId);
        if (typeof s.ScopeId === "string") setScopeId(s.ScopeId);
        // Status refreshes carry display labels too; only authoritative IDs may select a model.
        if (!pendingModelRef.current) {
          const descriptor = s.activeModelDescriptor as { id?: string; Id?: string } | undefined;
          const active = s.activeModel as { id?: string; Id?: string } | undefined;
          const confirmedId = descriptor?.id ?? descriptor?.Id ?? active?.id ?? active?.Id;
          if (confirmedId) setModelId(confirmedId);
        }
      }
    },

    onSetTools: (rawTools: unknown[]) => {
      setTools(rawTools ?? []);
    },

    onSetToolReceipts: (rawReceipts: unknown[]) => {
      setToolReceipts(rawReceipts ?? []);
    },

    onSetProductCatalogs: (catalogs: unknown) => {
      const c = (catalogs ?? {}) as Record<string, unknown> & {
        Integrations?: unknown;
        Documents?: unknown;
      };
      setProductCatalogs({
        integrations: c.integrations ?? c.Integrations ?? {},
        documents: c.documents ?? c.Documents ?? {},
      });
    },

    onAppendToolResult: (result: unknown) => {
      const r = result as ToolResult;
      setToolResults((prev) => [...prev, r ?? {}]);
    },

    onAppendScreenshotArtifact: (artifact: unknown) => {
      const a = artifact as ScreenshotArtifact;
      if (a) {
        screenshotsRef.current = [...screenshotsRef.current, a];
        syncScreenshots();
      }
    },

    onUpdateScreenshotArtifact: (update: unknown) => {
      const u = update as ScreenshotArtifact;
      if (u && u.screenshotId) {
        if (u.reviewOperationId) {
          if (u.reviewOperationId !== reviewOperationsRef.current[u.screenshotId]) return;
          delete reviewOperationsRef.current[u.screenshotId];
          setScreenshotReviews((previous) => ({ ...previous, [u.screenshotId!]: u.reviewError ? "failed" : "acknowledged" }));
          if (u.reviewError) setOperationError(String(u.reviewError));
          else {
            // Clear the typed note only after the host acknowledges the review;
            // a failed or timed-out review keeps it so the user can retry.
            setReviewNotes((previous) => {
              if (!(u.screenshotId! in previous)) return previous;
              const next = { ...previous };
              delete next[u.screenshotId!];
              return next;
            });
          }
        }
        screenshotsRef.current = screenshotsRef.current.map((s) =>
          s.screenshotId === u.screenshotId ? { ...s, ...u } : s,
        );
        syncScreenshots();
      }
    },

    onGetTranscript: () => {
      return messagesRef.current.filter((m) => !m.presentationOnly && !m.localDemoOnly).map((m) => ({
        role: m.role,
        text: m.text ?? "",
      }));
    },
  };

  const finalizePendingBridgeFailure = useCallback(() => {
    const pendingId = streamingIdRef.current;
    streamingIdRef.current = null;
    setStreaming(false);
    if (!pendingId) return;

    commitMessages((current) =>
      current.map((m) =>
        m.id === pendingId
          ? { ...m, text: "Bridge transport failed. Please try again.", streaming: false }
          : m,
      ),
    );
  }, [commitMessages]);

  // -------------------------------------------------------------------------
  // Install bridge on mount
  // -------------------------------------------------------------------------
  useLayoutEffect(() => {
    let mounted = true;
    setBridgeStatus("connecting");

    try {
      const bridge = createBlueBrickWindowBridge(handlers, {
        onTransportError: () => {
          if (!mounted) return;
          setBridgeStatus("error");
          finalizePendingBridgeFailure();
        },
      });
      bridgeRef.current = bridge;
      setBridgeStatus(bridge.isHostAvailable() ? "connected" : "offline");
    } catch {
      bridgeRef.current = null;
      setBridgeStatus("error");
    }

    return () => {
      bridgeRef.current = null;
      setBridgeStatus("offline");
      mounted = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // -------------------------------------------------------------------------
  // Actions
  // -------------------------------------------------------------------------
  const requestModel = useCallback(
    (id: string) => {
      if (pendingModelRef.current) return;
      if (!bridgeRef.current?.isHostAvailable()) { setOperationError("Model selection unavailable: host is offline."); return; }
      const operationId = cryptoId();
      latestModelOperationRef.current = operationId;
      pendingModelRef.current = { id, operationId };
      setPendingModel(id);
      setOperationError("");
      bridgeRef.current.post("selectModel", { type: "selectModel", modelId: id, operationId });
      window.setTimeout(() => {
        if (pendingModelRef.current?.operationId !== operationId) return;
        pendingModelRef.current = null;
        setPendingModel(null);
        setOperationError("Model selection timed out. The confirmed selection has not changed.");
      }, 30000);
    },
    [],
  );

  const handleSelectModel = useCallback(
    (model: Model) => {
      if (model.available === false || pendingModel || bridgeStatus !== "connected") return;
      lastAutoTargetRef.current = null;
      requestModel(model.id);
      modelPickerRef.current?.removeAttribute("open");
    },
    [bridgeStatus, pendingModel, requestModel],
  );

  // Auto-select the first available model when the current selection is
  // unknown or unavailable (e.g. its API key is missing). One attempt per
  // models payload; an explicit user choice always wins.
  const lastAutoTargetRef = useRef<string | null>(null);
  useEffect(() => {
    lastAutoTargetRef.current = null;
  }, [models]);
  useEffect(() => {
    if (bridgeStatus !== "connected" || pendingModelRef.current || models.length === 0) return;
    const current = models.find((m) => m.id === modelId);
    if (current && current.available !== false) return;
    const target = models.find((m) => m.available !== false) ?? models[0];
    if (!target || target.id === modelId || target.id === lastAutoTargetRef.current) return;
    lastAutoTargetRef.current = target.id;
    requestModel(target.id);
  }, [models, bridgeStatus, modelId, requestModel]);

  const handleSelectScope = useCallback(
    (s: Scope) => {
      if (!s.enabled) return;
      if (!bridgeRef.current?.isHostAvailable()) { setOperationError("Scope selection unavailable: host is offline."); return; }
      bridgeRef.current?.post("selectScope", { type: "selectScope", scopeId: s.id });
    },
    [],
  );

  const handleCapture = useCallback(() => {
    bridgeRef.current?.post("captureScreenshot", { type: "captureScreenshot" });
  }, []);

  const queueFiles = useCallback(async (incoming: FileList | File[]) => {
    const files = Array.from(incoming);
    if (!files.length) return;
    const accepted = files.filter((file) => /\.(pdf|png|jpe?g|bmp)$/i.test(file.name));
    if (accepted.length !== files.length) {
      setOperationError("Only PDF, PNG, JPG, JPEG, and BMP files can be attached.");
      return;
    }
    if (accepted.some((file) => file.size > MAX_ATTACHMENT_BYTES)) {
      setOperationError("Each attachment must be 10 MB or smaller.");
      return;
    }
    if (isPacketDemo && (accepted.length !== 2 || accepted.some((file) => !file.name.toLowerCase().endsWith(".pdf")))) {
      setOperationError("The packet demo requires exactly two PDF files selected together.");
      return;
    }
    try {
      const work = [Promise.all(accepted.map(fileToAttachment))];
      const analysis = isPacketDemo ? Promise.all(accepted.map(analyzePacketFile)) : null;
      const pendingAssistantId = isPacketDemo ? cryptoId() : "";
      if (isPacketDemo) {
        commitMessages((current) => [
          ...current.filter((message) => !message.presentationOnly),
          {
            id: cryptoId(),
            role: "user",
            text: "Review these drawing packets in upload order and separate confirmed evidence from unknowns.",
            attachments: accepted.map((file) => file.name),
          },
          { id: pendingAssistantId, role: "assistant", text: "Reading both PDF packets locally…", streaming: true },
        ]);
      }
      const [encoded] = await Promise.all(work);
      if (analysis) {
        const reviews = await analysis;
        demoPacketFilesRef.current = accepted;
        const intake = reviews.map((review, index) => `${index + 1}. ${review.fileName} · ${review.pageCount} page${review.pageCount === 1 ? "" : "s"}`).join("\n");
        commitMessages((current) => [
          ...current.filter((message) => message.id !== pendingAssistantId),
          {
            id: cryptoId(),
            role: "assistant",
            text: `DEMO · local intake receipt\n\nBoth PDFs were read from their actual page text and linked to this browser session in order:\n${intake}\n\nExtraction lanes: identifiers, title blocks, BOM rows, dimensions, notes, and deterministic findings. No file left this browser.`,
            attachments: accepted.map((file) => file.name),
          },
          {
            id: cryptoId(),
            role: "assistant",
            text: `DEMO · generated packet findings\n\n${packetEvidenceSummary(reviews)}\n\nBOUNDARY\nThese findings are confirmed from local PDF text only. CAD geometry, configurations, mass properties, and PDM revision remain unknown because no live engineering system was queried.`,
            evidence: packetEvidenceLinks(reviews),
          },
        ]);
        setPendingAttachments([]);
        setPacketDemoNotice("Both PDFs were processed locally in upload order. The receipt and findings below are deterministic demo output; no file left this browser.");
        setOperationError("");
        return;
      }
      setPendingAttachments((current) => {
        const next = [...current, ...encoded];
        const totalBytes = next.reduce((sum, file) => sum + file.size, 0);
        if (next.length > MAX_ATTACHMENT_COUNT || totalBytes > MAX_ATTACHMENT_TOTAL_BYTES) {
          setOperationError("Attach up to 8 files and 24 MB total per message.");
          return current;
        }
        setOperationError("");
        return next;
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : "One or more files could not be read.";
      setOperationError(message);
      if (isPacketDemo) {
        commitMessages((current) => current.map((item) => item.streaming ? { ...item, text: `Packet analysis failed: ${message}`, streaming: false } : item));
      }
    }
  }, [commitMessages, isPacketDemo]);

  const openPacketEvidence = useCallback(async (evidence: EvidenceLink) => {
    const file = demoPacketFilesRef.current[evidence.packetIndex];
    if (!file) {
      setOperationError("That packet is no longer available in this browser session.");
      return;
    }
    setPreviewLoading(true);
    setOperationError("");
    try {
      const rendered = await renderPacketPage(file, evidence.pageNumber);
      setPacketPagePreview({
        packetIndex: evidence.packetIndex,
        fileName: file.name,
        pageNumber: evidence.pageNumber,
        pageCount: rendered.pageCount,
        label: evidence.label,
        imageUrl: rendered.imageUrl,
        zoom: 1,
        fitMode: "width",
      });
    } catch (error) {
      setOperationError(error instanceof Error ? error.message : "The PDF page preview could not be rendered.");
    } finally {
      setPreviewLoading(false);
    }
  }, []);

  const navigatePacketPreview = useCallback(async (pageNumber: number) => {
    const current = packetPagePreview;
    if (!current || pageNumber < 1 || pageNumber > current.pageCount || previewLoading) return;
    const file = demoPacketFilesRef.current[current.packetIndex];
    if (!file) {
      setOperationError("That packet is no longer available in this browser session.");
      return;
    }
    setPreviewLoading(true);
    setOperationError("");
    try {
      const rendered = await renderPacketPage(file, pageNumber);
      setPacketPagePreview((preview) => preview ? {
        ...preview,
        pageNumber,
        pageCount: rendered.pageCount,
        imageUrl: rendered.imageUrl,
        label: `Page ${pageNumber} of ${rendered.pageCount}`,
      } : null);
    } catch (error) {
      setOperationError(error instanceof Error ? error.message : "The PDF page preview could not be rendered.");
    } finally {
      setPreviewLoading(false);
    }
  }, [packetPagePreview, previewLoading]);

  const adjustPacketPreviewZoom = useCallback((delta: number) => {
    setPacketPagePreview((preview) => preview ? {
      ...preview,
      zoom: Math.min(2, Math.max(0.5, Number((preview.zoom + delta).toFixed(2)))),
      fitMode: "custom",
    } : null);
  }, []);

  const handleAttach = useCallback(() => fileInputRef.current?.click(), []);
  const removeAttachment = useCallback((id: string) => {
    setPendingAttachments((current) => current.filter((file) => file.id !== id));
  }, []);
  const handleThreadDrop = useCallback((event: DragEvent<HTMLElement>) => {
    event.preventDefault();
    setIsDraggingFiles(false);
    void queueFiles(event.dataTransfer.files);
  }, [queueFiles]);

  const handleSearch = useCallback(() => {
    const msg = input.trim();
    if (msg) {
      bridgeRef.current?.post("search", { type: "search", message: msg, scopeId });
    }
  }, [input, scopeId]);

  const handleSend = useCallback((overrideText?: string) => {
    const msg = (overrideText ?? input).trim();
    if (!msg && pendingAttachments.length === 0) return;
    if (isPacketDemo) {
      if (!msg) return;
      commitMessages((current) => [
        ...current.filter((message) => !message.presentationOnly),
        { id: cryptoId(), role: "user", text: msg, localDemoOnly: true },
      ]);
      setInput("");
      setPacketDemoNotice("Demo note kept locally — it was not sent to the BlueBrick host or an AI model.");
      return;
    }
    if (!bridgeRef.current?.isHostAvailable()) { setOperationError("Send unavailable: host is offline."); return; }
    // Single transaction: the ref must contain the exact user + pending
    // assistant records BEFORE the host can later call bbGetTranscript.
    const pendingAssistantId = cryptoId();
    streamingIdRef.current = pendingAssistantId;
    commitMessages((current) => [
      ...current,
      { id: cryptoId(), role: "user", text: msg, attachments: pendingAttachments.map((file) => file.name) },
      { id: pendingAssistantId, role: "assistant", text: "", streaming: true },
    ]);
    setInput("");
    setStreaming(true);
    bridgeRef.current?.post("sendMessage", {
      type: "sendMessage",
      message: msg,
      scopeId,
      attachments: pendingAttachments.map(({ id, name, mimeType, size, base64Data }) => ({
        clientId: id,
        name,
        mimeType,
        size,
        base64Data,
      })),
    });
    setPendingAttachments([]);
  }, [input, scopeId, pendingAttachments, commitMessages, isPacketDemo]);

  const handleStop = useCallback(() => {
    bridgeRef.current?.post("cancelMessage", { type: "cancelMessage" });
    setStreaming(false);
    const sid = streamingIdRef.current;
    if (sid) {
      commitMessages((current) =>
        current.map((m) => (m.id === sid ? { ...m, streaming: false } : m)),
      );
      streamingIdRef.current = null;
    }
  }, [commitMessages]);

  const handleNewSession = useCallback(() => {
    if (!bridgeRef.current?.isHostAvailable()) { setOperationError("New session unavailable: host is offline."); return; }
    bridgeRef.current.post("newSession", { type: "newSession" });
  }, [commitMessages, syncScreenshots]);

  const toolbarCatalog = [...BUILT_IN_TOOLBAR_ITEMS, ...toolbarPreferences.customItems];
  const toolbarItems = toolbarPreferences.itemIds
    .map((id) => toolbarCatalog.find((item) => item.id === id))
    .filter((item): item is ToolbarItem => Boolean(item));
  const toolbarColumns = Math.max(1, Math.ceil(toolbarItems.length / toolbarPreferences.rows));
  const allTemplates = [...BUILT_IN_PROMPT_TEMPLATES, ...customTemplates];

  const runToolbarItem = useCallback((item: ToolbarItem) => {
    setToolbarNotice("");
    if (item.id === "new") return handleNewSession();
    if (item.id === "capture") return handleCapture();
    if (item.id === "search") return handleSearch();
    setToolbarNotice(`${item.label} is configured as a host-required ${item.category} action. This browser demo does not execute SOLIDWORKS or PDM operations.`);
  }, [handleCapture, handleNewSession, handleSearch]);

  const runTemplate = useCallback((template: PromptTemplate) => {
    handleSend(template.prompt);
  }, [handleSend]);

  const addToolbarItem = useCallback((id: string) => {
    setToolbarPreferences((current) => current.locked || current.itemIds.includes(id)
      ? current
      : { ...current, itemIds: [...current.itemIds, id] });
  }, []);

  const removeToolbarItem = useCallback((id: string) => {
    setToolbarPreferences((current) => current.locked
      ? current
      : { ...current, itemIds: current.itemIds.filter((itemId) => itemId !== id) });
  }, []);

  const moveToolbarItem = useCallback((id: string, direction: -1 | 1) => {
    setToolbarPreferences((current) => {
      if (current.locked) return current;
      const from = current.itemIds.indexOf(id);
      const to = from + direction;
      if (from < 0 || to < 0 || to >= current.itemIds.length) return current;
      const itemIds = [...current.itemIds];
      [itemIds[from], itemIds[to]] = [itemIds[to], itemIds[from]];
      return { ...current, itemIds };
    });
  }, []);

  const dropToolbarItem = useCallback((targetId: string) => {
    if (!draggedToolbarId || draggedToolbarId === targetId) return;
    setToolbarPreferences((current) => {
      if (current.locked) return current;
      const itemIds = current.itemIds.filter((id) => id !== draggedToolbarId);
      const targetIndex = itemIds.indexOf(targetId);
      itemIds.splice(targetIndex < 0 ? itemIds.length : targetIndex, 0, draggedToolbarId);
      return { ...current, itemIds };
    });
    setDraggedToolbarId(null);
  }, [draggedToolbarId]);

  const addCustomMacro = useCallback(() => {
    const label = macroName.trim();
    const source = macroSource.trim();
    if (!label || !source || toolbarPreferences.locked) return;
    const customItem: ToolbarItem = {
      id: `macro-${cryptoId()}`,
      label,
      description: `Custom macro metadata · ${source}`,
      symbol: "macro",
      category: "macro",
      hostRequired: true,
      source,
    };
    setToolbarPreferences((current) => ({
      ...current,
      customItems: [...current.customItems, customItem],
      itemIds: [...current.itemIds, customItem.id],
    }));
    setMacroName("");
    setMacroSource("");
    setToolbarNotice(`${label} was added as UI-only macro metadata. No file was opened or executed.`);
  }, [macroName, macroSource, toolbarPreferences.locked]);

  const addCustomTemplate = useCallback(() => {
    const label = templateName.trim();
    const prompt = templatePrompt.trim();
    if (!label || !prompt) return;
    const template: PromptTemplate = { id: `tpl-${cryptoId()}`, label, prompt };
    setCustomTemplates((current) => [...current, template]);
    setTemplateName("");
    setTemplatePrompt("");
  }, [templateName, templatePrompt]);

  const removeCustomTemplate = useCallback((id: string) => {
    setCustomTemplates((current) => current.filter((template) => template.id !== id));
  }, []);

  const handleReview = useCallback(
    (screenshotId: string, reviewStatus: "approved" | "rejected", targetType = "screenshot", note = "") => {
      if (!bridgeRef.current?.isHostAvailable()) { setOperationError("Review unavailable: host is offline."); return; }
      const operationId = cryptoId();
      reviewOperationsRef.current[screenshotId] = operationId;
      setScreenshotReviews((prev) => ({ ...prev, [screenshotId]: "pending" }));
      setOperationError("");
      bridgeRef.current.post("reviewScreenshotItem", {
        type: "reviewScreenshotItem", screenshotId, targetType, targetId: screenshotId, reviewStatus, operationId,
        reviewNote: note,
      });
      window.setTimeout(() => {
        if (reviewOperationsRef.current[screenshotId] !== operationId) return;
        delete reviewOperationsRef.current[screenshotId];
        setScreenshotReviews((prev) => ({ ...prev, [screenshotId]: "failed" }));
        setOperationError("Screenshot review timed out. The saved decision is unknown; no success is assumed.");
      }, 30000);
    }, [],
  );

  // -------------------------------------------------------------------------
  // Derived state
  // -------------------------------------------------------------------------
  // Re-seat annotation badges whenever thread content changes.
  useLayoutEffect(() => {
    layoutPins();
  }, [layoutPins, messages, screenshots, toolResults]);
  const availableModels = models.length > 0 ? models : [{ id: "UNKNOWN", displayName: "UNKNOWN" }];
  const selectedModel = availableModels.find((m) => m.id === modelId);
  const selectedModelIssue =
    selectedModel && selectedModel.available === false
      ? (selectedModel.unavailableReason ?? "This model is currently unavailable.")
      : null;
  const selectedModelLabel = selectedModel?.displayName ?? formatModelLabel(modelId);
  const pickerModels = models.length > 0 ? models : isPacketDemo ? PACKET_DEMO_MODELS : availableModels;
  const modelsByProvider = pickerModels.reduce<Record<string, Model[]>>((groups, model) => {
    const provider = model.providerId?.trim() || model.displayName.split("·")[0]?.trim() || "Other";
    (groups[provider] ??= []).push(model);
    return groups;
  }, {});
  const statusObj = statusBlob as Record<string, unknown>;
  const connectionState =
    bridgeStatus !== "connected"
      ? "HOST OFFLINE"
      : typeof (statusObj?.configured ?? statusObj?.Configured) === "boolean"
        ? "HOST CONNECTED"
        : "HOST CONNECTING";
  const modeLabel =
    typeof statusObj?.mode === "string"
      ? String(statusObj.mode)
      : typeof statusObj?.AssistantMode === "string"
        ? String(statusObj.AssistantMode)
        : "UNKNOWN";
  const providerConfigured = statusObj?.configured ?? statusObj?.Configured;
  const providerState = connectionState !== "HOST CONNECTED"
    ? "NOT VERIFIED"
    : providerConfigured !== true
      ? "UNAVAILABLE"
      : modeLabel.toLowerCase() === "mock" ? "MOCK MODE" : "CONFIGURED";
  const surface = resolveAssistantSurface(typeof window !== "undefined" ? window.location.search : "");

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------
  if (surface === "execution-board") {
    return <ExecutionBoardApp />;
  }
  if (surface === "vira-lab") {
    return <ViraLabApp search={typeof window !== "undefined" ? window.location.search : ""} />;
  }
  if (surface === "hardware-cad") {
    return (
      <main className="shell vira-command-surface">
        <header className="top"><div className="brand-row"><div className="brand"><div className="mark" aria-hidden="true" /><div className="brand-text"><span className="brand-title">VIRA Hardware Intelligence</span><span className="brand-sub">MCM CAD Acquisition</span></div></div><RuntimeIdentitySurface /></div></header>
        <HardwareCadPanel />
      </main>
    );
  }
  return (
    <main
      className={"shell vira-command-surface" + (isPacketDemo ? " packet-demo" : "") + (annotate ? " annotating" : "")}
      ref={threadRef}
      onClickCapture={handleAnnotateClick}
      onMouseOverCapture={handleAnnotateHover}
    >
      <header className="top">
        <div className="brand-row">
          <div className="brand">
            <div className="mark" aria-hidden="true" />
            <div className="brand-text">
              <span className="brand-title">BlueBrick Assistant</span>
              <span className="brand-sub">{modeLabel}</span>
            </div>
          </div>
          <RuntimeIdentitySurface />
        </div>

        {operationError && <p role="alert">{operationError}</p>}
        {pendingModel && <p role="status">Waiting for the host to acknowledge model selection…</p>}
        <div className="controls">
          <div className="scope-chips">
            {scopes.map((s) => (
              <button
                key={s.id}
                className={"scope" + (s.id === scopeId ? " selected" : "")}
                data-scope={s.id}
                disabled={!s.enabled}
                onClick={() => handleSelectScope(s)}
                aria-label={"Select scope " + s.label}
                aria-disabled={!s.enabled}
                title={"Scope " + s.label + " — " + (s.enabled ? (s.unavailableReason ?? "available") : (s.unavailableReason ?? "unavailable"))}
              >
                <span>{s.label}</span>
              </button>
            ))}
          </div>

          <div className="toolbar-shell">
            <div className="toolbar-heading">
              <button type="button" className="toolbar-config-toggle" aria-expanded={toolbarOpen} aria-controls="toolbar-configurator" onClick={() => setToolbarOpen((open) => !open)}>
                <span className="action-symbol more" aria-hidden="true" />
                Customize
              </button>
            </div>
            <div className={`primary-action-rail toolbar-rows-${toolbarPreferences.rows}`} style={{ "--toolbar-columns": toolbarColumns } as CSSProperties} aria-label="Customizable assistant toolbar">
              {toolbarItems.map((item, index) => (
                <button
                  type="button"
                  key={item.id}
                  className={`action toolbar-action ${draggedToolbarId === item.id ? "dragging" : ""}`}
                  aria-label={item.label}
                  data-tip={`${item.label} — ${item.description}${item.hostRequired ? " (host required)" : ""}`}
                  data-tip-edge={toolbarTipEdge(index, toolbarColumns, toolbarItems.length)}
                  draggable={!toolbarPreferences.locked}
                  onDragStart={(event) => {
                    if (toolbarPreferences.locked) return;
                    setDraggedToolbarId(item.id);
                    event.dataTransfer.effectAllowed = "move";
                    event.dataTransfer.setData("text/plain", item.id);
                  }}
                  onDragEnd={() => setDraggedToolbarId(null)}
                  onDragOver={(event) => { if (!toolbarPreferences.locked) event.preventDefault(); }}
                  onDrop={(event) => { event.preventDefault(); dropToolbarItem(item.id); }}
                  onClick={() => runToolbarItem(item)}
                >
                  <ToolbarIcon symbol={item.symbol} />
                  {item.hostRequired ? <span className="host-required-dot" aria-hidden="true" /> : null}
                </button>
              ))}
            </div>

            {toolbarOpen ? (
              <section id="toolbar-configurator" className="toolbar-configurator" aria-label="Toolbar configurator">
                <div className="toolbar-config-controls">
                  <div className="toolbar-segmented" aria-label="Toolbar rows">
                    <button type="button" disabled={toolbarPreferences.locked} aria-pressed={toolbarPreferences.rows === 1} onClick={() => setToolbarPreferences((current) => ({ ...current, rows: 1 }))}>1 row</button>
                    <button type="button" disabled={toolbarPreferences.locked} aria-pressed={toolbarPreferences.rows === 2} onClick={() => setToolbarPreferences((current) => ({ ...current, rows: 2 }))}>2 rows</button>
                  </div>
                  <button type="button" className="toolbar-lock" aria-pressed={toolbarPreferences.locked} onClick={() => setToolbarPreferences((current) => ({ ...current, locked: !current.locked }))}>
                    {toolbarPreferences.locked ? "Unlock layout" : "Lock layout"}
                  </button>
                </div>

                <div className="toolbar-order-list" aria-label="Current toolbar order">
                  {toolbarItems.map((item, index) => (
                    <div key={item.id} className="toolbar-order-item">
                      <ToolbarIcon symbol={item.symbol} />
                      <span title={item.description}>{item.label}</span>
                      <button type="button" title={`Move ${item.label} left`} disabled={toolbarPreferences.locked || index === 0} onClick={() => moveToolbarItem(item.id, -1)}>←</button>
                      <button type="button" title={`Move ${item.label} right`} disabled={toolbarPreferences.locked || index === toolbarItems.length - 1} onClick={() => moveToolbarItem(item.id, 1)}>→</button>
                      <button type="button" title={`Remove ${item.label}`} disabled={toolbarPreferences.locked} onClick={() => removeToolbarItem(item.id)}>×</button>
                    </div>
                  ))}
                </div>

                <div className="toolbar-catalog">
                  <strong>Tool catalog</strong>
                  <div>
                    {toolbarCatalog.filter((item) => !toolbarPreferences.itemIds.includes(item.id)).map((item) => (
                      <button type="button" key={item.id} disabled={toolbarPreferences.locked} onClick={() => addToolbarItem(item.id)} title={item.description}><span>+</span> {item.label}</button>
                    ))}
                    {toolbarCatalog.every((item) => toolbarPreferences.itemIds.includes(item.id)) ? <small>All available tools are on the toolbar.</small> : null}
                  </div>
                </div>

                <div className="toolbar-macro-form">
                  <strong>Add custom macro</strong>
                  <input aria-label="Custom macro title" placeholder="Toolbar title" value={macroName} disabled={toolbarPreferences.locked} onChange={(event) => setMacroName(event.target.value)} />
                  <input aria-label="PDM vault relative macro path" placeholder="Vault relative path · Macros\\MyMacro.swp" value={macroSource} disabled={toolbarPreferences.locked} onChange={(event) => setMacroSource(event.target.value)} />
                  <button type="button" disabled={toolbarPreferences.locked || !macroName.trim() || !macroSource.trim()} onClick={addCustomMacro}>Add macro</button>
                  <small>Stores toolbar metadata only. This browser demo never opens or runs the macro.</small>
                </div>
              </section>
            ) : null}
            {toolbarNotice ? <p className="toolbar-notice" role="status">{toolbarNotice}</p> : null}
          </div>
        </div>
      </header>

        {annotate && (
          <div className="pin-layer" aria-hidden="true">
            {pins.map((p, i) => (
              pinPositions[p.id] && (
                <span
                  key={p.id}
                  className="pin-badge"
                  style={{ left: pinPositions[p.id].x, top: pinPositions[p.id].y }}
                  title={(p.excerpt || p.selector) + (p.note ? " — " + p.note : "")}
                >
                  {i + 1}
                </span>
              )
            ))}
          </div>
        )}
      <section
        className={"thread" + (isDraggingFiles ? " file-drag-active" : "")}
        aria-live="polite"
        onDragEnter={(event) => {
          if (event.dataTransfer.types.includes("Files")) {
            event.preventDefault();
            setIsDraggingFiles(true);
          }
        }}
        onDragOver={(event) => {
          if (event.dataTransfer.types.includes("Files")) event.preventDefault();
        }}
        onDragLeave={(event) => {
          if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setIsDraggingFiles(false);
        }}
        onDrop={handleThreadDrop}
      >
        {isDraggingFiles ? <div className="drop-overlay" role="status">Drop files to add them in this order</div> : null}

        {messages.map((m) => (
          <div key={m.id} className={"msg" + (m.role === "user" ? " user" : "") + (isPacketDemo && m.role === "user" ? " packet-demo-user" : "") + (m.presentationOnly ? " packet-demo-example" : "")}>
            <div className="role">{m.presentationOnly ? "demo example · user" : m.role}</div>
            <div className="text">
              {m.text}
              {m.streaming && <span className="streaming-cursor">▋</span>}
            </div>
            {m.attachment && (
              <div className="meta">
                <span>Attachment: {m.attachment}</span>
              </div>
            )}
            {m.attachments?.length ? (
              <div className="message-attachments" aria-label="Message attachments">
                {m.attachments.map((name, index) => (
                  <span key={`${m.id}-${name}-${index}`}>{index + 1}. {name}</span>
                ))}
              </div>
            ) : null}
            {m.evidence?.length ? (
              <div className="evidence-links" aria-label="Packet page evidence">
                {m.evidence.map((evidence, index) => (
                  <button
                    type="button"
                    key={`${m.id}-${evidence.packetIndex}-${evidence.pageNumber}-${index}`}
                    onClick={() => void openPacketEvidence(evidence)}
                    disabled={previewLoading}
                  >
                    <span aria-hidden="true">↗</span> {evidence.label}
                  </button>
                ))}
              </div>
            ) : null}
          </div>
        ))}

        {packetPagePreview ? (
          <aside className="packet-page-preview" aria-label={`PDF evidence preview for ${packetPagePreview.fileName}`}>
            <div className="packet-page-preview-head">
              <div>
                <span>Local PDF evidence</span>
                <strong>{packetPagePreview.fileName} · page {packetPagePreview.pageNumber}</strong>
              </div>
              <button type="button" onClick={() => setPacketPagePreview(null)} aria-label="Close PDF evidence preview">Close</button>
            </div>
            <div className="packet-page-preview-label">{packetPagePreview.label}</div>
            <div className="packet-page-preview-controls" aria-label="PDF page and zoom controls">
              <div className="packet-page-navigation">
                <button
                  type="button"
                  onClick={() => void navigatePacketPreview(packetPagePreview.pageNumber - 1)}
                  disabled={previewLoading || packetPagePreview.pageNumber <= 1}
                  aria-label="Previous PDF page"
                >← Previous</button>
                <span aria-live="polite">{packetPagePreview.pageNumber} / {packetPagePreview.pageCount}</span>
                <button
                  type="button"
                  onClick={() => void navigatePacketPreview(packetPagePreview.pageNumber + 1)}
                  disabled={previewLoading || packetPagePreview.pageNumber >= packetPagePreview.pageCount}
                  aria-label="Next PDF page"
                >Next →</button>
              </div>
              <div className="packet-page-zoom">
                <button
                  type="button"
                  className="packet-page-fit-button"
                  onClick={() => setPacketPagePreview((preview) => preview ? { ...preview, zoom: 1, fitMode: "width" } : null)}
                  aria-label="Fit PDF to width"
                  aria-pressed={packetPagePreview.fitMode === "width"}
                >Fit width</button>
                <button
                  type="button"
                  className="packet-page-fit-button"
                  onClick={() => setPacketPagePreview((preview) => preview ? { ...preview, zoom: 1, fitMode: "page" } : null)}
                  aria-label="Fit entire PDF page"
                  aria-pressed={packetPagePreview.fitMode === "page"}
                >Fit page</button>
                <button type="button" onClick={() => adjustPacketPreviewZoom(-0.25)} disabled={packetPagePreview.zoom <= 0.5} aria-label="Zoom out">−</button>
                <button type="button" onClick={() => setPacketPagePreview((preview) => preview ? { ...preview, zoom: 1, fitMode: "custom" } : null)} aria-label="Reset PDF zoom">{packetPagePreview.fitMode === "custom" ? `${Math.round(packetPagePreview.zoom * 100)}%` : "100%"}</button>
                <button type="button" onClick={() => adjustPacketPreviewZoom(0.25)} disabled={packetPagePreview.zoom >= 2} aria-label="Zoom in">+</button>
              </div>
            </div>
            <div className={`packet-page-preview-canvas is-fit-${packetPagePreview.fitMode}`}>
              <img
                src={packetPagePreview.imageUrl}
                alt={`${packetPagePreview.fileName}, page ${packetPagePreview.pageNumber}`}
                style={packetPagePreview.fitMode === "custom" ? { width: `${packetPagePreview.zoom * 100}%` } : undefined}
              />
            </div>
          </aside>
        ) : null}

        {screenshots.map((s) => (
          <div key={s.screenshotId ?? s.artifactId ?? s.fileName ?? "screenshot"} className="shot">
            {s.thumbnailDataUrl?.startsWith("data:image/jpeg;base64,") && (
              <img src={s.thumbnailDataUrl} alt={s.attachedToConversation ? "Screenshot attached to this conversation" : "Locally captured screenshot"} style={{ maxWidth: "100%", maxHeight: 220, objectFit: "contain" }} />
            )}
            <div role="status">{s.attachedToConversation ? "Attached to this conversation" : "Local capture — not attached"} · {s.approvalMode ?? "MANUAL"} local review · Upload consent is separate</div>
            <div className="shot-head">
              <strong className="badge">Screenshot captured</strong>
              <span className="badge">
                {s.width && s.height ? `${s.width} x ${s.height}` : "size unknown"}
              </span>
            </div>
            {s.fileName && <div className="catalog-sub">{s.fileName}</div>}
            {s.sourceWindowTitle && <div className="catalog-sub">{s.sourceWindowTitle}</div>}
            <div className="meta">
              <span>
                local only: {String(s.localOnlyCloudState ?? "local only")}
              </span>
              <span>
                annotations: {s.annotations?.length ?? 0} · contacts: {s.contacts?.length ?? 0}
              </span>
            </div>
            {s.annotations && s.annotations.length > 0 && (
              <div className="list">
                {s.annotations.map((a, i) => (
                  <div key={a.id ?? i} className="receipt-summary">
                    <strong>{a.label ?? "Annotation"}</strong>
                    <span>{a.source ?? "unknown"} · {a.reviewStatus ?? "pending"}</span>
                  </div>
                ))}
              </div>
            )}
            {s.contacts && s.contacts.length > 0 && (
              <div className="list">
                {s.contacts.map((c, i) => (
                  <div key={c.id ?? i} className="receipt-summary">
                    <strong>{c.name ?? "Contact"}</strong>
                    <span>{c.email ?? ""} · {c.reviewStatus ?? "pending"}</span>
                  </div>
                ))}
              </div>
            )}
            <div className="review-actions">
              <input
                className="note-input"
                aria-label={"Review note for screenshot " + (s.screenshotId ?? "")}
                placeholder="Add a review note (optional)…"
                value={reviewNotes[s.screenshotId ?? ""] ?? ""}
                onChange={(e) => setReviewNotes((prev) => ({ ...prev, [s.screenshotId ?? ""]: e.target.value }))}
              />
              <button
                aria-label={"Approve screenshot " + (s.screenshotId ?? "")}
                disabled={screenshotReviews[s.screenshotId ?? ""] === "pending" || s.reviewStatus === "approved"}
                onClick={() => s.screenshotId && handleReview(s.screenshotId, "approved", "screenshot", reviewNotes[s.screenshotId ?? ""] ?? "")}
              >
                Approve
              </button>
              <button aria-label="Reject screenshot review"
                disabled={screenshotReviews[s.screenshotId ?? ""] === "pending"}
                onClick={() => s.screenshotId && handleReview(s.screenshotId, "rejected", "screenshot", reviewNotes[s.screenshotId ?? ""] ?? "")}>
                Reject
              </button>
              <button
                disabled={s.reviewStatus !== "approved" || s.cloudSendApproved === true || screenshotReviews[s.screenshotId ?? ""] === "pending"}
                onClick={() => s.screenshotId && handleReview(s.screenshotId, "approved", "screenshot-upload", reviewNotes[s.screenshotId ?? ""] ?? "")}>
                Allow image upload
              </button>
              <span role="status">{screenshotReviews[s.screenshotId ?? ""] === "pending" ? "Saving decision…" : String(s.reviewStatus ?? "pending")}
                {s.cloudSendApproved === true ? " · upload allowed" : " · local only"}</span>
            </div>
          </div>
        ))}

        {toolResults.map((t, i) => (
          <div key={i} className="tool-card">
            <div className="tool-head">
              <strong className="badge">{t.label ?? "Tool result"}</strong>
              <span className={"badge " + (t.status === "done" ? "ok" : "warn")}>
                {t.status ?? "done"}
              </span>
            </div>
            {t.message && <div className="catalog-sub">{t.message}</div>}
            {t.query && <div className="catalog-sub">Query: {t.query}</div>}
          </div>
        ))}
      </section>

      {annotate && (
        <section className="pin-editor" aria-label="Annotation pins">
          <div className="pin-editor-head">
            <strong>Annotations ({pins.length})</strong>
            <div className="pin-editor-actions">
              <button onClick={exportPins} disabled={pins.length === 0}>
                {pinsExported ? "Copied ✓" : "Copy JSON"}
              </button>
              <button onClick={() => setPins([])} disabled={pins.length === 0}>Clear</button>
            </div>
          </div>
          {pinsExported && <p className="model-note ok" role="status">Copied — paste it into chat and I will implement each pin.</p>}
          <textarea
            className="pin-export-area"
            aria-label="Annotation JSON export"
            readOnly
            rows={5}
            hidden
            onFocus={(e) => e.currentTarget.select()}
          />
          {pins.map((p, i) => (
            <div key={p.id} className="pin-row">
              <span className="pin-badge static" title={p.selector}>{i + 1}</span>
              <div className="pin-body">
                <code className="pin-selector" title={p.selector}>{p.selector}</code>
                {p.excerpt && <div className="catalog-sub">{p.excerpt}</div>}
                <textarea
                  className="note-input"
                  aria-label={"Note for pin " + (i + 1)}
                  placeholder="What should change here?…"
                  value={p.note}
                  rows={2}
                  onChange={(e) => updatePinNote(p.id, e.target.value)}
                />
              </div>
              <button aria-label={"Remove pin " + (i + 1)} onClick={() => removePin(p.id)}>✕</button>
            </div>
          ))}
          {pins.length === 0 && <p className="catalog-sub">Click anything above to pin it, then describe the change here.</p>}
        </section>
      )}

      <footer className="footer">
        {pendingAttachments.length ? (
          <div className="attachment-tray" aria-label="Files linked to the next message">
            {pendingAttachments.map((file, index) => (
              <div className="attachment-item" key={file.id} tabIndex={0}>
                <div className={"attachment-thumbnail" + (file.previewUrl ? " image" : " pdf")}>
                  {file.previewUrl ? <img src={file.previewUrl} alt="" /> : <span>PDF</span>}
                  <b aria-label={`Attachment order ${index + 1}`}>{index + 1}</b>
                </div>
                <div className="attachment-popout" role="tooltip">
                  <strong>{file.name}</strong>
                  <span>{readableBytes(file.size)} · linked as context #{index + 1}</span>
                  <span>{file.extraction}</span>
                  <button type="button" onClick={() => removeAttachment(file.id)} aria-label={`Remove ${file.name}`}>Remove</button>
                </div>
              </div>
            ))}
          </div>
        ) : null}
        <div className="composer">
          <input
            ref={fileInputRef}
            className="file-input"
            type="file"
            accept={ACCEPTED_ATTACHMENT_TYPES}
            multiple
            aria-label="Choose PDF or image files"
            onChange={(event) => {
              if (event.currentTarget.files) void queueFiles(event.currentTarget.files);
              event.currentTarget.value = "";
            }}
          />
          <div className="composer-template-rail" aria-label="Prompt templates">
            {allTemplates.map((template) => (
              <button
                type="button"
                key={template.id}
                className="template-chip"
                title={template.prompt}
                onClick={() => runTemplate(template)}
              >
                {template.label}
              </button>
            ))}
            <button
              type="button"
              className="template-add"
              aria-expanded={templatesOpen}
              aria-controls="template-configurator"
              title="Add or remove custom prompt templates"
              onClick={() => setTemplatesOpen((open) => !open)}
            >
              +
            </button>
          </div>
          {templatesOpen ? (
            <section id="template-configurator" className="template-configurator" aria-label="Prompt template configurator">
              <div className="template-custom-form">
                <strong>Add custom template</strong>
                <input aria-label="Custom template label" placeholder="Chip label" value={templateName} onChange={(event) => setTemplateName(event.target.value)} />
                <input aria-label="Custom template prompt text" placeholder="Prompt text sent when the chip is clicked" value={templatePrompt} onChange={(event) => setTemplatePrompt(event.target.value)} />
                <button type="button" disabled={!templateName.trim() || !templatePrompt.trim()} onClick={addCustomTemplate}>Add template</button>
                <small>Stored on this machine. Clicking a chip sends its prompt with the current attachments.</small>
              </div>
              {customTemplates.length ? (
                <div className="template-custom-list" aria-label="Custom templates">
                  {customTemplates.map((template) => (
                    <div key={template.id} className="template-custom-item">
                      <span title={template.prompt}>{template.label}</span>
                      <button type="button" title={`Remove ${template.label}`} onClick={() => removeCustomTemplate(template.id)}>×</button>
                    </div>
                  ))}
                </div>
              ) : null}
            </section>
          ) : null}
          <div className="composer-input-shell">
            <textarea
              aria-label="Message BlueBrick Assistant"
              placeholder="Ask anything, attach files, or use the controls below"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && !e.shiftKey) {
                  e.preventDefault();
                  if (streaming) handleStop();
                  else handleSend();
                }
              }}
            />
            <div className="composer-context-rail">
              <details className="model-picker" ref={modelPickerRef}>
                <summary aria-label={`Choose provider and model. Current model: ${selectedModelLabel}`}>
                  <span className="model-picker-mark" aria-hidden="true">◆</span>
                  <span className="model-picker-label">{selectedModelLabel}</span>
                  <span className="model-picker-chevron" aria-hidden="true">⌄</span>
                </summary>
                <div className="model-picker-menu" role="dialog" aria-label="Choose an AI provider and model">
                  <div className="model-picker-menu-head">
                    <strong>Provider & model</strong>
                    <span>{models.length > 0 ? `${models.length} from host` : isPacketDemo ? "Demo catalog · host offline" : "0 from host"}</span>
                  </div>
                  {models.length === 0 && !isPacketDemo ? (
                    <p className="model-picker-empty">No models received. Connect the BlueBrick host to load configured providers.</p>
                  ) : Object.entries(modelsByProvider).map(([provider, providerModels]) => (
                    <details className="provider-group" key={provider} open={providerModels.some((model) => model.id === modelId)}>
                      <summary>
                        <span>{formatProviderLabel(provider)}</span>
                        <small>{providerModels.length}</small>
                      </summary>
                      <div className="provider-models">
                        {providerModels.map((model) => (
                          <button
                            type="button"
                            key={model.id}
                            className={model.id === modelId ? "selected" : ""}
                            disabled={model.available === false || !!pendingModel || bridgeStatus !== "connected"}
                            onClick={() => handleSelectModel(model)}
                            title={model.available === false ? (model.unavailableReason ?? "Unavailable") : model.displayName}
                          >
                            <span>{model.displayName}</span>
                            <small>{model.id === modelId ? "Active" : model.available === false ? "Unavailable" : "Select"}</small>
                          </button>
                        ))}
                      </div>
                    </details>
                  ))}
                  {selectedModelIssue && <p className="model-picker-empty" role="status">Selected model unavailable: {selectedModelIssue}</p>}
                </div>
              </details>
              <button
                type="button"
                className="paperclip-button"
                aria-label="Attach PDF or image files"
                title="Attach multiple files"
                onClick={handleAttach}
              >
                <ToolbarIcon symbol="attach" />
              </button>
            </div>
          </div>
          <div className="composer-actions">
            {streaming ? (
              <button
                className="stop-button"
                aria-label="Stop streaming response"
                onClick={handleStop}
              >
                Stop
              </button>
            ) : (
              <button
                className="send-button"
                aria-label="Send message"
                onClick={() => handleSend()}
                disabled={!input.trim() && pendingAttachments.length === 0}
              >
                Send
              </button>
            )}
          </div>
        </div>
        {isPacketDemo && packetDemoNotice ? <p className="packet-demo-notice" role="status">{packetDemoNotice}</p> : null}
        <div
          className="safety-footer"
          title="Local-first: screenshots stay local unless explicitly approved. Bridge status reflects the local host connection."
        >
          <span className={"safety-dot" + (bridgeStatus === "connected" ? "" : " off")} aria-hidden="true">●</span>
          <span className="safety-label">Local-first</span>
          <span className="safety-spacer" aria-hidden="true" />
          <span className="safety-bridge">{connectionState}</span>
        </div>
      </footer>
    </main>
  );
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
function formatProviderLabel(provider: string): string {
  return provider
    .replace(/[_-]+/g, " ")
    .replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function cryptoId(): string {
  // crypto.randomUUID when available, deterministic fallback otherwise.
  try {
    if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
      return crypto.randomUUID();
    }
  } catch {
    /* fall through */
  }
  return "id-" + Date.now().toString(36) + "-" + Math.random().toString(36).slice(2, 10);
}

/**
 * Compact CSS path for annotation pins: stable enough to re-locate the
 * element for badge layout and to tell the implementer exactly which
 * node a note refers to. Presentation-only; never sent to the host.
 */
function cssPath(el: Element | null): string {
  if (!el || el === document.body) return "body";
  const parts: string[] = [];
  let node: Element | null = el;
  while (node && node !== document.body && parts.length < 6) {
    let part = node.tagName.toLowerCase();
    const cls =
      typeof node.className === "string"
        ? node.className.split(/\s+/).filter((c) => c && !c.startsWith("annotate"))[0]
        : "";
    const id = node.getAttribute("id");
    if (id) {
      parts.unshift(part + "#" + id);
      break;
    }
    if (cls) part += "." + cls;
    const parent: Element | null = node.parentElement;
    if (parent) {
      const tag: string = (node as Element).tagName;
      const sameTag: Element[] = Array.from(parent.children).filter(
        (c: Element) => c.tagName === tag,
      );
      if (sameTag.length > 1) part += ":nth-of-type(" + (sameTag.indexOf(node as Element) + 1) + ")";
    }
    parts.unshift(part);
    node = parent;
  }
  return parts.join(" > ");
}

/**
 * Presentation-only label formatting for model identifiers that arrive
 * without backend display metadata. Model IDs crossing the bridge are
 * never modified.
 */
function formatModelLabel(id: string): string {
  if (!id) return id;
  return id
    .split(/[-_]+/)
    .filter(Boolean)
    .map((part) => {
      if (/^\d+(\.\d+)*$/.test(part)) return part;
      if (part.toLowerCase() === "gpt") return part.toUpperCase();
      if (part.length <= 3 && !/\d/.test(part)) return part.toUpperCase();
      return part.charAt(0).toUpperCase() + part.slice(1);
    })
    .join(" ");
}

function safeParse<T>(raw: string, fallback: T): T {
  try {
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
}
