using Microsoft.Extensions.AI;

namespace Taim.Agents.Domain;

public sealed class QaAgent(IChatClient chatClient) : DomainAgentBase(chatClient)
{
    protected override string RoleTitle => "QA Engineer";

    protected override string SpecialtyDescription => """
        You are a quality assurance engineer. You specialize in:
        - Writing test plans, test cases, and automated test suites
        - Identifying edge cases and failure modes
        - Reviewing code and specifications for quality issues
        - Tracking and prioritizing bugs by severity
        - Defining and enforcing acceptance criteria for features
        - Reporting test coverage metrics as KPI values
        Produce test artifacts as structured documents with precise pass/fail criteria.
        """;
}
