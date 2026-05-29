# ADR: AionUI Model Authority And Provider Boundary

Date: 2026-05-28

Status: Accepted for P0 planning

## Context

BlueBrick needs to keep NVIDIA-compatible model profiles available while also supporting OpenAI API keys/OAuth from subscription quota and AionUI-managed model/provider configuration. The assistant currently has local model profile work, but the durable ownership boundary must be explicit before more provider, tool-calling, screenshot, and CAD workflows are added.

The important product constraint is that BlueBrick is a SolidWorks-side assistant and should not become the long-term source of truth for every model/provider secret. AionUI can act as the model registry or broker when available. BlueBrick should store only the minimum local selection and runtime state needed to render the assistant panel and route a request.

## Decision

AionUI is the preferred model/provider authority when an AionUI broker or registry is available. BlueBrick remains a local consumer of model profiles.

BlueBrick may persist:

- selected profile ID.
- local non-secret profile metadata needed for display.
- provider availability state.
- last verified timestamp.
- capability flags such as vision, streaming, tools, and JSON mode.

BlueBrick must not persist:

- provider API keys.
- OAuth access tokens or refresh tokens.
- raw AionUI secret material.
- secrets in config examples, registry plaintext, chat history, screenshots, telemetry, route traces, or execution receipts.

## Provider Profile Contract

Profiles should converge on this shape:

```json
{
  "id": "aionui-default",
  "display_name": "AionUI Default",
  "provider_kind": "openai_compatible|aionui_broker|openai|gemini|anthropic|nvidia|local|opencode_gateway",
  "base_url_alias": "AIONUI_BROKER|OPENAI_COMPATIBLE|NVIDIA|LOCAL_OLLAMA",
  "model_id": "string",
  "supports_vision": false,
  "supports_streaming": false,
  "supports_tools": false,
  "supports_json_mode": false,
  "context_limit": null,
  "secret_ref": "runtime-only",
  "enabled": true,
  "is_default": false,
  "source": "bluebrick|aionui_snapshot|aionui_broker|config_example",
  "last_verified_at": "ISO-8601|null"
}
```

## Runtime Resolution Rules

- NVIDIA-compatible profiles remain supported and can be the default when configured.
- OpenAI profiles resolve credentials at runtime through approved local environment, secure OS credential storage, or a future OAuth/broker path.
- AionUI broker profiles should delegate provider auth and routing to AionUI rather than copying provider keys into BlueBrick.
- Missing secrets or an unavailable broker must render a clear unavailable state, not crash the assistant panel.
- Unknown or invalid profile IDs fall back to a safe default/mock profile.
- Screenshot analysis requires `supports_vision = true`; otherwise the UI must deny analysis or offer a model switch.
- Tool calling requires `supports_tools = true` and route/tool policy approval; model capability alone is not sufficient.

## OAuth And Subscription Quota Boundary

OpenAI API keys or OAuth-backed subscription quota must be hosted behind an approved provider boundary:

- preferred: AionUI broker or other local broker that owns token refresh and quota routing.
- acceptable for development: local runtime environment or OS credential store with no checked-in values.
- not acceptable: keys in repository files, example config, WebView DOM, local transcripts, screenshots, telemetry logs, or git history.

BlueBrick should receive only a runtime authorization result or short-lived request capability, not long-lived provider credentials.

## Consequences

Positive:

- Keeps model/provider ownership centralized.
- Preserves NVIDIA-compatible local/provider options.
- Supports OpenAI quota/OAuth without turning BlueBrick config into a secret store.
- Lets the UI render disabled/unavailable states deterministically.

Tradeoffs:

- Requires a broker/profile availability check before full model switching is reliable.
- Requires explicit capability gating for vision, tools, streaming, and JSON mode.
- Requires tests proving secrets do not leak into DOM, logs, transcripts, screenshots, route traces, or receipts.

## Required Follow-Up

1. Add AionUI broker synchronization for profile snapshots.
2. Add UI states for unavailable broker, missing secret, disabled profile, and unsupported vision/tool operation.
3. Add tests for fallback profile behavior and missing-secret behavior.
4. Add redaction tests for logs, telemetry, chat history, screenshots, and receipts.
5. Add AionUI broker integration only after the route/tool policy is enforced.

## Current Implementation Note

`AssistantModelProfile` now includes provider kind, base URL alias, capability flags, context limit, secret reference, enabled/source metadata, and last verified timestamp. Local config profiles are normalized in `OpenAiAssistantService`, and screenshot analysis is gated by `SupportsVision`. This is local profile-contract work only; AionUI broker synchronization is not implemented yet.
