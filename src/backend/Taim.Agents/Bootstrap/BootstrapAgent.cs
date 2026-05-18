using Microsoft.Extensions.AI;
using Taim.Agents.Shared;

namespace Taim.Agents.Bootstrap;

/// <summary>
/// The entry point for every user-submitted goal.
/// Analyzes the goal and recommends an initial executive team structure.
/// Does not persist — runs once and terminates. The workflow handles DB writes.
/// </summary>
public sealed class BootstrapAgent(IChatClient chatClient)
{
    private const string SystemPrompt = """
        You are a world-class organizational consultant and AI agent architect.
        Your job is to analyze a user's goal and design the optimal team of AI agents to accomplish it.

        When given a goal you must:
        1. Identify the domain(s) involved (business, education, personal, technical, etc.)
        2. Determine what expert knowledge is needed
        3. Design an executive team (CEO and direct reports) appropriate for the scale of the goal
        4. For each executive agent, write a charter that:
           - States their role and responsibilities
           - Lists the sub-teams they should build
           - Specifies what KPIs they should set
           - Explains how their work connects to the user's goal
        5. Keep the team lean — start small and let agents expand as needed

        Respond ONLY with a JSON object in exactly this structure (all field names must be camelCase):
        {
          "summary": "brief description of the recommended team and approach",
          "executiveTeam": [
            {
              "name": "agent display name",
              "role": "one of: Ceo, Cto, Cmo, Cfo, Hr, ProductManager, Developer, Designer, QaEngineer, MarketingSpecialist, ContentWriter, DataAnalyst, SalesRepresentative, CustomerSupport, Generic",
              "charter": "detailed charter for this agent",
              "preferredProvider": null,
              "preferredModel": null
            }
          ],
          "initialKpis": ["KPI description 1", "KPI description 2"],
          "successCriteria": "how success will be measured"
        }
        """;

    public async Task<TeamRecommendation> RecommendTeamAsync(Guid tenantId, Guid taskId, string goal, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, $"""
                User Goal: {goal}
                Task ID: {taskId}

                Design the optimal agent team for this goal.
                Return a TeamRecommendation JSON object.
                """)
        };

        var response = await chatClient.GetResponseAsync(messages, null, ct);

        return AgentJson.Deserialize<TeamRecommendation>(response.Text, "BootstrapAgent");
    }
}

public sealed record AgentSpec(
    string Name,
    string Role,
    string Charter,
    string? PreferredProvider = null,
    string? PreferredModel = null
);

public sealed record TeamRecommendation(
    string Summary,
    IReadOnlyList<AgentSpec> ExecutiveTeam,
    IReadOnlyList<string> InitialKpis,
    string SuccessCriteria
);
