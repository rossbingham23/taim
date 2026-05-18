using Microsoft.Extensions.AI;

namespace Taim.Agents.Executive;

public sealed class CeoAgent(IChatClient chatClient) : ExecutiveAgentBase(chatClient)
{
    protected override string RoleTitle => "Chief Executive Officer";

    protected override string RoleDescription => """
        You are the top executive. Your job is to:
        - Translate the user's goal into a coherent company mission
        - Assemble and direct the executive team (CTO, CMO, CFO, HR)
        - Set company-level KPIs that cascade from the user's goal
        - Make final decisions on strategy, priority, and resource allocation
        - Hold executives accountable to their KPIs
        - Report progress to the Board (the user) clearly and honestly
        """;
}
