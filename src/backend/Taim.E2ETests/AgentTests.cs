using System.Net;

namespace Taim.E2ETests;

[Collection("Api")]
public class AgentTests(ApiFixture fixture)
{
    [Fact]
    public async Task ListAgents_ReturnsArray()
    {
        var res = await fixture.Client.GetAsync("/api/agents");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task GetAgent_WithUnknownId_Returns404()
    {
        var res = await fixture.Client.GetAsync($"/api/agents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
