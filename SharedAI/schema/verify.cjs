const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const Ajv = require(process.argv[2]);
const ajv = new Ajv({strict:false, allErrors:true});
const read = f => JSON.parse(fs.readFileSync(path.join(__dirname,f),'utf8'));
let checks = 0;
for (const [schemaName,catalogName] of [['provider','providers'],['model','models'],['routing','routes']]) {
  const validate = ajv.compile(read(schemaName+'.schema.json'));
  const value = read('../catalog/'+catalogName+'.json');
  for (const record of Array.isArray(value) ? value : [value]) { assert.ok(validate(record),JSON.stringify(validate.errors)); checks++; }
  const altered = structuredClone(Array.isArray(value) ? value[0] : value);
  altered.apiKey='synthetic-sensitive-marker'; assert.equal(validate(altered),false); checks++;
}
const inference = ajv.compile(read('inference.schema.json'));
assert.equal(inference({kind:'request',modelId:'nvidia-kimi-k3',messages:[],tools:[],streaming:true,structuredOutput:false,reasoning:{}}),true); checks++;
assert.equal(inference({kind:'request',modelId:'missing'}),false); checks++;
const model = ajv.compile(read('model.schema.json'));
const canonicalModels = read('../catalog/models.json');
for (const canonical of canonicalModels) {
  const wrongProvider = structuredClone(canonical);
  wrongProvider.providerId = canonical.id === 'nvidia-kimi-k3' ? 'google-gemini' : 'nvidia';
  assert.equal(model(wrongProvider), false); checks++;
  const openRouterProvider = structuredClone(canonical);
  openRouterProvider.providerId = 'openrouter';
  assert.equal(model(openRouterProvider), false); checks++;
}
assert.equal(inference({kind:'response',modelId:'nvidia-kimi-k3',providerId:'openrouter',providerModel:'moonshotai/kimi-k3',content:'',toolCalls:[],usage:{inputTokens:0,outputTokens:0},finishStatus:'complete',error:null,diagnostics:{}}),false); checks++;
console.log(JSON.stringify({status:'PASS',checks,schemaDraft:'draft-07',inference:'contract-only; not wired to transport'}));
