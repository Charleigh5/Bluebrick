import assert from "node:assert/strict";
import config from "../vite.config.mjs";

const plugin = config.plugins.find((candidate) => candidate?.name === "bluebrick-runtime-build-identity");
assert(plugin, "Vite config must install the runtime build identity plugin.");
const { runtimeBuildIdentityPlugin } = await import("../vite.config.mjs");
const transformed = runtimeBuildIdentityPlugin("BB20-TEST-&-IDENTITY").transformIndexHtml("<html><head></head><body></body></html>");
assert.match(transformed, /<meta name="bluebrick-build-id" content="BB20-TEST-&amp;-IDENTITY">/);
assert.throws(
  () => runtimeBuildIdentityPlugin("UNKNOWN").transformIndexHtml("<html><head></head></html>"),
  /VITE_BLUEBRICK_BUILD_ID/
);
const identityPlugin = runtimeBuildIdentityPlugin("fixture-build");
const meta = '<meta name="bluebrick-build-id" content="existing">';
for (const inert of [`<!-- ${meta} -->`, `<script>const markup = '${meta}';</script>`, `<div data='${meta}'></div>`, '<meta data-name="bluebrick-build-id" content="existing">']) {
  const output = identityPlugin.transformIndexHtml(`<html><head>${inert}</head></html>`);
  assert(output.includes('<meta name="bluebrick-build-id" content="fixture-build">'));
  assert(output.includes(inert), "Inert markup must be preserved.");
}
assert.throws(() => identityPlugin.transformIndexHtml(`<html><head>${meta}</head></html>`), /Duplicate/);
assert.throws(() => identityPlugin.transformIndexHtml('<!-- <head></head> -->'), /Missing HTML head/);
assert.throws(() => identityPlugin.transformIndexHtml('<script>const head = "<head>";</script>'), /Missing HTML head/);
console.log("runtime build identity transform: PASS");
