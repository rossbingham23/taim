using Microsoft.Extensions.AI;
using Taim.Agents.Shared;
using Taim.Core.Agents;

namespace Taim.Agents.Expert;

/// <summary>
/// Gathers domain knowledge relevant to a user's goal before team creation.
/// Called by BootstrapAgent to understand the landscape before recommending agents.
/// Produces structured expert knowledge that informs team composition and KPIs.
/// </summary>
public sealed class ExpertAgent(IChatClient chatClient)
{
    private const string SystemPrompt = """
        You are a world-class domain expert and business consultant.
        Your job is to provide structured, actionable knowledge about a given goal's domain.

        When analyzing a goal, provide:
        1. Domain overview — what field(s) this goal operates in
        2. Key success factors — what separates successful from failed attempts
        3. Common pitfalls — the top 3-5 mistakes people make
        4. Required expertise — what skill sets are genuinely needed
        5. Recommended KPIs — 3-5 measurable indicators of progress
        6. Timeline reality check — realistic milestones for the budget and scope

        Be honest and specific. Avoid generic advice.
        Respond ONLY with a JSON object in exactly this structure (all field names must be camelCase):
        {
          "domain": "primary domain name",
          "overview": "domain overview text",
          "keySuccessFactors": ["factor 1", "factor 2"],
          "commonPitfalls": ["pitfall 1", "pitfall 2"],
          "requiredExpertise": ["expertise 1", "expertise 2"],
          "recommendedKpis": ["KPI 1", "KPI 2"],
          "timelineReality": "honest assessment of timeline"
        }
        """;

    public async Task<ExpertKnowledge> GatherKnowledgeAsync(string goal, string? domain = null, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, $"""
                Goal: {goal}
                {(domain is not null ? $"Domain context: {domain}" : string.Empty)}

                Provide expert knowledge to inform agent team composition and KPIs.
                Return an ExpertKnowledge JSON object.
                """)
        };

        var response = await chatClient.GetResponseAsync(messages, null, ct);

        return AgentJson.Deserialize<ExpertKnowledge>(response.Text, "ExpertAgent");
    }
}

public sealed record ExpertKnowledge(
    string Domain,
    string Overview,
    IReadOnlyList<string> KeySuccessFactors,
    IReadOnlyList<string> CommonPitfalls,
    IReadOnlyList<string> RequiredExpertise,
    IReadOnlyList<string> RecommendedKpis,
    string TimelineReality
);
