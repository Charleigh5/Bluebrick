/**
 * Task 3 behavioral verifier for the production inline fallback bbAppend.
 * It extracts and executes the handler body from AssistantPanel.cs in a small
 * dependency-free DOM harness, covering object, JSON-string, malformed, and
 * empty inputs without launching the host application.
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const sourcePath = join(root, "..", "AssistantPanel.cs");
const source = readFileSync(sourcePath, "utf8");
const buildShellMarker = "private static string BuildShellHtml()";
const buildShellStart = source.indexOf(buildShellMarker);
const buildShellEnd = source.indexOf("\n        private ", buildShellStart + buildShellMarker.length);
const marker = "window.bbAppend=function(raw){";
const markerMatches = [...source.matchAll(/window\.bbAppend=function\(raw\)\{/g)];
const start = markerMatches[0]?.index ?? -1;
const end = source.indexOf("\n};", start);

if (buildShellStart < 0 || buildShellEnd < 0) {
  throw new Error("Could not locate the BuildShellHtml method boundary.");
}
if (markerMatches.length !== 1 || start < buildShellStart || start >= buildShellEnd) {
  throw new Error(
    `Expected one production bbAppend marker inside BuildShellHtml; found ${markerMatches.length}.`,
  );
}
if (end < 0 || end >= buildShellEnd) {
  throw new Error("Could not locate the end of the BuildShellHtml bbAppend handler.");
}

const handlerBody = source.slice(start + marker.length, end);

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function createHarness() {
  class FakeElement {
    constructor() {
      this.children = [];
      this.className = "";
      this.textContent = "";
      this.style = { display: "" };
      this.removed = false;
    }

    appendChild(child) {
      this.children.push(child);
      return child;
    }

    remove() {
      this.removed = true;
    }
  }

  const empty = new FakeElement();
  const log = new FakeElement();
  const typingIndicator = new FakeElement();
  const modelName = new FakeElement();
  modelName.textContent = "Verifier Model";
  const byId = { log, typingIndicator, modelName };
  const document = {
    body: { scrollHeight: 1 },
    querySelector(selector) {
      return selector === ".empty" ? (empty.removed ? null : empty) : null;
    },
    createElement() {
      return new FakeElement();
    },
    getElementById(id) {
      return byId[id] ?? null;
    }
  };
  const window = { scrollTo() {} };
  const context = { document, window, _modelColor: "#3ba7a4" };
  vm.runInNewContext(`window.bbAppend=function(raw){${handlerBody}\n};`, context);
  return { window, empty, log, typingIndicator };
}

const objectHarness = createHarness();
objectHarness.window.bbAppend({ role: "assistant", text: "object form", attachment: "part.png" });
assert(objectHarness.log.children.length === 1, "Object payload must append one message.");
assert(objectHarness.log.children[0].className === "msg assistant", "Object payload role/class was not rendered.");
assert(objectHarness.log.children[0].textContent === "object form", "Object payload text was not rendered.");
const objectMeta = objectHarness.log.children[0].children.filter((child) => child.className === "meta");
assert(objectMeta.length === 1, "Object payload attachment must render one metadata child.");
assert(objectMeta[0].textContent === "Attachment: part.png", "Object payload attachment was not rendered.");
assert(objectHarness.empty.removed, "Object payload must remove the empty state.");
assert(objectHarness.typingIndicator.style.display === "none", "Object payload must hide the typing indicator.");

const stringHarness = createHarness();
stringHarness.window.bbAppend(JSON.stringify({ role: "user", text: "JSON string form" }));
assert(stringHarness.log.children.length === 1, "JSON-string payload must append one message.");
assert(stringHarness.log.children[0].className === "msg user", "JSON-string payload role/class was not rendered.");
assert(stringHarness.log.children[0].textContent === "JSON string form", "JSON-string payload text was not rendered.");
assert(stringHarness.empty.removed, "JSON-string payload must remove the empty state.");
assert(stringHarness.typingIndicator.style.display === "none", "JSON-string payload must hide the typing indicator.");

const invalidHarness = createHarness();
let malformedThrew = false;
let emptyThrew = false;
try {
  invalidHarness.window.bbAppend("{malformed");
} catch {
  malformedThrew = true;
}
try {
  invalidHarness.window.bbAppend("");
} catch {
  emptyThrew = true;
}
assert(!malformedThrew && !emptyThrew, "Malformed and empty payloads must be ignored without throwing.");
assert(invalidHarness.log.children.length === 0, "Invalid payloads must not append a message.");
assert(!invalidHarness.empty.removed, "Invalid payloads must not disturb the empty state.");
assert(invalidHarness.typingIndicator.style.display === "", "Invalid payloads must not disturb the typing indicator.");

console.log(JSON.stringify({
  ok: true,
  checked: ["object form", "JSON-string form", "malformed input", "empty input"],
  source: sourcePath,
  writesRepo: false,
  launchesSolidWorks: false,
  callsExternalSystems: false
}, null, 2));
