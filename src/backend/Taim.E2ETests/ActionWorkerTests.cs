using System.Net;

namespace Taim.E2ETests;

[Collection("Api")]
public class ActionWorkerTests(ApiFixture fixture)
{
    [Fact]
    public async Task ExecuteAction_WithUnknownId_Returns404()
    {
        var res = await fixture.Client.PostAsJsonAsync(
            $"/api/actions/{Guid.NewGuid()}/execute", new { });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task ExecuteAction_WithOpenAction_Returns202()
    {
        // Create a task to get a taskId
        var taskRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "E2E execute-action probe", budgetUsd = 5.0 });
        Assert.Equal(HttpStatusCode.Accepted, taskRes.StatusCode);
        var taskBody = await taskRes.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = taskBody.GetProperty("id").GetString()!;

        // Create an open action for this task (no agent assigned — simulates a standalone action)
        var createRes = await fixture.Client.PostAsJsonAsync("/api/actions",
            new { taskId = Guid.Parse(taskId), title = "E2E test action for execute endpoint" });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var actionBody = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var actionId = actionBody.GetProperty("id").GetString()!;

        // Trigger execution — should return 202 (action has no agent, worker will log warning and stop)
        var execRes = await fixture.Client.PostAsJsonAsync(
            $"/api/actions/{actionId}/execute", new { });

        Assert.Equal(HttpStatusCode.Accepted, execRes.StatusCode);
        var execBody = await execRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Execution started", execBody.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ExecuteAction_WithDoneAction_Returns409()
    {
        // Create a task + action, update action to done, then try to execute
        var taskRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "E2E execute-done-action probe", budgetUsd = 5.0 });
        Assert.Equal(HttpStatusCode.Accepted, taskRes.StatusCode);
        var taskBody = await taskRes.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = taskBody.GetProperty("id").GetString()!;

        var createRes = await fixture.Client.PostAsJsonAsync("/api/actions",
            new { taskId = Guid.Parse(taskId), title = "E2E done action" });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var actionBody = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var actionId = actionBody.GetProperty("id").GetString()!;

        // Mark it done
        await fixture.Client.PatchAsJsonAsync($"/api/actions/{actionId}",
            new { status = "done" });

        // Execute should 409
        var execRes = await fixture.Client.PostAsJsonAsync(
            $"/api/actions/{actionId}/execute", new { });
        Assert.Equal(HttpStatusCode.Conflict, execRes.StatusCode);
    }

    [Fact]
    public async Task ListActions_ForNewTask_ReturnsArray()
    {
        var taskRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "E2E list-actions probe", budgetUsd = 1.0 });
        Assert.Equal(HttpStatusCode.Accepted, taskRes.StatusCode);
        var taskBody = await taskRes.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = taskBody.GetProperty("id").GetString()!;

        var res = await fixture.Client.GetAsync($"/api/actions?taskId={taskId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }
}
