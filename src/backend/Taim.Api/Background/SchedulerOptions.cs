namespace Taim.Api.Background;

public sealed class SchedulerOptions
{
    public int IntervalSeconds { get; set; } = 30;
    public bool Enabled { get; set; } = true;
}
