/** R06 mirrored contract. Catalog is static declaration, never runtime inference proof. */
export type Capabilities = { text: boolean; vision: boolean; tools: boolean; streaming: boolean; structuredOutput: boolean };
export type Provider = { id: string; protocol: 'openai-chat-completions'; baseUrl: string; credentialBinding: string };
export type Model = { id: string; providerId: string; providerModel: string; displayName: string; capabilities: Capabilities; contextLimit: number; roles: string[] };
export type Catalog = { providers: Provider[]; models: Model[]; routes: Record<'default' | 'vision' | 'tools', string[]> };
export type RuntimeState = { available: boolean; reason?: string };
const capabilityKeys = ['text', 'vision', 'tools', 'streaming', 'structuredOutput'] as const;
function fail(): never { throw new Error('Invalid SharedAI metadata'); }
function object(value: unknown, keys: readonly string[]): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return fail();
  const record = value as Record<string, unknown>;
  if (Object.keys(record).length !== keys.length || keys.some(key => !Object.prototype.hasOwnProperty.call(record, key))) return fail();
  return record;
}
function metadata(value: unknown): asserts value is string {
  if (typeof value !== 'string' || !value.trim() || value.length > 256 || /[\r\n]|bearer\s|sk-[a-z0-9]|AIza|-----BEGIN/i.test(value)) fail();
}
function list(value: unknown): asserts value is unknown[] { if (!Array.isArray(value) || value.length === 0) fail(); }
function id(value: unknown): asserts value is string { metadata(value); if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value)) fail(); }
export function validateCatalog(input: unknown): Catalog {
  const root = object(input, ['providers', 'models', 'routes']);
  list(root.providers); list(root.models);
  if (root.providers.length !== 3 || root.models.length !== 2) fail();
  const providerIds = new Set<string>();
  for (const value of root.providers) {
    const p = object(value, ['id', 'protocol', 'baseUrl', 'credentialBinding']);
    id(p.id); if (providerIds.has(p.id)) fail(); providerIds.add(p.id);
    if (!['nvidia', 'google-gemini', 'openrouter'].includes(p.id)) fail();
    if (p.protocol !== 'openai-chat-completions') fail();
    metadata(p.baseUrl); metadata(p.credentialBinding);
    let endpoint: URL;
    try { endpoint = new URL(p.baseUrl); } catch { return fail(); }
    if (endpoint.protocol !== 'https:' || endpoint.username || endpoint.password || endpoint.search || endpoint.hash) fail();
    if (!/^[A-Z][A-Z0-9_]*_API_KEY$/.test(p.credentialBinding)) fail();
    const approved = {
      nvidia: ['https://integrate.api.nvidia.com/v1', 'NVIDIA_API_KEY'],
      'google-gemini': ['https://generativelanguage.googleapis.com/v1beta/openai', 'GEMINI_API_KEY'],
      openrouter: ['https://openrouter.ai/api/v1', 'OPENROUTER_API_KEY'],
    } as const;
    const expected = approved[p.id as keyof typeof approved];
    if (!expected || p.baseUrl !== expected[0] || p.credentialBinding !== expected[1]) fail();
  }
  const modelIds = new Set<string>();
  for (const value of root.models) {
    const m = object(value, ['id', 'providerId', 'providerModel', 'displayName', 'capabilities', 'contextLimit', 'roles']);
    id(m.id); id(m.providerId); if (modelIds.has(m.id) || !providerIds.has(m.providerId)) fail(); modelIds.add(m.id);
    metadata(m.providerModel); metadata(m.displayName);
    if (!['nvidia-kimi-k3', 'google-gemini-3-8-flash'].includes(m.id) || m.providerId !== (m.id === 'nvidia-kimi-k3' ? 'nvidia' : 'google-gemini')) fail();
    if (!/^[a-z0-9][a-z0-9./-]{0,99}$/.test(m.providerModel) || m.displayName.length > 80) fail();
    const caps = object(m.capabilities, capabilityKeys);
    if (capabilityKeys.some(key => typeof caps[key] !== 'boolean')) fail();
    if (typeof m.contextLimit !== 'number' || !Number.isSafeInteger(m.contextLimit) || m.contextLimit <= 0 || m.contextLimit > 2147483647) fail();
    list(m.roles); m.roles.forEach(id); if (new Set(m.roles).size !== m.roles.length) fail();
    if (m.roles.some(role => !['general', 'vision', 'engineering', 'agent', 'fast', 'fallback'].includes(role as string))) fail();
  }
  const routes = object(root.routes, ['default', 'vision', 'tools']);
  for (const route of Object.values(routes)) {
    list(route); if (new Set(route).size !== route.length) fail();
    for (const modelId of route) if (typeof modelId !== 'string' || !modelIds.has(modelId)) fail();
  }
  return input as Catalog;
}
export function routeCandidates(catalog: Catalog, route: keyof Catalog['routes'], requirements: Partial<Capabilities>, runtime: Record<string, RuntimeState>): string[] {
  validateCatalog(catalog);
  if (!Object.prototype.hasOwnProperty.call(catalog.routes, route) || Object.keys(requirements).some(key => !capabilityKeys.includes(key as keyof Capabilities) || typeof requirements[key as keyof Capabilities] !== 'boolean')) fail();
  return catalog.routes[route].filter(modelId => {
    const model = catalog.models.find(item => item.id === modelId)!;
    const available = Object.prototype.hasOwnProperty.call(runtime, modelId) && runtime[modelId]?.available === true;
    return available && capabilityKeys.every(key => !(requirements[key] || (route === 'vision' && key === 'vision') || (route === 'tools' && key === 'tools')) || model.capabilities[key]);
  });
}

