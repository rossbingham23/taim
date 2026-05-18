using System.Net;

namespace Taim.E2ETests;

[Collection("Api")]
public class ApprovalTests(ApiFixture fixture)
{
    [Fact]
    public async Task ListApprovals_ReturnsArray()
    {
        var res = await fixture.Client.GetAsync("/api/approvals");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task DecideApproval_WithUnknownId_Returns404()
    {
        var res = await fixture.Client.PostAsJsonAsync(
            $"/api/approvals/{Guid.NewGuid()}/decide",
            new { approved = true, scope = "once" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
