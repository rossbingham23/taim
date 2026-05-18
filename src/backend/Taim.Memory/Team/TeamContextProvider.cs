using Taim.Core.Agents;

namespace Taim.Memory.Team;

/// <summary>
/// Builds a formatted team context string for injection into agent system prompts.
/// Shows the agent's manager chain and direct reports.
/// </summary>
public sealed class TeamContextProvider(IAgentRegistry registry)
{
    public async Task<string> GetContextStringAsync(
        Guid tenantId,
        Guid agentId,
        CancellationToken ct = default)
    {
        var self = await registry.GetAsync(tenantId, agentId, ct);
        if (self is null) return string.Empty;

        var lines = new List<string>();

        // Direct reports
        var reports = await registry.GetTeamAsync(tenantId, agentId, ct);
        if (reports.Count > 0)
        {
            lines.Add("Your direct reports:");
            foreach (var r in reports)
                lines.Add($"  - {r.Name} ({r.Role}): {Truncate(r.Charter, 80)}");
        }

        // Peers (siblings — same parent)
        if (self.ParentAgentId.HasValue)
        {
            var peers = await registry.GetTeamAsync(tenantId, self.ParentAgentId, ct);
            var sibs = peers.Where(p => p.Id != agentId).ToList();
            if (sibs.Count > 0)
            {
                lines.Add("Your peers:");
                foreach (var p in sibs)
                    lines.Add($"  - {p.Name} ({p.Role})");
            }

            var manager = await registry.GetAsync(tenantId, self.ParentAgentId.Value, ct);
            if (manager is not null)
                lines.Add($"You report to: {manager.Name} ({manager.Role})");
        }

        return lines.Count > 0 ? string.Join("\n", lines) : string.Empty;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
