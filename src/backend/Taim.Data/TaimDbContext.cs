using Microsoft.EntityFrameworkCore;
using Taim.Data.Models;

namespace Taim.Data;

public class TaimDbContext(DbContextOptions<TaimDbContext> options) : DbContext(options)
{
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<TenantProviderConfigEntity> TenantProviderConfigs => Set<TenantProviderConfigEntity>();
    public DbSet<BudgetEntity> Budgets => Set<BudgetEntity>();
    public DbSet<SpendEntryEntity> SpendEntries => Set<SpendEntryEntity>();
    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();
    public DbSet<KpiEntity> Kpis => Set<KpiEntity>();
    public DbSet<KpiValueEntity> KpiValues => Set<KpiValueEntity>();
    public DbSet<ApprovalEntity> Approvals => Set<ApprovalEntity>();
    public DbSet<MeetingEntity> Meetings => Set<MeetingEntity>();
    public DbSet<MeetingParticipantEntity> MeetingParticipants => Set<MeetingParticipantEntity>();
    public DbSet<MeetingMessageEntity> MeetingMessages => Set<MeetingMessageEntity>();
    public DbSet<ScheduledTaskEntity> ScheduledTasks => Set<ScheduledTaskEntity>();
    public DbSet<MemoryEntryEntity> MemoryEntries => Set<MemoryEntryEntity>();
    public DbSet<ExecutiveReportEntity> ExecutiveReports => Set<ExecutiveReportEntity>();
    public DbSet<AgentChatHistoryEntity> AgentChatHistory => Set<AgentChatHistoryEntity>();
    public DbSet<ActionEntity> Actions => Set<ActionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("uuid-ossp")
                    .HasPostgresExtension("vector")
                    .HasPostgresExtension("pg_trgm");

