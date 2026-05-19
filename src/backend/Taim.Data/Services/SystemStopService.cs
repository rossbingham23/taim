using StackExchange.Redis;
using Taim.Core.System;

namespace Taim.Data.Services;

public sealed class SystemStopService(IConnectionMultiplexer redis) : ISystemStopService
{
    private const string Key = "taim:system:stop";
    private IDatabase Db => redis.GetDatabase();

    public async Task<bool> IsStoppedAsync(CancellationToken ct = default)
        => await Db.KeyExistsAsync(Key);

    public async Task StopAsync(CancellationToken ct = default)
        => await Db.StringSetAsync(Key, "1");

    public async Task ResumeAsync(CancellationToken ct = default)
        => await Db.KeyDeleteAsync(Key);
}
