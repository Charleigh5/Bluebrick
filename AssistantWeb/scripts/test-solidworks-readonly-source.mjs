import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const root = new URL("../../", import.meta.url);
const reader = await readFile(new URL("Agent/SolidWorksActiveDocumentContextReader.cs", root), "utf8");
const dispatcher = await readFile(new URL("Agent/SolidWorksMainThreadDispatcher.cs", root), "utf8");
const assembly = await readFile(new URL("Agent/SolidWorksAssemblyReader.cs", root), "utf8");
const server = await readFile(new URL("Agent/AgentHttpServer.cs", root), "utf8");
const addin = await readFile(new URL("swaddin.cs", root), "utf8");
const project = await readFile(new URL("BlueBrick.csproj", root), "utf8");
const tools = await readFile(new URL("Agent/AssistantToolService.cs", root), "utf8");

assert.match(reader, /ISolidWorksMainThreadDispatcher/);
assert.match(reader, /_dispatcher\.Invoke/);
assert.doesNotMatch(reader, /Task\.Run\s*\([^)]*(?:IActiveDoc2|ActiveDoc)/s);
assert.match(dispatcher, /Control/);
assert.match(dispatcher, /InvokeRequired/);
assert.match(dispatcher, /control\.Invoke/);
assert.match(server, /AgentHttpServer\(ISldWorks swApp, AgentConfig config, AgentOverlay overlay, Control mainThreadControl\)/);
assert.match(server, /new SolidWorksMainThreadDispatcher\(mainThreadControl\)/);
assert.match(addin, /new AgentHttpServer\(SwApp, _agentConfig, _agentOverlay, TaskPanWinFormControl\)/);

assert.match(assembly, /GetRootComponent3\(false\)/);
assert.match(assembly, /GetChildren\(\)/);
assert.match(assembly, /GetSuppression2\(\)/);
assert.match(assembly, /GetUnloadedComponentNames/);
assert.match(assembly, /Get6\([^;]*true/s, "component properties must be cached-only");
assert.match(assembly, /MaxDepth\s*=\s*32/);
assert.match(assembly, /MaxRecords\s*=\s*5000/);
for (const forbidden of ["SetSuppression", "SetReferencedConfiguration", "ResolveAllLightWeightComponents", "LightweightAllResolved", "EditRebuild3", "ForceRebuild3", "Save3", "Select4"]) {
  assert.doesNotMatch(assembly, new RegExp(forbidden), `forbidden mutating/resolving API: ${forbidden}`);
}
assert.doesNotMatch(assembly, /GetModelDoc2\s*\(/, "component traversal must not load model documents");
assert.doesNotMatch(assembly, /GetComponents\s*\(/, "flat traversal must not replace hierarchy traversal");
assert.match(project, /Agent\\SolidWorksMainThreadDispatcher\.cs/);
assert.match(project, /Agent\\SolidWorksAssemblyReader\.cs/);
assert.match(tools, /MaxComponentEvidenceBytes\s*=\s*524288/);
assert.match(tools, /component_evidence_json/);
assert.match(tools, /assembly_traversal_json/);
assert.match(tools, /assembly_payload_status/);
assert.match(tools, /Encoding\.UTF8\.GetByteCount/);

console.log(JSON.stringify({ ok: true, checked: [
  "main-thread COM dispatch",
  "non-resolving hierarchy traversal",
  "cached-only component properties",
  "bounded traversal",
  "forbidden mutation APIs absent"
] }, null, 2));