        // ── Tenants ──────────────────────────────────────────────────────────
        modelBuilder.Entity<TenantEntity>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── Users ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<UserEntity>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Email).IsUnique();
        });

        // ── TenantProviderConfigs ─────────────────────────────────────────────
        modelBuilder.Entity<TenantProviderConfigEntity>(e =>
        {
            e.ToTable("tenant_provider_configs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Provider).HasColumnName("provider");
            e.Property(x => x.ApiKey).HasColumnName("api_key");
            e.Property(x => x.BaseUrl).HasColumnName("base_url");
            e.Property(x => x.DefaultModel).HasColumnName("default_model");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.TenantId, x.Provider }).IsUnique();
        });

        // ── Budgets ───────────────────────────────────────────────────────────
        modelBuilder.Entity<BudgetEntity>(e =>
        {
            e.ToTable("budgets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.TaskId).HasColumnName("task_id");
            e.Property(x => x.LimitUsd).HasColumnName("limit_usd").HasColumnType("numeric(12,6)");
            e.Property(x => x.SpentUsd).HasColumnName("spent_usd").HasColumnType("numeric(12,6)");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // ── SpendEntries ──────────────────────────────────────────────────────
        modelBuilder.Entity<SpendEntryEntity>(e =>
        {
            e.ToTable("spend_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.BudgetId).HasColumnName("budget_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.Provider).HasColumnName("provider");
            e.Property(x => x.Model).HasColumnName("model");
            e.Property(x => x.InputTokens).HasColumnName("input_tokens");
            e.Property(x => x.OutputTokens).HasColumnName("output_tokens");
            e.Property(x => x.CostUsd).HasColumnName("cost_usd").HasColumnType("numeric(12,6)");
            e.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            e.HasOne<BudgetEntity>().WithMany(b => b.SpendEntries).HasForeignKey(x => x.BudgetId);
        });

        // ── Tasks ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<TaskEntity>(e =>
        {
            e.ToTable("tasks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Goal).HasColumnName("goal");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.BudgetId).HasColumnName("budget_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // ── Agents ────────────────────────────────────────────────────────────
        modelBuilder.Entity<AgentEntity>(e =>
        {
            e.ToTable("agents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.TaskId).HasColumnName("task_id");
            e.Property(x => x.ParentAgentId).HasColumnName("parent_agent_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.Charter).HasColumnName("charter");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Provider).HasColumnName("provider");
            e.Property(x => x.Model).HasColumnName("model");
            e.Property(x => x.DurableEntityKey).HasColumnName("durable_entity_key");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne<TaskEntity>().WithMany(t => t.Agents).HasForeignKey(a => a.TaskId);
            e.HasMany(x => x.Kpis).WithOne().HasForeignKey(k => k.AgentId);
        });

        // ── KPIs ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<KpiEntity>(e =>
        {
            e.ToTable("kpis");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.ParentKpiId).HasColumnName("parent_kpi_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.TargetValue).HasColumnName("target_value");
            e.Property(x => x.Unit).HasColumnName("unit");
            e.Property(x => x.Direction).HasColumnName("direction");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasMany(x => x.Values).WithOne().HasForeignKey(v => v.KpiId);
        });

        // ── KpiValues ─────────────────────────────────────────────────────────
        modelBuilder.Entity<KpiValueEntity>(e =>
        {
            e.ToTable("kpi_values");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.KpiId).HasColumnName("kpi_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Value).HasColumnName("value");
            e.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            e.Property(x => x.Source).HasColumnName("source");
        });

        // ── Approvals ─────────────────────────────────────────────────────────
        modelBuilder.Entity<ApprovalEntity>(e =>
        {
            e.ToTable("approvals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.ToolName).HasColumnName("tool_name");
            e.Property(x => x.ToolArguments).HasColumnName("tool_arguments").HasColumnType("jsonb");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Scope).HasColumnName("scope");
            e.Property(x => x.ScopeKey).HasColumnName("scope_key");
            e.Property(x => x.DecidedAt).HasColumnName("decided_at");
            e.Property(x => x.DurableRequestId).HasColumnName("durable_request_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── Meetings ──────────────────────────────────────────────────────────
        modelBuilder.Entity<MeetingEntity>(e =>
        {
            e.ToTable("meetings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.TaskId).HasColumnName("task_id");
            e.Property(x => x.Topic).HasColumnName("topic");
            e.Property(x => x.MeetingType).HasColumnName("meeting_type");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.OrganizerAgentId).HasColumnName("organizer_agent_id");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.EndedAt).HasColumnName("ended_at");
            e.Property(x => x.Summary).HasColumnName("summary");
            e.HasMany(x => x.Messages).WithOne().HasForeignKey(m => m.MeetingId);
            e.HasMany(x => x.Participants).WithOne().HasForeignKey(p => p.MeetingId);
        });

        // ── MeetingParticipants ───────────────────────────────────────────────
        modelBuilder.Entity<MeetingParticipantEntity>(e =>
        {
            e.ToTable("meeting_participants");
            e.HasKey(x => new { x.MeetingId, x.AgentId });
            e.Property(x => x.MeetingId).HasColumnName("meeting_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.Role).HasColumnName("role");
        });

        // ── MeetingMessages ───────────────────────────────────────────────────
        modelBuilder.Entity<MeetingMessageEntity>(e =>
        {
            e.ToTable("meeting_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.MeetingId).HasColumnName("meeting_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.Sequence).HasColumnName("sequence");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── ScheduledTasks ────────────────────────────────────────────────────
        modelBuilder.Entity<ScheduledTaskEntity>(e =>
        {
            e.ToTable("scheduled_tasks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.CronExpression).HasColumnName("cron_expression");
            e.Property(x => x.Prompt).HasColumnName("prompt");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.DurableInstanceId).HasColumnName("durable_instance_id");
            e.Property(x => x.LastRunAt).HasColumnName("last_run_at");
            e.Property(x => x.NextRunAt).HasColumnName("next_run_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── MemoryEntries ─────────────────────────────────────────────────────
        modelBuilder.Entity<MemoryEntryEntity>(e =>
        {
            e.ToTable("memory_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.Collection).HasColumnName("collection");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.Embedding).HasColumnName("embedding").HasColumnType("vector(1536)");
            e.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── ExecutiveReports ──────────────────────────────────────────────────
        modelBuilder.Entity<ExecutiveReportEntity>(e =>
        {
            e.ToTable("executive_reports");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.TaskId).HasColumnName("task_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.ReportType).HasColumnName("report_type");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── Actions ───────────────────────────────────────────────────────────────
        modelBuilder.Entity<ActionEntity>(e =>
        {
            e.ToTable("actions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.TaskId).HasColumnName("task_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.CreatedByAgentId).HasColumnName("created_by_agent_id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Priority).HasColumnName("priority");
            e.Property(x => x.ParentActionId).HasColumnName("parent_action_id");
            e.Property(x => x.DueAt).HasColumnName("due_at");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // ── AgentChatHistory ──────────────────────────────────────────────────
        modelBuilder.Entity<AgentChatHistoryEntity>(e =>
        {
            e.ToTable("agent_chat_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.ToolCalls).HasColumnName("tool_calls").HasColumnType("jsonb");
            e.Property(x => x.ToolCallId).HasColumnName("tool_call_id");
            e.Property(x => x.Sequence).HasColumnName("sequence");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}
