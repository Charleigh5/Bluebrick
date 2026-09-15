// Regenerate strict draft-07 contracts without a package install.
const fs = require('node:fs');
const path = require('node:path');
const obj = properties => ({ type: 'object', additionalProperties: false, required: Object.keys(properties), properties });
const str = { type: 'string', minLength: 1, maxLength: 256, pattern: '^(?!.*(?:[\\r\\n]|[Bb]earer |sk-|AIza|-----BEGIN)).+$' };
const enumeration = values => ({ type: 'string', enum: values });
const list = items => ({ type: 'array', minItems: 1, uniqueItems: true, items });
const modelIds = ['nvidia-kimi-k3', 'google-gemini-3-8-flash'];
const providerIds = ['nvidia', 'google-gemini', 'openrouter'];
const modelProviders = [['nvidia-kimi-k3','nvidia'], ['google-gemini-3-8-flash','google-gemini']];
const caps = obj(Object.fromEntries(['text','vision','tools','streaming','structuredOutput'].map(x => [x,{type:'boolean'}])));
const provider = { oneOf: [
  obj({id:{const:'nvidia'},protocol:{const:'openai-chat-completions'},baseUrl:{const:'https://integrate.api.nvidia.com/v1'},credentialBinding:{const:'NVIDIA_API_KEY'}}),
  obj({id:{const:'google-gemini'},protocol:{const:'openai-chat-completions'},baseUrl:{const:'https://generativelanguage.googleapis.com/v1beta/openai'},credentialBinding:{const:'GEMINI_API_KEY'}}),
  obj({id:{const:'openrouter'},protocol:{const:'openai-chat-completions'},baseUrl:{const:'https://openrouter.ai/api/v1'},credentialBinding:{const:'OPENROUTER_API_KEY'}})
] };
const model = {oneOf:modelProviders.map(([modelId,providerId]) => obj({id:{const:modelId},providerId:{const:providerId},providerModel:{type:'string',pattern:'^[a-z0-9][a-z0-9./-]{0,99}$'},displayName:{...str,maxLength:80},capabilities:caps,contextLimit:{type:'integer',minimum:1,maximum:2147483647},roles:list(enumeration(['general','vision','engineering','agent','fast','fallback']))}))};
const routing = obj(Object.fromEntries(['default','vision','tools'].map(x => [x,list(enumeration(modelIds))])));
const failure = enumeration(['credential_missing','authentication_failed','rate_limited','provider_unavailable','model_unavailable','timeout','malformed_response','unsupported_payload','tool_call_parse_failure','context_limit','stream_interrupted','bluebrick_tool_execution_failure']);
// Contracts only. Image references remain application-owned and approval-gated.
const request = obj({kind:{const:'request'},modelId:enumeration(modelIds),messages:{type:'array',items:obj({role:enumeration(['system','user','assistant','tool']),content:{type:'string',maxLength:1000000},imageReferences:{type:'array',items:str},toolCallId:{anyOf:[{type:'null'},str]},toolCalls:{type:'array',items:obj({id:str,name:str,arguments:{type:'object'}})}})},tools:{type:'array',items:obj({name:str,parameters:{type:'object'}})},streaming:{type:'boolean'},structuredOutput:{type:'boolean'},reasoning:{type:'object',maxProperties:8}});
const response = {oneOf:modelProviders.map(([modelId,providerId]) => obj({kind:{const:'response'},modelId:{const:modelId},providerId:{const:providerId},providerModel:str,content:{type:'string',maxLength:1000000},toolCalls:{type:'array',items:obj({id:str,name:str,arguments:{type:'object'}})},usage:obj({inputTokens:{type:'integer',minimum:0},outputTokens:{type:'integer',minimum:0}}),finishStatus:enumeration(['complete','tool_calls','length','error','interrupted']),error:{anyOf:[{type:'null'},failure]},diagnostics:{type:'object',additionalProperties:false,properties:{boundary:enumeration(['request_construction','transport','inference','response_parsing','tool_call_parsing','permission_validation','tool_execution','tool_continuation']),httpStatus:{type:'integer',minimum:100,maximum:599}}}}))};
for (const [name,schema] of Object.entries({provider,model,routing,inference:{oneOf:[request,response]}})) fs.writeFileSync(path.join(__dirname,name+'.schema.json'),JSON.stringify({$schema:'http://json-schema.org/draft-07/schema#',...schema},null,2)+'\n');
