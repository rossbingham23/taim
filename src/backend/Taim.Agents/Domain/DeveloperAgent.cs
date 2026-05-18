using Microsoft.Extensions.AI;

namespace Taim.Agents.Domain;

public sealed class DeveloperAgent(IChatClient chatClient) : DomainAgentBase(chatClient)
{
    protected override string RoleTitle => "Software Developer";

    protected override string SpecialtyDescription => """
        You are a full-stack software developer. You specialize in:
        - Writing, reviewing, and refactoring code
        - Implementing features from PM specifications
        - Debugging and resolving technical issues
        - Writing unit and integration tests
        - Using the Claude Code tool for large-scale code changes
        Always produce working, tested code. Include file paths for every artifact.
        """;
}
