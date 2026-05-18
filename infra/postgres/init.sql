-- ─────────────────────────────────────────────────────────────────────────────
-- TAIM PostgreSQL Schema
-- ─────────────────────────────────────────────────────────────────────────────

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "vector";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

-- ── Tenants ──────────────────────────────────────────────────────────────────
CREATE TABLE tenants (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name        TEXT NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ── Users ────────────────────────────────────────────────────────────────────
CREATE TABLE users (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id   UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    email       TEXT NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_users_tenant ON users(tenant_id);

-- ── Tenant LLM Provider Config ───────────────────────────────────────────────
CREATE TABLE tenant_provider_configs (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id   UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    provider    TEXT NOT NULL,  -- 'openai' | 'anthropic' | 'gemini' | 'ollama'
    api_key     TEXT,           -- encrypted at application layer
    base_url    TEXT,           -- for Ollama
    default_model TEXT NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, provider)
);

-- ── Budget ───────────────────────────────────────────────────────────────────
CREATE TABLE budgets (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    task_id         UUID,           -- FK added after tasks table
    limit_usd       NUMERIC(12,6) NOT NULL,
    spent_usd       NUMERIC(12,6) NOT NULL DEFAULT 0,
    status          TEXT NOT NULL DEFAULT 'active',  -- 'active' | 'paused' | 'exhausted'
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_budgets_tenant ON budgets(tenant_id);

CREATE TABLE spend_entries (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    budget_id       UUID NOT NULL REFERENCES budgets(id),
    agent_id        UUID NOT NULL,
    provider        TEXT NOT NULL,
    model           TEXT NOT NULL,
    input_tokens    INTEGER NOT NULL DEFAULT 0,
    output_tokens   INTEGER NOT NULL DEFAULT 0,
    cost_usd        NUMERIC(12,6) NOT NULL,
    recorded_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_spend_tenant ON spend_entries(tenant_id);
CREATE INDEX idx_spend_budget ON spend_entries(budget_id);
CREATE INDEX idx_spend_agent ON spend_entries(agent_id);

-- ── Tasks (user-submitted goals) ─────────────────────────────────────────────
CREATE TABLE tasks (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    goal            TEXT NOT NULL,
    status          TEXT NOT NULL DEFAULT 'pending',
                    -- 'pending' | 'bootstrapping' | 'running' | 'paused' | 'completed' | 'failed'
    budget_id       UUID REFERENCES budgets(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_tasks_tenant ON tasks(tenant_id);

ALTER TABLE budgets ADD CONSTRAINT fk_budgets_task
    FOREIGN KEY (task_id) REFERENCES tasks(id);

-- ── Agents ───────────────────────────────────────────────────────────────────
CREATE TABLE agents (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    task_id         UUID REFERENCES tasks(id),
    parent_agent_id UUID REFERENCES agents(id),
    name            TEXT NOT NULL,
    role            TEXT NOT NULL,      -- 'bootstrap' | 'expert' | 'ceo' | 'cto' | 'developer' | ...
    charter         TEXT,               -- agent's instructions / charter
    status          TEXT NOT NULL DEFAULT 'idle',
                    -- 'idle' | 'active' | 'waiting_approval' | 'sleeping' | 'terminated'
    provider        TEXT,
    model           TEXT,
    durable_entity_key TEXT,            -- Durable Task entity key
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_agents_tenant ON agents(tenant_id);
CREATE INDEX idx_agents_task ON agents(task_id);
CREATE INDEX idx_agents_parent ON agents(parent_agent_id);

-- ── Actions (work items dispatched to agents) ────────────────────────────────
CREATE TABLE actions (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id           UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    task_id             UUID NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    agent_id            UUID REFERENCES agents(id) ON DELETE SET NULL,
    created_by_agent_id UUID REFERENCES agents(id) ON DELETE SET NULL,
    title               TEXT NOT NULL,
    description         TEXT,
    status              TEXT NOT NULL DEFAULT 'open',
                        -- 'open' | 'in_progress' | 'blocked' | 'done' | 'cancelled'
    priority            INTEGER NOT NULL DEFAULT 50,
    parent_action_id    UUID REFERENCES actions(id) ON DELETE SET NULL,
    due_at              TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_actions_tenant ON actions(tenant_id);
CREATE INDEX idx_actions_task ON actions(task_id);
CREATE INDEX idx_actions_agent ON actions(agent_id);

-- ── KPIs ─────────────────────────────────────────────────────────────────────
CREATE TABLE kpis (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    agent_id        UUID NOT NULL REFERENCES agents(id) ON DELETE CASCADE,
    parent_kpi_id   UUID REFERENCES kpis(id),
    name            TEXT NOT NULL,
    description     TEXT,
    target_value    TEXT,
    unit            TEXT,
    direction       TEXT NOT NULL DEFAULT 'higher_is_better',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_kpis_tenant ON kpis(tenant_id);
CREATE INDEX idx_kpis_agent ON kpis(agent_id);

CREATE TABLE kpi_values (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    kpi_id      UUID NOT NULL REFERENCES kpis(id) ON DELETE CASCADE,
    tenant_id   UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    value       TEXT NOT NULL,
    recorded_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    source      TEXT  -- which agent reported this
);
CREATE INDEX idx_kpi_values_kpi ON kpi_values(kpi_id);

-- ── Approvals ────────────────────────────────────────────────────────────────
CREATE TABLE approvals (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    agent_id        UUID NOT NULL REFERENCES agents(id),
    tool_name       TEXT NOT NULL,
    tool_arguments  JSONB,
    description     TEXT NOT NULL,  -- human-readable "Agent X wants to send email to ..."
    status          TEXT NOT NULL DEFAULT 'pending',
                    -- 'pending' | 'approved' | 'denied'
    scope           TEXT NOT NULL DEFAULT 'once',
                    -- 'once' | 'agent_tool' | 'agent_tool_param'
    scope_key       TEXT,           -- serialized scope for long-lived matching
    decided_at      TIMESTAMPTZ,
    durable_request_id TEXT,        -- Durable Task request port ID for resume
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_approvals_tenant ON approvals(tenant_id);
CREATE INDEX idx_approvals_status ON approvals(status);

-- ── Meetings (agent-to-agent structured meetings) ────────────────────────────
CREATE TABLE meetings (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id           UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    task_id             UUID REFERENCES tasks(id),
    topic               TEXT NOT NULL,
    meeting_type        TEXT NOT NULL DEFAULT 'kickoff_sync',  -- 'kickoff_sync' | 'status_check' | 'decision_request' | 'escalation' | 'briefing'
    status              TEXT NOT NULL DEFAULT 'in_progress',   -- 'in_progress' | 'completed' | 'failed'
    organizer_agent_id  UUID REFERENCES agents(id),
    started_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    ended_at            TIMESTAMPTZ,
    summary             TEXT
);
CREATE INDEX idx_meetings_tenant ON meetings(tenant_id);
CREATE INDEX idx_meetings_task ON meetings(task_id);

CREATE TABLE meeting_participants (
    meeting_id  UUID NOT NULL REFERENCES meetings(id) ON DELETE CASCADE,
    agent_id    UUID NOT NULL REFERENCES agents(id),
    role        TEXT NOT NULL DEFAULT 'participant',            -- 'organizer' | 'participant'
    PRIMARY KEY (meeting_id, agent_id)
);

CREATE TABLE meeting_messages (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    meeting_id  UUID NOT NULL REFERENCES meetings(id) ON DELETE CASCADE,
    tenant_id   UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    agent_id    UUID REFERENCES agents(id),  -- NULL = system/user message
    role        TEXT NOT NULL,               -- 'user' | 'assistant' | 'system'
    content     TEXT NOT NULL,
    sequence    INTEGER NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_meeting_messages_meeting ON meeting_messages(meeting_id);

-- ── Scheduled Tasks ───────────────────────────────────────────────────────────
CREATE TABLE scheduled_tasks (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    agent_id        UUID NOT NULL REFERENCES agents(id) ON DELETE CASCADE,
    name            TEXT NOT NULL,
    cron_expression TEXT NOT NULL,
    prompt          TEXT NOT NULL,
    status          TEXT NOT NULL DEFAULT 'active',   -- 'active' | 'paused' | 'deleted'
    durable_instance_id TEXT,                          -- Durable Task orchestration instance
    last_run_at     TIMESTAMPTZ,
    next_run_at     TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_scheduled_tasks_tenant ON scheduled_tasks(tenant_id);
CREATE INDEX idx_scheduled_tasks_agent ON scheduled_tasks(agent_id);

-- ── Semantic Memory (vector store) ────────────────────────────────────────────
CREATE TABLE memory_entries (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    agent_id        UUID REFERENCES agents(id),   -- NULL = shared team memory
    collection      TEXT NOT NULL DEFAULT 'default',
    content         TEXT NOT NULL,
    embedding       vector(1536),                 -- OpenAI ada-002 / text-embedding-3-small
    metadata        JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_memory_tenant ON memory_entries(tenant_id);
CREATE INDEX idx_memory_agent ON memory_entries(agent_id);
CREATE INDEX idx_memory_collection ON memory_entries(tenant_id, collection);
-- Vector similarity index (IVFFlat for approximate nearest neighbor)
CREATE INDEX idx_memory_embedding ON memory_entries
    USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);

-- ── Executive Reports ─────────────────────────────────────────────────────────
CREATE TABLE executive_reports (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id   UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    task_id     UUID REFERENCES tasks(id),
    agent_id    UUID NOT NULL REFERENCES agents(id),  -- agent that generated it
    title       TEXT NOT NULL,
    content     TEXT NOT NULL,
    report_type TEXT NOT NULL DEFAULT 'status',  -- 'status' | 'milestone' | 'alert'
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_reports_tenant ON executive_reports(tenant_id);

-- ── Chat History (persistent agent conversation history) ─────────────────────
CREATE TABLE agent_chat_history (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    agent_id        UUID NOT NULL REFERENCES agents(id) ON DELETE CASCADE,
    session_id      TEXT NOT NULL,
    role            TEXT NOT NULL,   -- 'user' | 'assistant' | 'system' | 'tool'
    content         TEXT NOT NULL,
    tool_calls      JSONB,
    tool_call_id    TEXT,
    sequence        INTEGER NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_chat_history_agent ON agent_chat_history(agent_id);
CREATE INDEX idx_chat_history_session ON agent_chat_history(agent_id, session_id);

-- ── Row-Level Security ────────────────────────────────────────────────────────
-- All tenant-scoped tables enforce isolation via RLS.
-- The application sets app.tenant_id on each DB connection.

ALTER TABLE users                 ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_provider_configs ENABLE ROW LEVEL SECURITY;
ALTER TABLE budgets               ENABLE ROW LEVEL SECURITY;
ALTER TABLE spend_entries         ENABLE ROW LEVEL SECURITY;
ALTER TABLE tasks                 ENABLE ROW LEVEL SECURITY;
ALTER TABLE agents                ENABLE ROW LEVEL SECURITY;
ALTER TABLE actions               ENABLE ROW LEVEL SECURITY;
ALTER TABLE kpis                  ENABLE ROW LEVEL SECURITY;
ALTER TABLE kpi_values            ENABLE ROW LEVEL SECURITY;
ALTER TABLE approvals             ENABLE ROW LEVEL SECURITY;
ALTER TABLE meetings              ENABLE ROW LEVEL SECURITY;
ALTER TABLE meeting_messages      ENABLE ROW LEVEL SECURITY;
ALTER TABLE meeting_participants  ENABLE ROW LEVEL SECURITY;
ALTER TABLE scheduled_tasks       ENABLE ROW LEVEL SECURITY;
ALTER TABLE memory_entries        ENABLE ROW LEVEL SECURITY;
ALTER TABLE executive_reports     ENABLE ROW LEVEL SECURITY;
ALTER TABLE agent_chat_history    ENABLE ROW LEVEL SECURITY;

-- Helper function: get current tenant from session config
CREATE OR REPLACE FUNCTION current_tenant_id() RETURNS UUID AS $$
BEGIN
  RETURN current_setting('app.tenant_id', true)::UUID;
EXCEPTION WHEN others THEN
  RETURN NULL;
END;
$$ LANGUAGE plpgsql STABLE;

-- Create RLS policies for each table (read + write isolation)
DO $$
DECLARE
  tbl TEXT;
  tables TEXT[] := ARRAY[
    'users', 'tenant_provider_configs', 'budgets', 'spend_entries',
    'tasks', 'agents', 'actions', 'kpis', 'kpi_values', 'approvals',
    'meetings', 'meeting_messages', 'scheduled_tasks',
    'memory_entries', 'executive_reports', 'agent_chat_history'
  ];
BEGIN
  FOREACH tbl IN ARRAY tables LOOP
    EXECUTE format(
      'CREATE POLICY tenant_isolation ON %I
       USING (tenant_id = current_tenant_id())
       WITH CHECK (tenant_id = current_tenant_id())',
      tbl
    );
  END LOOP;
END $$;

-- meeting_participants links via meeting_id; use a join-based policy
CREATE POLICY tenant_isolation ON meeting_participants
    USING (
        EXISTS (
            SELECT 1 FROM meetings m
            WHERE m.id = meeting_id
              AND m.tenant_id = current_tenant_id()
        )
    );

-- Superuser / service role bypasses RLS (for migrations, admin operations)
-- Application code uses a restricted role that obeys RLS.
CREATE ROLE taim_app;
GRANT CONNECT ON DATABASE taim TO taim_app;
GRANT USAGE ON SCHEMA public TO taim_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO taim_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO taim_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO taim_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO taim_app;

-- Seed: default tenant for local development
INSERT INTO tenants (id, name)
VALUES ('00000000-0000-0000-0000-000000000001'::UUID, 'Local Dev Tenant');

-- Seed: default admin user (password: taim-admin)
INSERT INTO users (id, tenant_id, email, password_hash)
VALUES (
    '00000000-0000-0000-0000-000000000002'::UUID,
    '00000000-0000-0000-0000-000000000001'::UUID,
    'admin@taim.local',
    '$2b$10$C3dtX8z2b4u8NUhra269xeY/lDAqkXtC2LN/CEiju2A9J9jc0BC7e'
);
