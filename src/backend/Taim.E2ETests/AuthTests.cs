using System.Net;

namespace Taim.E2ETests;

[Collection("Api")]
public class AuthTests(ApiFixture fixture)
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        // The fixture already logged in successfully — if we get here, login works.
        Assert.False(string.IsNullOrEmpty(fixture.Token));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        using var anon = new HttpClient { BaseAddress = new Uri(ApiFixture.BaseUrl) };
        var res = await anon.PostAsJsonAsync("/api/auth/login",
            new { email = ApiFixture.Email, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        using var anon = new HttpClient { BaseAddress = new Uri(ApiFixture.BaseUrl) };
        var res = await anon.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@example.com", password = "irrelevant" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        using var anon = new HttpClient { BaseAddress = new Uri(ApiFixture.BaseUrl) };
        var res = await anon.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
