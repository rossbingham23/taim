using System.Net;

namespace Taim.E2ETests;

[Collection("Api")]
public class SystemTests(ApiFixture fixture)
{
    [Fact]
    public async Task SystemStatus_ReturnsStoppedBool()
    {
        var res = await fixture.Client.GetAsync("/api/system/status");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("stopped", out var stopped));
        Assert.Equal(JsonValueKind.False, stopped.ValueKind);
    }

    [Fact]
    public async Task SystemStop_ThenResume_TogglesStopped()
    {
        var stopRes = await fixture.Client.PostAsync("/api/system/stop", null);
        Assert.Equal(HttpStatusCode.NoContent, stopRes.StatusCode);

        var statusRes = await fixture.Client.GetAsync("/api/system/status");
        var status = await statusRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.True, status.GetProperty("stopped").ValueKind);

        var resumeRes = await fixture.Client.PostAsync("/api/system/resume", null);
        Assert.Equal(HttpStatusCode.NoContent, resumeRes.StatusCode);

        var afterRes = await fixture.Client.GetAsync("/api/system/status");
        var after = await afterRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.False, after.GetProperty("stopped").ValueKind);
    }

    [Fact]
    public async Task TerminateTask_WithUnknownId_Returns404()
    {
        var res = await fixture.Client.PostAsync($"/api/tasks/{Guid.NewGuid()}/terminate", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task TerminateTask_WithActiveTask_Returns204()
    {
        var submitRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "E2E terminate probe", budgetUsd = 1.0 });
        Assert.Equal(HttpStatusCode.Accepted, submitRes.StatusCode);

        var submitBody = await submitRes.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = submitBody.GetProperty("id").GetString()!;

        // Terminate immediately (task may still be bootstrapping — that's fine)
        var terminateRes = await fixture.Client.PostAsync($"/api/tasks/{taskId}/terminate", null);
        Assert.Equal(HttpStatusCode.NoContent, terminateRes.StatusCode);

        // Verify status is now terminated
        var taskRes = await fixture.Client.GetAsync($"/api/tasks/{taskId}");
        var taskBody = await taskRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("terminated", taskBody.GetProperty("task").GetProperty("status").GetString());
    }
}
