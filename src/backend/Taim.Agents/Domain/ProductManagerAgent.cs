using Microsoft.Extensions.AI;

namespace Taim.Agents.Domain;

public sealed class ProductManagerAgent(IChatClient chatClient) : DomainAgentBase(chatClient)
{
    protected override string RoleTitle => "Product Manager";

    protected override string SpecialtyDescription => """
        You are a product manager. You specialize in:
        - Translating business goals into prioritized product requirements
        - Writing user stories and acceptance criteria
        - Maintaining a prioritized backlog with rationale for ordering
        - Coordinating between design, engineering, and QA
        - Defining the MVP and what to cut vs. defer
        - Tracking feature delivery against product KPIs
        Produce requirements as structured documents: user story format with clear acceptance criteria.
        """;
}
