// Run after compiling generated/typescript/sharedAI.ts into tests/.compiled.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { validateCatalog, routeCandidates, reconcileAion } = require('./.compiled/sharedAI.js');
const root = path.resolve(__dirname, '..');
const read = name => JSON.parse(fs.readFileSync(path.join(root, 'catalog', name + '.json'), 'utf8').replace(/^\uFEFF/, ''));
const catalog = { providers: read('providers'), models: read('models'), routes: read('routes') };
let passed = 0;
function test(name, run) { run(); passed++; console.log('PASS ' + name); }
function changed(edit) { const value = structuredClone(catalog); edit(value); return value; }
const kimi = 'nvidia-kimi-k3', gemini = 'google-gemini-3-8-flash';
const both = { [kimi]: { available: true }, [gemini]: { available: true } };
test('38 shared cross-language route fixtures', () => {
  const fixtures = JSON.parse(fs.readFileSync(path.join(__dirname,'fixtures.json'),'utf8'));
  for (const f of fixtures) {
    const c = structuredClone(catalog);
    if(f.missingKimiCapability) c.models[0].capabilities[f.missingKimiCapability] = false;
    assert.deepEqual(routeCandidates(c,'default',f.requirements,{[kimi]:{available:!f.unavailableKimi},[gemini]:{available:true}}),f.expected);
  }
  assert.equal(fixtures.length,38);
});
test('canonical catalog validates with OpenRouter and exactly two stable models', () => {
  validateCatalog(catalog);
  assert.deepEqual(catalog.providers, [
    { id: 'nvidia', protocol: 'openai-chat-completions', baseUrl: 'https://integrate.api.nvidia.com/v1', credentialBinding: 'NVIDIA_API_KEY' },
    { id: 'google-gemini', protocol: 'openai-chat-completions', baseUrl: 'https://generativelanguage.googleapis.com/v1beta/openai', credentialBinding: 'GEMINI_API_KEY' },
    { id: 'openrouter', protocol: 'openai-chat-completions', baseUrl: 'https://openrouter.ai/api/v1', credentialBinding: 'OPENROUTER_API_KEY' },
  ]);
  assert.deepEqual(catalog.models.map(m => m.id).sort(), [gemini, kimi]);
});
for (const route of ['default', 'vision', 'tools']) test(route + ' deterministically prefers Kimi', () => assert.deepEqual(routeCandidates(catalog, route, {}, both), [kimi, gemini]));
test('missing credential permits available Gemini', () => assert.deepEqual(routeCandidates(catalog, 'default', {}, { ...both, [kimi]: { available: false, reason: 'credential_missing' } }), [gemini]));
test('absent runtime states never mean available', () => assert.deepEqual(routeCandidates(catalog, 'default', {}, {}), []));
test('required capabilities filter candidates', () => assert.deepEqual(routeCandidates(changed(c => c.models[0].capabilities.structuredOutput = false), 'default', { structuredOutput: true }, both), [gemini]));
test('vision route inherently requires vision', () => assert.deepEqual(routeCandidates(changed(c => c.models[0].capabilities.vision = false), 'vision', {}, both), [gemini]));
for (const [name, edit] of [
  ['duplicate models', c => c.models.push(c.models[0])],
  ['invalid provider reference', c => c.models[0].providerId = 'missing'],
  ['invalid route reference', c => c.routes.default.push('missing')],
  ['duplicate route', c => c.routes.default.push(c.routes.default[0])],
  ['unknown secret field', c => c.providers[0].apiKey = 'REDACTED_TEST_ONLY'],
  ['endpoint credentials', c => c.providers[0].baseUrl = 'https://user:pass@example.test/v1'],
  ['endpoint query', c => c.providers[0].baseUrl += '?key=TEST_ONLY'],
  ['wrong capability type', c => c.models[0].capabilities.vision = 'true'],
  ['fractional context', c => c.models[0].contextLimit = 1.5],
  ['secret-like metadata', c => c.models[0].displayName = 'Bearer TEST_ONLY'],
  ['unknown role', c => c.models[0].roles = ['unknown']],
  ['context overflow', c => c.models[0].contextLimit = 2147483648],
  ['provider identity mismatch', c => c.models[0].providerId = 'google-gemini'],
  ['unapproved endpoint', c => c.providers[0].baseUrl = 'https://example.test/v1'],
  ['wrong credential binding', c => c.providers[0].credentialBinding = 'OTHER_API_KEY'],
  ['unapproved OpenRouter endpoint', c => c.providers[2].baseUrl = 'https://openrouter.ai/v1'],
  ['wrong OpenRouter credential binding', c => c.providers[2].credentialBinding = 'OPENROUTER_TOKEN'],
  ['duplicate OpenRouter provider', c => c.providers.push(c.providers[2])],
  ['secret-bearing OpenRouter metadata', c => c.providers[2].apiKey = 'synthetic-sensitive-marker'],
  ['oversized display name', c => c.models[0].displayName = 'x'.repeat(81)],
]) test('rejects ' + name, () => assert.throws(() => validateCatalog(changed(edit)), /Invalid SharedAI metadata/));
test('provider API model revision preserves stable identity', () => {
  const revised = changed(c => c.models[0].providerModel = 'moonshotai/kimi-k3-revised');
  validateCatalog(revised); assert.deepEqual(routeCandidates(revised, 'default', {}, both), [kimi, gemini]);
});
const bindings = catalog.providers.map(p => ({ providerId: p.id, aionProviderId: 'fixture-' + p.id }));
const projections = catalog.providers.filter(p => catalog.models.some(m => m.providerId === p.id)).map(p => {
  const model = catalog.models.find(m => m.providerId === p.id);
  return { id: 'fixture-' + p.id, platform: 'custom', baseUrl: p.baseUrl, model: [model.providerModel], contextLimit: model.contextLimit,
    capabilities: [{ type: 'text', isUserSelected: true }, { type: 'vision', isUserSelected: true }, { type: 'function_calling', isUserSelected: true }] };
});
test('AionUI maps explicit provider IDs and provider model strings without mutation', () => {
  const before = JSON.stringify(projections);
  const results = reconcileAion(catalog, projections, bindings);
  assert.equal(results.length, 2); assert.equal(JSON.stringify(projections), before);
  results.forEach(r => { assert.deepEqual(r.issues, ['capability_unknown:streaming', 'capability_unknown:structuredOutput']); assert.equal(r.enabled, true); assert.equal('available' in r, false); });
});
test('AionUI reports missing mappings', () => assert.ok(reconcileAion(catalog, [], []).every(r => r.issues.includes('missing_provider_mapping'))));
test('AionUI reports capability and endpoint drift', () => {
  const input = structuredClone(projections); input[0].capabilities[1].isUserSelected = false; input[0].baseUrl = 'https://example.test/v1';
  const result = reconcileAion(catalog, input, bindings)[0]; assert.ok(result.issues.includes('capability_drift:vision')); assert.ok(result.issues.includes('endpoint_drift'));
});
test('AionUI reports missing model and ambiguous provider', () => {
  const input = structuredClone(projections); input[0].model = [];
  assert.ok(reconcileAion(catalog, input, bindings)[0].issues.includes('missing_model_mapping'));
  assert.ok(reconcileAion(catalog, projections, [...bindings, bindings[0]])[0].issues.includes('ambiguous_provider_mapping'));
});
test('AionUI rejects full secret-bearing record without echoing input', () => assert.throws(() => reconcileAion(catalog, [{ ...projections[0], apiKey: 'SYNTHETIC_SECRET_MUST_NOT_ECHO' }], bindings), error => error.message === 'Invalid SharedAI metadata'));
test('AionUI enable state is preserved separately from availability', () => {
  const input = structuredClone(projections); input[0].enabled = false;
  assert.equal(reconcileAion(catalog, input, bindings)[0].enabled, false);
});
console.log(JSON.stringify({ status: 'PASS', passed, scope: 'isolated contracts/router and synthetic AionUI projections; no live DB or inference' }));
