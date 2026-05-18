namespace Taim.E2ETests;

[Collection("Api")]
public class HealthTests(ApiFixture fixture)
{
    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var res = await fixture.Client.GetAsync("/health");
        Assert.True(res.IsSuccessStatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", body.GetProperty("status").GetString());
    }
}
