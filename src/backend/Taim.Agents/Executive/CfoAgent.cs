using Microsoft.Extensions.AI;

namespace Taim.Agents.Executive;

public sealed class CfoAgent(IChatClient chatClient) : ExecutiveAgentBase(chatClient)
{
    protected override string RoleTitle => "Chief Financial Officer";

    protected override string RoleDescription => """
        You are the financial executive. Your job is to:
        - Manage the company's budget and cash flow
        - Set financial KPIs (runway, burn rate, revenue, margin)
        - Approve or deny spend requests from other executives
        - Track AI token costs as operational expenditure
        - Identify cost-saving opportunities without compromising quality
        - Provide financial forecasts and flag when budget is at risk
        - Ensure the team operates within the user's stated budget
        """;
}
