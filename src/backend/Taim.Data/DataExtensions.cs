using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Taim.Core.Actions;
using Taim.Core.Agents;
using Taim.Core.Approvals;
using Taim.Core.Budget;
using Taim.Core.KPIs;
using Taim.Core.Meetings;
using Taim.Core.Providers;
using Taim.Core.Reports;
using Taim.Core.System;
using Taim.Core.Teams;
using Taim.Data.Services;

namespace Taim.Data;

public static class DataExtensions
{
    public static IServiceCollection AddTaimData(this IServiceCollection services, IConfiguration config)
    {
        var pgConnection = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

        var redisConnection = config.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");

        // Redis — singleton multiplexer
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnection));

        // TenantIdAccessor is Scoped: one per HTTP request or operation context
        services.AddScoped<TenantIdAccessor>();
        services.AddScoped<ITenantIdAccessor>(sp => sp.GetRequiredService<TenantIdAccessor>());
        services.AddScoped<RlsInterceptor>();

        // AddDbContext (not Pool) — required because RlsInterceptor is Scoped and needs per-request resolution.
        services.AddDbContext<TaimDbContext>((sp, options) =>
        {
            options
                .UseNpgsql(pgConnection, npgsql =>
                {
                    npgsql.UseVector();
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                })
                .AddInterceptors(sp.GetRequiredService<RlsInterceptor>());
        });

        // Domain service implementations
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IAgentRegistry, AgentRegistryService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ITenantProviderResolver, TenantProviderResolver>();
        services.AddScoped<IKpiService, KpiService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IMeetingStore, MeetingService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IActionService, ActionService>();
        services.AddScoped<ISystemStopService, SystemStopService>();

        return services;
    }
}
