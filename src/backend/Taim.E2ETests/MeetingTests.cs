using System.Net;

namespace Taim.E2ETests;

[Collection("Api")]
public class MeetingTests(ApiFixture fixture)
{
    [Fact]
    public async Task ListMeetings_WithTaskId_Returns200WithArray()
    {
        // Submit a task to get a valid taskId (meetings may or may not exist)
        var submitRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "Meeting smoke test task", budgetUsd = 1.0 });
        var submitBody = await submitRes.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = submitBody.GetProperty("id").GetString()!;

        var res = await fixture.Client.GetAsync($"/api/meetings?taskId={taskId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task ListMeetings_WithoutTaskId_Returns400()
    {
        var res = await fixture.Client.GetAsync("/api/meetings");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetMeeting_WithUnknownId_Returns404()
    {
        var res = await fixture.Client.GetAsync($"/api/meetings/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
