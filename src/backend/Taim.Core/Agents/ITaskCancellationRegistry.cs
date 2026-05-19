namespace Taim.Core.Agents;

public interface ITaskCancellationRegistry
{
    CancellationToken Register(Guid taskId);
    void Cancel(Guid taskId);
    void Unregister(Guid taskId);
}
