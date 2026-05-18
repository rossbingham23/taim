using Taim.Agents.Shared;
using Taim.Budget;
using Taim.Core.Notifications;
using Taim.Data;
using Taim.Notifications;
using Taim.Providers;

var builder = Host.CreateApplicationBuilder(args);

// ── Notifications ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<INotificationService, NotificationService>();

// ── Data + Redis ──────────────────────────────────────────────────────────────
builder.Services.AddTaimData(builder.Configuration);

// ── LLM Providers ─────────────────────────────────────────────────────────────
builder.Services.AddTaimProviders();

// ── Budget ─────────────────────────────────────────────────────────────────────
builder.Services.AddTaimBudget();

// ── Agents ────────────────────────────────────────────────────────────────────
builder.Services.AddTaimAgents();

// ── Durable Task worker (agents + orchestrations) ─────────────────────────────
// TODO Phase 6: builder.Services.AddTaimWorkflows(builder.Configuration);

var host = builder.Build();
host.Run();
