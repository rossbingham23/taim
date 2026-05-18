# Taim.Providers — LLM Provider Factory

Resolves per-tenant LLM provider configuration and creates `IChatClient` instances.

## Key Files

| File | Purpose |
|---|---|
| `ProviderFactory.cs` | Implements `IProviderFactory` — reads `tenant_provider_configs` and creates `IChatClient` |
| `Anthropic/` | Anthropic client builder (via OpenAI-compat endpoint) |
| `OpenAI/` | OpenAI client builder |
| `Gemini/` | Google Gemini client builder |
| `Ollama/` | Ollama local client builder |

## How It Works

`IProviderFactory.CreateAsync(tenantId, provider?, model?)`:
1. Reads the `tenant_provider_configs` row for the requested provider (or default)
2. Constructs the appropriate `IChatClient` for that provider/model
3. Returns it unwrapped — callers (typically `AgentFactory`) then wrap it with `TokenLedgerMiddleware`

## Anthropic Constraint

Anthropic's OpenAI-compat layer **does not support** `ChatResponseFormat.Json` (the `json_object` response_format type). Always use plain text responses and parse via `AgentJson.Deserialize<T>()`. The system prompt must include the expected JSON structure explicitly.

## Adding a New Provider

1. Create `NewProvider/NewProviderClientBuilder.cs`
2. Add the case in `ProviderFactory.cs`
3. Add seed config in `infra/postgres/init.sql` for local dev

## Scoping

`IProviderFactory` is Scoped — it reads per-tenant config from DB so it must not be a singleton.
