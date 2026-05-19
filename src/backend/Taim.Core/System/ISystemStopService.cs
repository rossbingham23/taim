namespace Taim.Core.System;

public interface ISystemStopService
{
    Task<bool> IsStoppedAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task ResumeAsync(CancellationToken ct = default);
}
