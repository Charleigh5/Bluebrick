import { createHash } from "node:crypto";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(root);
const configuration = process.argv.find((argument) => argument.startsWith("--configuration="))?.slice("--configuration=".length);
const expectedDll = configuration === "Lab" ? "BlueBrick.Lab.dll" : configuration === "Release" ? "BlueBrick.dll" : null;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function hash(relativePath) {
  const path = join(repoRoot, relativePath);
  assert(existsSync(path), `Required output is missing: ${relativePath}`);
  return createHash("sha256").update(readFileSync(path)).digest("hex").toUpperCase();
}

assert(expectedDll, "Pass --configuration=Release or --configuration=Lab.");

const expectedMembers = [
  "config/appsettings.json",
  "config/appsettings.lab.json",
  "AssistantWeb/dist/index.html",
  "AssistantWeb/dist/assistant-index.css",
  "AssistantWeb/dist/assistant-web.js",
];

const expectedDistInventory = expectedMembers
  .filter((member) => member.startsWith("AssistantWeb/dist/"))
  .map((member) => member.slice("AssistantWeb/dist/".length))
  .sort();

function assertDistInventory(relativeDirectory, label) {
  const directory = join(repoRoot, relativeDirectory);
  assert(existsSync(directory), `Required ${label} dist directory is missing: ${relativeDirectory}`);
  const actualInventory = readdirSync(directory, { withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => entry.name)
    .sort();
  assert(
    JSON.stringify(actualInventory) === JSON.stringify(expectedDistInventory),
    `${label} dist inventory differs from the required triplet: expected ${JSON.stringify(expectedDistInventory)}, got ${JSON.stringify(actualInventory)}.`,
  );
  return actualInventory;
}

const sourceDistInventory = assertDistInventory("AssistantWeb/dist", "source");
const outputDistInventory = assertDistInventory(`bin/${configuration}/AssistantWeb/dist`, `${configuration} output`);

const members = expectedMembers.map((source) => {
  const output = `bin/${configuration}/${source}`;
  const sourceHash = hash(source);
  const outputHash = hash(output);
  assert(sourceHash === outputHash, `${configuration} output differs from source: ${source}`);
  return { source, output, sha256: sourceHash };
});

const productionOutput = JSON.parse(readFileSync(join(repoRoot, `bin/${configuration}/config/appsettings.json`), "utf8"));
const labOutput = JSON.parse(readFileSync(join(repoRoot, `bin/${configuration}/config/appsettings.lab.json`), "utf8"));
assert(productionOutput?.Assistant?.UseReactWebView === true, `${configuration} production config must explicitly activate React.`);
assert(labOutput?.Assistant?.UseReactWebView === true, `${configuration} lab config must explicitly activate React.`);

const dllPath = `bin/${configuration}/${expectedDll}`;
const dllSha256 = hash(dllPath);

console.log(JSON.stringify({
  ok: true,
  configuration,
  dll: { path: dllPath, sha256: dllSha256 },
  members,
  sourceDistInventory,
  outputDistInventory,
  verified: [
    "both_output_configs",
    "exact_dist_triplet",
    "byte_matching_source_outputs",
    "configuration_specific_dll",
  ],
}, null, 2));
