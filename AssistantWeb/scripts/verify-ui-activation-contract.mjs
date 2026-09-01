import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(root);

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function read(relativePath) {
  return readFileSync(join(repoRoot, relativePath), "utf8");
}

function readJson(relativePath) {
  return JSON.parse(read(relativePath));
}

const expectedDist = [
  "AssistantWeb\\dist\\index.html",
  "AssistantWeb\\dist\\assistant-index.css",
  "AssistantWeb\\dist\\assistant-web.js",
];

for (const configPath of ["config/appsettings.json", "config/appsettings.lab.json"]) {
  const config = readJson(configPath);
  assert(
    config?.Assistant?.UseReactWebView === true,
    `${configPath} must explicitly set Assistant.UseReactWebView to true.`,
  );
}

const project = read("BlueBrick.csproj");
for (const contentPath of [
  "config\\appsettings.json",
  "config\\appsettings.lab.json",
  ...expectedDist,
]) {
  const escaped = contentPath.replace(/\\/g, "\\\\");
  const contentPattern = new RegExp(
    `<Content Include="${escaped}">\\s*<CopyToOutputDirectory>PreserveNewest<\\/CopyToOutputDirectory>\\s*<\\/Content>`,
    "m",
  );
  assert(contentPattern.test(project), `BlueBrick.csproj must package ${contentPath} with PreserveNewest.`);
}

for (const relativePath of expectedDist) {
  assert(existsSync(join(repoRoot, relativePath)), `Recovered dist member is missing: ${relativePath}`);
}

const bundledDist = expectedDist
  .map((relativePath) => readFileSync(join(repoRoot, relativePath), "utf8"))
  .join("\n");
const forbiddenDistIdentifiers = [
  "X-Agent" + "-Auth",
  ".agent" + "_token",
  "OPENAI" + "_API_KEY",
  "NVIDIA" + "_API_KEY",
  "ANTHROPIC" + "_API_KEY",
  "GEMINI" + "_API_KEY",
  "SALESFORCE" + "_ACCESS_TOKEN",
  "SALESFORCE" + "_REFRESH_TOKEN",
  "DATABASE" + "_URL",
];
for (const identifier of forbiddenDistIdentifiers) {
  assert(!bundledDist.includes(identifier), `React dist must not expose secret identifier: ${identifier}`);
}

const host = read("Agent/AssistantWebViewHost.cs");
assert(
  host.includes("AssistantWebViewActivationState") &&
    host.includes("WaitForReactBootstrapAsync") &&
    host.includes("RecordBootstrapSuccess"),
  "React activation must depend on the activation state model and deterministic bootstrap readiness.",
);
assert(
  host.indexOf("var bootstrapFailure = await WaitForReactBootstrapAsync") < host.indexOf("_activationState.RecordBootstrapSuccess"),
  "React must not be marked active until the bootstrap readiness probe has completed.",
);
assert(
  host.includes("assistant-index.css") && host.includes("assistant-web.js") && host.includes("NavigateFallback"),
  "Missing dist assets and bootstrap/navigation failures must take the typed fallback path.",
);
const state = read("Agent/AssistantWebViewActivationState.cs");
for (const reason of ["disabled by configuration", "navigation failed", "navigation timed out", "bootstrap failed"]) {
  assert(state.includes(reason), `Activation state must retain a typed ${reason} reason.`);
}
const pane = read("Forms/FrmPane.cs");
const autoExpand = pane.match(/cplMainChat\.Expand\(\);([\s\S]{0,120})/);
assert(autoExpand && !autoExpand[1].includes("EnsureAssistantPanelAsync"), "Auto-expand must rely on its expansion handler for one initialization path.");
const panel = read("AssistantPanel.cs");
assert(panel.includes("_initializationGate.GetOrStart(EnsureInitializedCoreAsync)"), "Assistant panel initialization must be single-flight.");
const routing = read("AssistantWeb/src/App.tsx");
assert(routing.includes("return <ExecutionBoardApp />") && routing.includes("return <ViraLabApp"), "Recovered execution-board and VIRA Lab surfaces must be mounted by App.");
assert(
  /import\s*\{[^}]*useLayoutEffect[^}]*\}\s*from\s*["']react["']/.test(routing),
  "The browser bridge must install before the first paint so first-render actions cannot observe an uninitialized ref.",
);
assert(
  /useLayoutEffect\(\s*\(\)\s*=>\s*\{[\s\S]*?createBlueBrickWindowBridge\(handlers[\s\S]*?bridgeRef\.current\s*=\s*bridge/s.test(routing),
  "The bridge must be installed by the layout-effect mount contract.",
);
const actionsStart = routing.indexOf("// Actions");
const derivedStateStart = routing.indexOf("// Derived state");
const actionsSource = routing.slice(actionsStart, derivedStateStart);
assert(
  /bridgeRef\.current\?\.post/.test(actionsSource),
  "Rendered actions must read the current bridge ref at event time instead of a render-captured nullable bridge.",
);
assert(
  !/const\s+bridge\s*=\s*bridgeRef\.current/.test(actionsSource),
  "Rendered actions must not capture bridgeRef.current during render.",
);

console.log(JSON.stringify({
  ok: true,
  checked: [
    "production_react_activation",
    "lab_react_activation",
    "packaged_production_and_lab_config",
    "exact_dist_triplet",
    "dist_has_no_secret_identifiers",
    "truthful_react_activation_state",
    "typed_fallback_reasons",
    "single_flight_auto_expansion",
    "recovered_surface_mounts",
  ],
  runtimeCeiling: "STATIC_SOURCE_CONTRACT_ONLY",
}, null, 2));
