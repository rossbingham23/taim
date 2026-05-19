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

    [Fact]
    public async Task ApprovalHistory_WithValidTaskId_Returns200Array()
    {
        // Create a task first so we have a valid taskId
        var taskRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "History test task", budgetUsd = 1 });
        Assert.True(taskRes.IsSuccessStatusCode);
        var taskBody = await taskRes.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = taskBody.GetProperty("id").GetString()!;

        var res = await fixture.Client.GetAsync($"/api/approvals/history?taskId={taskId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task ApprovalHistory_WithoutTaskId_Returns400()
    {
        var res = await fixture.Client.GetAsync("/api/approvals/history");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
