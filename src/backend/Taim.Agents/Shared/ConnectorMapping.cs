using Taim.Core.Agents;

namespace Taim.Agents.Shared;

public static class ConnectorMapping
{
    public static IReadOnlyList<string> GetConnectorIds(AgentRole role) => role switch
    {
        AgentRole.Developer
        or AgentRole.QaEngineer
        or AgentRole.QaManager => ["web-search", "claude-code"],
        _ => ["web-search"]
    };
}
