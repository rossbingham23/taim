# Taim.Memory — Agent Memory and Context

Provides context enrichment (KPI state, team structure, chat history) and semantic memory (embeddings + vector search).

## Key Files

| File | Purpose |
|---|---|
| `KPI/KpiContextProvider.cs` | Formats an agent's KPIs into a text block for injection into prompts |
| `Team/TeamContextProvider.cs` | Formats direct reports, peers, and manager into a team-context block |
| `Episodic/ChatHistoryProvider.cs` | Stores + retrieves per-agent conversation history (table `agent_chat_history`) |
| `Semantic/VectorMemoryProvider.cs` | Embedding + cosine-similarity retrieval from `memory_entries` table |
| `Semantic/NoOpEmbeddingGenerator.cs` | Stub embedding generator (zero vector) — replace with a real model for production |
| `MemoryExtensions.cs` | `AddTaimMemory()` — registers all services |

## Episodic Memory

`ChatHistoryProvider` implements `IChatHistoryProvider` (defined in `Taim.Core.Memory`) — used to persist multi-turn conversations per agent so context survives container restarts. Sessions are keyed by `(agentId, sessionId)`.

Both `ChatHistoryProvider` (concrete) and `IChatHistoryProvider` are registered as Scoped in `MemoryExtensions`. Use `IChatHistoryProvider` in consuming services (e.g., `ActionWorker`).

**Action work loop session keys:** `action:{actionId}` — stores the full conversation for each executing action.

## Semantic Memory

`VectorMemoryProvider` implements `IMemoryService`:
- `StoreAsync(tenantId, agentId, content, metadata)` — embeds text and saves to `memory_entries`
- `SearchAsync(tenantId, query, limit)` — embeds query, does `<=>` (cosine) similarity search via pgvector

Currently uses `NoOpEmbeddingGenerator` (zero vectors — similarity search is non-functional). To enable real semantic memory, replace with an `IEmbeddingGenerator<string, Embedding<float>>` backed by an actual model (OpenAI `text-embedding-3-small`, etc.).

## Context Providers

Called by `AgentOrchestrator.BuildContextAsync` to assemble `ExecutiveContext`:
- `KpiContextProvider.GetKpiSummaryAsync(tenantId, agentId)` → formatted KPI lines
- `TeamContextProvider.GetTeamSummaryAsync(tenantId, agentId)` → formatted org lines
