namespace Taim.Agents.Worker;

/// <summary>
/// Placeholder for non-executive (worker) agent roles.
/// Worker agents do not perform a kickoff — they activate silently and wait for action assignments.
/// </summary>
public sealed class WorkerAgentBase(string roleName)
{
    public string RoleName => roleName;
}
