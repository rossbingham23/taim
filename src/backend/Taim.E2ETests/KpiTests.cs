using System.Net;

namespace Taim.E2ETests;

[Collection("Api")]
public class KpiTests(ApiFixture fixture)
{
    [Fact]
    public async Task ListKpis_WithTaskId_ReturnsArray()
    {
        // Submit a task to have a valid taskId
        var submitRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "E2E KPI probe", budgetUsd = 1.0 });
        var submitBody = await submitRes.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = submitBody.GetProperty("id").GetString()!;

        var res = await fixture.Client.GetAsync($"/api/kpis?taskId={taskId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        // KPIs may be empty if bootstrap hasn't finished — that's fine
    }

    [Fact]
    public async Task GetKpi_WithUnknownId_Returns404()
    {
        var res = await fixture.Client.GetAsync($"/api/kpis/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task RecordKpiValue_WithUnknownId_Returns404()
    {
        var res = await fixture.Client.PostAsJsonAsync(
            $"/api/kpis/{Guid.NewGuid()}/values",
            new { value = "42", source = "e2e-test" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
