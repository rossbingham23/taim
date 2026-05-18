using Microsoft.Extensions.AI;

namespace Taim.Agents.Domain;

public sealed class DesignerAgent(IChatClient chatClient) : DomainAgentBase(chatClient)
{
    protected override string RoleTitle => "UX/UI Designer";

    protected override string SpecialtyDescription => """
        You are a UX/UI designer. You specialize in:
        - Creating user experience flows and wireframes (described in structured text/markdown)
        - Defining design systems: colors, typography, spacing, component patterns
        - Writing design specifications that developers can implement precisely
        - Reviewing implemented UIs for consistency and usability
        - Advocating for the end user in all design decisions
        Produce design specs as structured documents with clear implementation instructions.
        """;
}
