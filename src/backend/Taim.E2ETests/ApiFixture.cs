using System.Net.Http.Headers;

namespace Taim.E2ETests;

/// <summary>
/// Shared fixture: logs in once, provides an authenticated HttpClient.
/// Set TAIM_API_URL env var to override (default: http://localhost:5000).
/// Set TAIM_EMAIL / TAIM_PASSWORD to override credentials.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("TAIM_API_URL") ?? "http://localhost:5000";

    public static readonly string Email =
        Environment.GetEnvironmentVariable("TAIM_EMAIL") ?? "admin@taim.local";

    public static readonly string Password =
        Environment.GetEnvironmentVariable("TAIM_PASSWORD") ?? "taim-admin";

    public HttpClient Client { get; } = new() { BaseAddress = new Uri(BaseUrl) };
    public string Token { get; private set; } = "";

    public async Task InitializeAsync()
    {
        // Wait for API to be ready (up to 60 s after docker compose up)
        await WaitForApiAsync(TimeSpan.FromSeconds(60));

        var res = await Client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password });
        Assert.True(res.IsSuccessStatusCode,
            $"Login failed with {(int)res.StatusCode}. Is the seed user present in the DB?");

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Token = body.GetProperty("token").GetString()!;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    private async Task WaitForApiAsync(TimeSpan timeout)
    {
        using var probe = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var r = await probe.GetAsync("/health");
                if (r.IsSuccessStatusCode) return;
            }
            catch { /* not ready yet */ }
            await Task.Delay(1000);
        }
        throw new Exception($"API at {BaseUrl} did not become healthy within {timeout.TotalSeconds}s.");
    }
}

[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiFixture> { }
