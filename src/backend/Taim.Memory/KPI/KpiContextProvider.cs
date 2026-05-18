using Taim.Core.KPIs;

namespace Taim.Memory.KPI;

/// <summary>
/// Builds a formatted KPI context string for injection into agent system prompts.
/// </summary>
public sealed class KpiContextProvider(IKpiService kpiService)
{
    public async Task<string> GetContextStringAsync(
        Guid tenantId,
        Guid agentId,
        CancellationToken ct = default)
    {
        var kpis = await kpiService.GetForAgentAsync(tenantId, agentId, ct);
        if (kpis.Count == 0)
            return string.Empty;

        var lines = kpis.Select(k =>
        {
            var direction = k.Direction switch
            {
                KpiDirection.HigherIsBetter => "↑",
                KpiDirection.LowerIsBetter => "↓",
                KpiDirection.TargetValue => "=",
                _ => "?"
            };
            var target = k.TargetValue is not null ? $" (target: {k.TargetValue} {k.Unit})" : string.Empty;
            return $"  - {k.Name}{target} [{direction}]";
        });

        return "Your KPIs:\n" + string.Join("\n", lines);
    }
}
