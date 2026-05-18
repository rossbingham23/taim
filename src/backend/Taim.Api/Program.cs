using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using Taim.Agents.Shared;
using Taim.Api.Endpoints;
using Taim.Api.Hubs;
using Taim.Api.Middleware;
using Taim.Budget;
using Taim.Connectors;
using Taim.Core.Activity;
using Taim.Core.Notifications;
using Taim.Data;
using Taim.Memory;
using Taim.Notifications;
using Taim.Providers;

var builder = WebApplication.CreateBuilder(args);

// Serialize enums as strings (camelCase) throughout the API
builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(
        System.Text.Json.JsonNamingPolicy.CamelCase));
});

// ── Authentication ─────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        // SignalR clients pass token via query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ── Data + Redis ──────────────────────────────────────────────────────────────
builder.Services.AddTaimData(builder.Configuration);

// ── LLM Providers ─────────────────────────────────────────────────────────────
builder.Services.AddTaimProviders();

// ── Budget ─────────────────────────────────────────────────────────────────────
builder.Services.AddTaimBudget();

// ── Agents ────────────────────────────────────────────────────────────────────
builder.Services.AddTaimAgents();

// ── Memory ────────────────────────────────────────────────────────────────────
builder.Services.AddTaimMemory();

// ── Connectors ────────────────────────────────────────────────────────────────
builder.Services.AddTaimConnectors();

// ── SignalR ────────────────────────────────────────────────────────────────────
// Use snake_case string enums so NotificationKind serializes as "executive_report" etc.
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
    });
builder.Services.AddSingleton<INotificationChannel, AgentHubNotifier>();
// ActivityFeedChannel is both a notification channel (captures events) and the IActivityFeed read interface
builder.Services.AddSingleton<ActivityFeedChannel>();
builder.Services.AddSingleton<IActivityFeed>(sp => sp.GetRequiredService<ActivityFeedChannel>());
builder.Services.AddSingleton<INotificationChannel>(sp => sp.GetRequiredService<ActivityFeedChannel>());
builder.Services.AddSingleton<INotificationService, NotificationService>();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration
            .GetValue("AllowedOrigins", "http://localhost:3000")!
            .Split(',', StringSplitOptions.TrimEntries);
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// ── OpenAPI ────────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// ── SignalR hub ────────────────────────────────────────────────────────────────
app.MapHub<AgentEventHub>("/hubs/agents");

// ── Health ────────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));

// ── API Endpoints ─────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapTaskEndpoints();
app.MapAgentEndpoints();
app.MapApprovalEndpoints();
app.MapKpiEndpoints();
app.MapReportEndpoints();
app.MapActivityEndpoints();
app.MapActionEndpoints();
app.MapMeetingEndpoints();

app.Run();
