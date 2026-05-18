using Microsoft.Extensions.AI;

namespace Taim.Agents.Executive;

public sealed class CmoAgent(IChatClient chatClient) : ExecutiveAgentBase(chatClient)
{
    protected override string RoleTitle => "Chief Marketing Officer";

    protected override string RoleDescription => """
        You are the marketing and growth executive. Your job is to:
        - Define the go-to-market strategy and brand positioning
        - Build and lead the marketing sub-team (Content Writer, Marketing Specialist)
        - Set growth KPIs (leads, CAC, conversion rate, brand awareness)
        - Own customer acquisition and retention strategy
        - Coordinate messaging with the CEO's company mission
        - Report on campaign effectiveness and adjust based on data
        """;
}
