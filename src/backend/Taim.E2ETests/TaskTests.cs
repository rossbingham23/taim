using System.Net;

namespace Taim.E2ETests;

[Collection("Api")]
public class TaskTests(ApiFixture fixture)
{
    [Fact]
    public async Task SubmitTask_Returns202WithTaskId()
    {
        var res = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "E2E test goal: validate task submission", budgetUsd = 10.0 });

        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString();
        Assert.False(string.IsNullOrEmpty(id));
        Assert.True(Guid.TryParse(id, out _));
    }

    [Fact]
    public async Task ListTasks_ReturnsArray()
    {
        // Ensure at least one task exists
        await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "E2E list-tasks probe", budgetUsd = 1.0 });

        var res = await fixture.Client.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.True(body.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetTask_ReturnsTaskAndGraph()
    {
        // Submit a task then fetch its detail
        var submitRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "E2E get-task detail probe", budgetUsd = 1.0 });
        Assert.Equal(HttpStatusCode.Accepted, submitRes.StatusCode);

        var submitBody = await submitRes.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = submitBody.GetProperty("id").GetString()!;

        var res = await fixture.Client.GetAsync($"/api/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("task", out var task));
        Assert.True(body.TryGetProperty("graph", out var graph));

        Assert.Equal(taskId, task.GetProperty("id").GetString());
        Assert.True(graph.TryGetProperty("nodes", out _));
        Assert.True(graph.TryGetProperty("edges", out _));
    }

    [Fact]
    public async Task GetTask_WithUnknownId_Returns404()
    {
        var res = await fixture.Client.GetAsync($"/api/tasks/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task SubmittedTask_HasBootstrappingOrFailedStatus()
    {
        var submitRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "E2E status probe", budgetUsd = 1.0 });
        var submitBody = await submitRes.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = submitBody.GetProperty("id").GetString()!;

        // Poll until status moves away from "bootstrapping" or a few seconds pass
        string status = "pending";
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            var r = await fixture.Client.GetAsync($"/api/tasks/{taskId}");
            var b = await r.Content.ReadFromJsonAsync<JsonElement>();
            status = b.GetProperty("task").GetProperty("status").GetString()!;
            if (status != "pending") break;
        }

        // Without an LLM key the bootstrap will fail; with one it should bootstrap/active.
        // Either way, status must have advanced from "pending".
        Assert.NotEqual("pending", status);
    }
}
