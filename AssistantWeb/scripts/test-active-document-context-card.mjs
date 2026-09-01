import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const { activeDocumentContextFromToolResult, normalizeActiveDocumentContext } = await import("../src/activeDocumentContext.ts");

const cases = [
  [
    "part document",
    {
      status: "ok",
      message: "Redacted active SOLIDWORKS document context captured.",
      items: [{ metadata: { document_type: "PART", document_title_hash: "sha256:part", is_dirty: "false", is_read_only: "true", custom_property_count: "3" } }]
    },
    { state: "ready", documentType: "Part", titleHash: "sha256:part", customPropertyCount: 3 }
  ],
  ["assembly document", { status: "ok", items: [{ metadata: { document_type: "ASSEMBLY" } }] }, { state: "ready", documentType: "Assembly" }],
  ["drawing document", { status: "ok", items: [{ metadata: { document_type: "DRAWING" } }] }, { state: "ready", documentType: "Drawing" }],
  ["no document", { status: "no_active_document", message: "No active SOLIDWORKS document is available." }, { state: "no-document", documentType: "No active document" }],
  ["loading", { status: "loading" }, { state: "loading" }],
  ["unavailable", { status: "disabled", message: "Adapter unavailable." }, { state: "unavailable" }],
  ["error", { status: "error", message: "Reader error." }, { state: "error" }]
];

for (const [name, raw, expected] of cases) {
  const actual = normalizeActiveDocumentContext(raw);
  for (const [key, value] of Object.entries(expected)) {
    assert.equal(actual[key], value, `${name}: ${key}`);
  }
  assert.equal(actual.mutationActions, 0, `${name}: mutation actions remain zero`);
}

const toolResult = activeDocumentContextFromToolResult({
  label: "Read Active Document Context",
  status: "ok",
  receipt: { ToolName: "read_active_document_context" },
  items: [{ Metadata: { document_type: "PART" } }]
});
assert.equal(toolResult?.state, "ready", "host tool result is recognized through its receipt");
assert.equal(activeDocumentContextFromToolResult({ label: "Search Local Vault" }), null, "unrelated tool results are ignored");

const appSource = await readFile(new URL("../src/App.tsx", import.meta.url), "utf8");
const cardSource = await readFile(new URL("../src/ActiveDocumentContextCard.tsx", import.meta.url), "utf8");
const styleSource = await readFile(new URL("../src/styles.css", import.meta.url), "utf8");
assert.match(appSource, /activeDocumentContextFromToolResult\(result\)/, "tool-result bridge updates the card state");
assert.match(appSource, /<ActiveDocumentContextCard context=\{activeDocumentContext\} \/>/, "card is rendered in the existing shell");
assert.match(cardSource, /aria-live="polite"/, "card announces context-state changes accessibly");
assert.match(cardSource, /Read-only snapshot/, "card keeps the action boundary visible");
assert.match(styleSource, /grid-template-columns: repeat\(2, minmax\(0, 1fr\)\)/, "card uses a narrow two-column metric layout");

console.log(JSON.stringify({ ok: true, tests: cases.map(([name]) => name) }, null, 2));
