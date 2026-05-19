namespace Taim.Agents.Shared;

/// <summary>
/// Resolves agent, chat client, and tools for an action, then fires the work loop
/// in a background Task. Registered as Scoped in DI.
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// Triggers execution of the given action. Returns false if the action is not
    /// in a triggerable state (anything other than 'open' or 'blocked').
    /// The actual loop runs in a background Task; this method returns immediately.
    /// </summary>
    Task<bool> TriggerAsync(Guid tenantId, Guid actionId, CancellationToken ct = default);
}
