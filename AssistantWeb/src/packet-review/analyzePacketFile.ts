import { GlobalWorkerOptions, getDocument } from "pdfjs-dist";
import pdfWorkerUrl from "pdfjs-dist/build/pdf.worker.min.mjs?url&inline";
import { analyzePacketPages, type PacketReview } from "./packetReview";

GlobalWorkerOptions.workerSrc = pdfWorkerUrl;

export async function analyzePacketFile(file: File): Promise<PacketReview> {
  if (file.type && file.type !== "application/pdf" && !file.name.toLowerCase().endsWith(".pdf")) {
    throw new Error(`${file.name} is not a PDF packet.`);
  }

  const document = await getDocument({ data: new Uint8Array(await file.arrayBuffer()) }).promise;
  try {
    const pages = await Promise.all(
      Array.from({ length: document.numPages }, async (_, index) => {
        const pageNumber = index + 1;
        const page = await document.getPage(pageNumber);
        const content = await page.getTextContent();
        return {
          pageNumber,
          text: content.items
            .map((item) => ("str" in item ? item.str : ""))
            .filter(Boolean)
            .join("\n"),
        };
      }),
    );
    return analyzePacketPages(file.name, pages);
  } finally {
    await document.cleanup();
  }
}

export type PacketPageRender = {
  imageUrl: string;
  pageCount: number;
};

export async function renderPacketPage(file: File, pageNumber: number): Promise<PacketPageRender> {
  const document = await getDocument({ data: new Uint8Array(await file.arrayBuffer()) }).promise;
  try {
    if (pageNumber < 1 || pageNumber > document.numPages) throw new Error(`Page ${pageNumber} is outside ${file.name}.`);
    const page = await document.getPage(pageNumber);
    const base = page.getViewport({ scale: 1 });
    const scale = Math.min(1.8, 860 / base.width);
    const viewport = page.getViewport({ scale });
    const canvas = window.document.createElement("canvas");
    const ratio = window.devicePixelRatio || 1;
    canvas.width = Math.floor(viewport.width * ratio);
    canvas.height = Math.floor(viewport.height * ratio);
    const context = canvas.getContext("2d");
    if (!context) throw new Error("PDF preview canvas is unavailable.");
    context.setTransform(ratio, 0, 0, ratio, 0, 0);
    await page.render({ canvas, canvasContext: context, viewport }).promise;
    return { imageUrl: canvas.toDataURL("image/png"), pageCount: document.numPages };
  } finally {
    await document.cleanup();
  }
}
