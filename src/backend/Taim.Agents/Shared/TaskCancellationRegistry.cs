using System.Collections.Concurrent;
using Taim.Core.Agents;

namespace Taim.Agents.Shared;

public sealed class TaskCancellationRegistry : ITaskCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    public CancellationToken Register(Guid taskId)
        => _sources.GetOrAdd(taskId, _ => new CancellationTokenSource()).Token;

    public void Cancel(Guid taskId)
    {
        if (_sources.TryGetValue(taskId, out var cts))
            cts.Cancel();
    }

    public void Unregister(Guid taskId)
    {
        if (_sources.TryRemove(taskId, out var cts))
            cts.Dispose();
    }
}
