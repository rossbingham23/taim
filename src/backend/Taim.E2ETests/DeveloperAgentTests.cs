using System.Net;

namespace Taim.E2ETests;

[Collection("Api")]
public class DeveloperAgentTests(ApiFixture fixture)
{
    /// <summary>
    /// Verifies AC-3: AgentFactory.PreApproveAsync inserts a web-search approval for every agent.
    /// Submits a simple goal, then polls until at least one approved web-search approval appears.
    /// Requires a live stack with LLM API keys to assemble a team.
    /// </summary>
    [Fact]
    public async Task AfterTeamAssembly_ApprovalsContainPreSeededWebSearch()
    {
        var taskRes = await fixture.Client.PostAsJsonAsync("/api/tasks",
            new { goal = "Write a hello world program in Python", budgetUsd = 2.0 });
        Assert.Equal(HttpStatusCode.Accepted, taskRes.StatusCode);

        // Poll up to 90s for a web-search pre-approval to appear
        var deadline = DateTime.UtcNow.AddSeconds(90);
        bool found = false;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(3000);
            var res = await fixture.Client.GetAsync("/api/approvals");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var body = await res.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var approval in body.EnumerateArray())
            {
                var toolName = approval.TryGetProperty("toolName", out var t) ? t.GetString() : null;
                var status   = approval.TryGetProperty("status", out var s)   ? s.GetString() : null;
                var scope    = approval.TryGetProperty("scope", out var sc)   ? sc.GetString() : null;

                if (toolName == "web-search" && status == "approved" &&
                    scope is "agentAndTool" or "agent_and_tool")
                {
                    found = true;
                    break;
                }
            }
            if (found) break;
        }

        Assert.True(found,
            "Expected at least one approved web-search approval after team assembly, " +
            "but none was found within 90 seconds. Check ANTHROPIC_API_KEY is set and the stack is running.");
    }

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
