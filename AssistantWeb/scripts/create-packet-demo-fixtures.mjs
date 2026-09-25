import { mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { tmpdir } from "node:os";

const outputRoot = process.argv[2] || join(tmpdir(), "bluebrick-packet-demo");
await mkdir(outputRoot, { recursive: true });

const fixtures = [
  {
    name: "Walmart-ASY511185-80238229.pdf",
    lines: [
      "WALMART RETAIL FIXTURE",
      "DRAWING NO ASY511185-80238229",
      "REV B",
      "DESCRIPTION: OPTICAL VALUE SIGN HOLDER",
      "MATERIAL: A1008",
      "THICKNESS: 0.0747 IN",
      "CONFIGURATION: Default",
      "SHEET 1 OF 1",
      "ITEM QTY PART NUMBER DESCRIPTION",
      "1 2 PRT511284-80241102 BRACKET",
      "2 1 PRT511285-80241103 PANEL",
      "1. REMOVE ALL SHARP EDGES.",
      "2. FINISH: BLACK POWDER COAT.",
      "12.50 +/-0.02",
    ],
  },
  {
    name: "ASY511185-80238229-detail-sheets.pdf",
    lines: [
      "DETAIL A",
      "DRAWING NO PRT511284-80241102",
      "REV B",
      "SHEET 1 OF 1",
      "4X DIA .250 THRU",
      "90 DEG",
      "1. DEBURR ALL EDGES.",
    ],
  },
];

function createPdf(lines) {
  const escapePdf = (value) => value.replaceAll("\\", "\\\\").replaceAll("(", "\\(").replaceAll(")", "\\)");
  const content = ["BT", "/F1 10 Tf", "42 748 Td", ...lines.flatMap((line, index) => [index ? "0 -17 Td" : "", `(${escapePdf(line)}) Tj`]).filter(Boolean), "ET"].join("\n");
  const objects = [
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
    `<< /Length ${Buffer.byteLength(content, "ascii")} >>\nstream\n${content}\nendstream`,
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
  ];
  let pdf = "%PDF-1.4\n";
  const offsets = [0];
  objects.forEach((object, index) => {
    offsets.push(Buffer.byteLength(pdf, "ascii"));
    pdf += `${index + 1} 0 obj\n${object}\nendobj\n`;
  });
  const xref = Buffer.byteLength(pdf, "ascii");
  pdf += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  for (const offset of offsets.slice(1)) pdf += `${String(offset).padStart(10, "0")} 00000 n \n`;
  return Buffer.from(`${pdf}trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`, "ascii");
}

for (const fixture of fixtures) {
  await writeFile(join(outputRoot, fixture.name), createPdf(fixture.lines));
}

console.log(JSON.stringify({ outputRoot, files: fixtures.map((fixture) => join(outputRoot, fixture.name)) }, null, 2));