/** Explicit allowlist projection of AionUI IProvider; never pass full credential-bearing records. */
export type AionProviderProjection = {
  id: string; platform: string; baseUrl: string; model: string[];
  capabilities?: { type: string; isUserSelected?: boolean }[];
  contextLimit?: number; enabled?: boolean; modelEnabled?: Record<string, boolean>;
};
export type AionBinding = { providerId: string; aionProviderId: string };
export type Reconciliation = { modelId: string; providerId: string; aionProviderId?: string; issues: string[]; declaredCapabilities: Capabilities; enabled?: boolean };
/** Pure report: no database, credentials, networking, mutation, or availability inference. */
export function reconcileAion(catalog: Catalog, projections: readonly AionProviderProjection[], bindings: readonly AionBinding[]): Reconciliation[] {
  validateCatalog(catalog);
  const allowed = ['id', 'platform', 'baseUrl', 'model', 'capabilities', 'contextLimit', 'enabled', 'modelEnabled'];
  for (const p of projections) {
    if (!p || Object.keys(p).some(key => !allowed.includes(key))) fail();
    metadata(p.id); metadata(p.platform); metadata(p.baseUrl);
    if (!Array.isArray(p.model)) fail(); p.model.forEach(metadata);
    if (p.capabilities && (!Array.isArray(p.capabilities) || p.capabilities.some(cap => !cap || Object.keys(cap).some(key => !['type', 'isUserSelected'].includes(key)) || typeof cap.type !== 'string' || (cap.isUserSelected !== undefined && typeof cap.isUserSelected !== 'boolean')))) fail();
    if (p.capabilities) { p.capabilities.forEach(cap => metadata(cap.type)); if (new Set(p.capabilities.map(cap => cap.type)).size !== p.capabilities.length) fail(); }
    if (p.enabled !== undefined && typeof p.enabled !== 'boolean') fail();
    if (p.contextLimit !== undefined && (!Number.isSafeInteger(p.contextLimit) || p.contextLimit <= 0)) fail();
    if (p.modelEnabled !== undefined && (!p.modelEnabled || typeof p.modelEnabled !== 'object' || Array.isArray(p.modelEnabled) || Object.values(p.modelEnabled).some(value => typeof value !== 'boolean'))) fail();
    let endpoint: URL;
    try { endpoint = new URL(p.baseUrl); } catch { return fail(); }
    if (endpoint.username || endpoint.password || endpoint.search || endpoint.hash) fail();
  }
  for (const b of bindings) { object(b, ['providerId', 'aionProviderId']); id(b.providerId); metadata(b.aionProviderId); }
  return catalog.models.map(model => {
    const provider = catalog.providers.find(item => item.id === model.providerId)!;
    const matches = bindings.filter(binding => binding.providerId === provider.id);
    const issues: string[] = [];
    const result: Reconciliation = { modelId: model.id, providerId: provider.id, issues, declaredCapabilities: { ...model.capabilities } };
    if (matches.length !== 1) { issues.push(matches.length ? 'ambiguous_provider_mapping' : 'missing_provider_mapping'); return result; }
    const entries = projections.filter(p => p.id === matches[0].aionProviderId);
    if (entries.length !== 1) { issues.push(entries.length ? 'ambiguous_provider_record' : 'missing_provider_record'); return result; }
    const entry = entries[0]; result.aionProviderId = entry.id;
    if (entry.baseUrl.replace(/\/+$/, '') !== provider.baseUrl.replace(/\/+$/, '')) issues.push('endpoint_drift');
    if (!entry.model.includes(model.providerModel)) issues.push('missing_model_mapping');
    if (entry.model.filter(name => name === model.providerModel).length > 1) issues.push('ambiguous_model_mapping');
    result.enabled = entry.enabled !== false && entry.modelEnabled?.[model.providerModel] !== false;
    for (const key of capabilityKeys) {
      const nativeKey = key === 'tools' ? 'function_calling' : key;
      const explicit = entry.capabilities?.find(cap => cap.type === nativeKey)?.isUserSelected;
      if (explicit === undefined) issues.push(`capability_unknown:${key}`);
      else if (explicit !== model.capabilities[key]) issues.push(`capability_drift:${key}`);
    }
    if (entry.contextLimit === undefined) issues.push('context_unknown');
    else if (entry.contextLimit !== model.contextLimit) issues.push('context_drift');
    return result;
  });
}
