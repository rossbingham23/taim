using Microsoft.Extensions.AI;
using Taim.Agents.Bootstrap;
using Taim.Agents.Shared;

namespace Taim.Agents.Executive;

/// <summary>
/// Context injected into every executive agent call.
/// Built by the workflow layer from DB state; agents never query the DB directly.
/// </summary>
public sealed record ExecutiveContext(
    Guid TenantId,
    Guid AgentId,
    string AgentName,
    string Role,
    string Charter,
    string Goal,
    IReadOnlyList<string> ParentKpis,
    IReadOnlyList<string> OwnKpis,
    string? TeamContext
);

public sealed record ExecutiveResponse(
    string Analysis,
    string Decision,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Delegations
);

public sealed record ProposedKpi(
    string Name,
    string Description,
    string TargetValue,
    string Unit,
    string Direction   // "HigherIsBetter" | "LowerIsBetter" | "TargetValue"
);

public abstract class ExecutiveAgentBase(IChatClient chatClient)
{
    protected abstract string RoleTitle { get; }
    protected abstract string RoleDescription { get; }

    private string BuildSystemPrompt(ExecutiveContext ctx)
    {
        var parentKpis = ctx.ParentKpis.Count > 0
            ? string.Join("\n", ctx.ParentKpis.Select(k => $"  - {k}"))
            : "  (none yet — you are the top-level executive)";

        var ownKpis = ctx.OwnKpis.Count > 0
            ? string.Join("\n", ctx.OwnKpis.Select(k => $"  - {k}"))
            : "  (not yet defined — propose them via ProposeKpisAsync)";

        var team = ctx.TeamContext ?? "  (team not yet assembled)";

        return $"""
            You are {ctx.AgentName}, {RoleTitle}.
            {RoleDescription}

            Charter: {ctx.Charter}

            Top-level user goal: {ctx.Goal}

            KPIs you must serve (from your principal):
            {parentKpis}

            Your own KPIs:
            {ownKpis}

            Your team:
            {team}

            Guidelines:
            - Be decisive and concise. No fluff.
            - Always distinguish what YOU will handle vs. what you delegate.
            - Propose concrete, measurable actions with owners.
            - Flag budget or blocker concerns immediately.
            - Think in terms of systems, not one-off tasks.
            """;
    }

    public async Task<ExecutiveResponse> RunAsync(ExecutiveContext ctx, string instruction, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, BuildSystemPrompt(ctx) + """

Respond ONLY with a JSON object in exactly this shape (no markdown, no extra fields):
{
  "analysis": "your strategic analysis",
  "decision": "your key decision or stance",
  "actions": ["thing you will personally do", "another personal action"],
  "delegations": ["Brief task title for a direct report", "Another task title"]
}
actions and delegations must be arrays of plain strings, not objects.
"""),
            new(ChatRole.User, instruction)
        };

        var response = await chatClient.GetResponseAsync(messages, null, ct);

        return AgentJson.Deserialize<ExecutiveResponse>(response.Text, RoleTitle);
    }

    public async Task<IReadOnlyList<ProposedKpi>> ProposeKpisAsync(ExecutiveContext ctx, CancellationToken ct = default)
    {
        var parentKpis = ctx.ParentKpis.Count > 0
            ? string.Join(", ", ctx.ParentKpis)
            : "none specified yet";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, BuildSystemPrompt(ctx) + "\n\nRespond ONLY with a JSON array of ProposedKpi objects."),
            new(ChatRole.User, $"""
                Propose 3-5 KPIs for your role that demonstrably contribute to:
                {parentKpis}

                Each KPI must be:
                - Specific and measurable
                - Achievable given realistic constraints
                - Directly tied to the user's goal

                Return a JSON array of ProposedKpi objects.
                """)
        };

        var response = await chatClient.GetResponseAsync(messages, null, ct);

        return AgentJson.Deserialize<List<ProposedKpi>>(response.Text, $"{RoleTitle}.ProposeKpis");
    }

    public async Task<IReadOnlyList<AgentSpec>> ProposeSubTeamAsync(ExecutiveContext ctx, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, BuildSystemPrompt(ctx) + "\n\nRespond ONLY with a JSON array of AgentSpec objects."),
            new(ChatRole.User, $"""
                Propose the sub-team that should report to you.
                Keep it lean — start with the minimum roles needed to make real progress.
                For each agent: name, role (from the allowed list), charter, and optionally a preferred provider/model.

                Allowed roles: Developer, Designer, QaEngineer, ProductManager, MarketingSpecialist,
                               ContentWriter, DataAnalyst, SalesRepresentative, CustomerSupport, Generic

                Return a JSON array of AgentSpec objects with fields:
                Name, Role, Charter, PreferredProvider (nullable), PreferredModel (nullable)
                """)
        };

        var response = await chatClient.GetResponseAsync(messages, null, ct);

        return AgentJson.Deserialize<List<AgentSpec>>(response.Text, $"{RoleTitle}.ProposeSubTeam");
    }
}
