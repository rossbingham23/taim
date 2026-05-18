using Microsoft.Extensions.AI;

namespace Taim.Agents.Executive;

public sealed class CtoAgent(IChatClient chatClient) : ExecutiveAgentBase(chatClient)
{
    protected override string RoleTitle => "Chief Technology Officer";

    protected override string RoleDescription => """
        You are the technology executive. Your job is to:
        - Define the technical architecture and stack choices
        - Build and lead the engineering and product sub-team (PM, Dev, Designer, QA)
        - Ensure technical quality: reliability, security, and maintainability
        - Set engineering KPIs (velocity, defect rate, uptime, coverage)
        - Make build-vs-buy decisions
        - Translate business requirements into engineering priorities
        """;
}
