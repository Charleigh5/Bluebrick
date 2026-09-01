import { writeFile } from "node:fs/promises";
import { join } from "node:path";
import { tmpdir } from "node:os";

const output = process.argv[2] || join(tmpdir(), "vira-phase-b-browser-fixture.pdf");
const lines = [
  "DRAWING NO ASY511185-80238229",
  "REV B",
  "DESCRIPTION: OPTICAL VALUE SIGN HOLDER",
  "SHEET 1 OF 1",
  "ITEM QTY PART NUMBER DESCRIPTION",
  "1 2 MPM511284-80241102 MOUNTING BRACKET",
  "2 1 PBO511290-80241108 PURCHASED BY OTHERS"
];
const escapePdf = (value) => value.replaceAll("\\", "\\\\").replaceAll("(", "\\(").replaceAll(")", "\\)");
const content = ["BT", "/F1 11 Tf", "48 740 Td", ...lines.flatMap((line, index) => [index ? "0 -18 Td" : "", `(${escapePdf(line)}) Tj`]).filter(Boolean), "ET"].join("\n");
const objects = [
  "<< /Type /Catalog /Pages 2 0 R >>",
  "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
  "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
  `<< /Length ${Buffer.byteLength(content, "ascii")} >>\nstream\n${content}\nendstream`,
  "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
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
pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
await writeFile(output, Buffer.from(pdf, "ascii"));
console.log(output);
