using Microsoft.Extensions.AI;
using Taim.Agents.Shared;

namespace Taim.Agents.Domain;

public sealed record DomainContext(
    Guid TenantId,
    Guid AgentId,
    string AgentName,
    string Charter,
    string Goal,
    IReadOnlyList<string> OwnKpis,
    string? ManagerContext   // name + role of reporting executive
);

public sealed record DomainResult(
    string Output,
    bool Success,
    IReadOnlyList<string> Artifacts,   // file paths, URLs, document names produced
    string? FailureReason = null
);

public abstract class DomainAgentBase(IChatClient chatClient)
{
    protected abstract string RoleTitle { get; }
    protected abstract string SpecialtyDescription { get; }

    private string BuildSystemPrompt(DomainContext ctx)
    {
        var kpis = ctx.OwnKpis.Count > 0
            ? string.Join("\n", ctx.OwnKpis.Select(k => $"  - {k}"))
            : "  (assigned by your manager)";

        var manager = ctx.ManagerContext ?? "your manager";

        return $"""
            You are {ctx.AgentName}, {RoleTitle}.
            {SpecialtyDescription}

            Your charter: {ctx.Charter}
            Reporting to: {manager}
            Project goal: {ctx.Goal}

            Your KPIs:
            {kpis}

            Guidelines:
            - Focus on execution, not strategy. Deliver concrete outputs.
            - List every artifact you produce (file, document, URL, analysis).
            - If blocked, state the blocker clearly so your manager can resolve it.
            - Quality over speed. Prefer doing it right over doing it fast.
            """;
    }

    public async Task<DomainResult> ExecuteAsync(
        DomainContext ctx,
        string task,
        IReadOnlyList<AITool>? tools = null,
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, BuildSystemPrompt(ctx) + "\n\nRespond ONLY with a JSON object matching the DomainResult schema."),
            new(ChatRole.User, task)
        };

        var options = tools is { Count: > 0 }
            ? new ChatOptions { Tools = [..tools] }
            : null;

        var response = await chatClient.GetResponseAsync(messages, options, ct);

        return AgentJson.Deserialize<DomainResult>(response.Text, RoleTitle);
    }
}
