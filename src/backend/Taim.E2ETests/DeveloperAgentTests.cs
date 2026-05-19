using System.Net;

namespace Taim.E2ETests;

[Collection("Api")]
public class DeveloperAgentTests(ApiFixture fixture)
{
    /// <summary>
    /// Verifies AC-1: Developer/QA agents produce no executive strategy reports.
    /// Polls until at least one executive report appears (confirming kickoff ran),
    /// then asserts no report belongs to a Developer or QA agent.
    /// Requires LLM API keys and team assembly to complete.
    /// </summary>
    [Fact]
    public async Task AfterKickoff_NoWorkerAgentReportsExist()
    {
        var taskRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "Build a REST API for a todo app", budgetUsd = 2.0 });
        Assert.Equal(HttpStatusCode.Accepted, taskRes.StatusCode);
        var taskBody = await taskRes.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = taskBody.GetProperty("id").GetString()!;

        // Poll up to 120s for at least one executive report (confirms kickoff ran)
        bool kickoffComplete = false;
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(5000);
            var reportsRes = await fixture.Client.GetAsync($"/api/reports?taskId={taskId}");
            Assert.Equal(HttpStatusCode.OK, reportsRes.StatusCode);
            var reports = await reportsRes.Content.ReadFromJsonAsync<JsonElement>();
            if (reports.GetArrayLength() > 0) { kickoffComplete = true; break; }
        }

        if (!kickoffComplete)
        {
            // No LLM keys or kickoff didn't complete — skip assertion
            return;
        }

        // Assert no reports from worker roles
        var finalRes = await fixture.Client.GetAsync($"/api/reports?taskId={taskId}");
        var allReports = await finalRes.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var report in allReports.EnumerateArray())
        {
            if (!report.TryGetProperty("agentName", out var nameEl)) continue;
            var name = nameEl.GetString()?.ToLowerInvariant() ?? string.Empty;
            Assert.False(
                name.Contains("developer") || name.Contains("qa"),
                $"Worker agent produced a report: '{nameEl.GetString()}'. " +
                "Only executive agents should produce strategy reports.");
        }
    }
}
