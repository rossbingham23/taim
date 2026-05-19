using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Taim.Core.Actions;
using Taim.Core.Agents;
using Taim.Core.Approvals;
using Taim.Core.Memory;
using Taim.Core.Notifications;

namespace Taim.Agents.Shared;

/// <summary>
/// Runs the multi-turn LLM tool-use loop for a single action.
/// Not registered in DI — instantiated by ActionExecutor with resolved dependencies.
/// </summary>
public sealed class ActionWorker(
    IActionService actionService,
    IApprovalService approvalService,
    INotificationService notifications,
    IAgentRegistry agentRegistry,
    IChatHistoryProvider chatHistory,
    ILogger logger)
{
    private const int MaxTurns = 15;

    public async Task ExecuteAsync(
        Guid tenantId, Guid taskId,
        ActionRecord action, AgentDefinition agent,
        IChatClient chatClient, IReadOnlyList<AITool> tools,
        CancellationToken ct = default)
    {
        try
        {
            await RunLoopAsync(tenantId, taskId, action, agent, chatClient, tools, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ActionWorker unhandled error for action {ActionId}", action.Id);
            var errDesc = (action.Description ?? string.Empty) + $"\n\nError: {ex.Message}";
            try
            {
                await actionService.UpdateAsync(tenantId, action.Id,
                    new UpdateActionRequest(Status: "blocked", Description: errDesc), ct);
                await PushActionUpdated(tenantId, taskId, action.Id, agent.Id, "blocked", ct);
                await agentRegistry.UpdateStatusAsync(tenantId, agent.Id, AgentStatus.Idle, ct);
                await PushAgentStatus(tenantId, taskId, agent, "idle", ct);
            }
            catch (Exception cleanupEx)
            {
                logger.LogError(cleanupEx, "ActionWorker cleanup failed for action {ActionId}", action.Id);
            }
        }
    }

    private async Task RunLoopAsync(
        Guid tenantId, Guid taskId,
        ActionRecord action, AgentDefinition agent,
        IChatClient chatClient, IReadOnlyList<AITool> tools,
        CancellationToken ct)
    {
        // Step 1: transition to in_progress
        await actionService.UpdateAsync(tenantId, action.Id,
            new UpdateActionRequest(Status: "in_progress"), ct);
        await PushActionUpdated(tenantId, taskId, action.Id, agent.Id, "in_progress", ct);

        // Step 2: set up synthetic complete_task tool
        var completionSignal = new TaskCompletionSource<(string Status, string Summary)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var completeTaskTool = AIFunctionFactory.Create(
            (string status, string summary) =>
            {
                completionSignal.TrySetResult((status, summary));
                return "ok";
            },
            "complete_task",
            "Call when the task is finished or you cannot proceed. " +
            "status='done' if complete, 'blocked' if you cannot continue. " +
            "summary: brief description of what was done or why blocked.",
            null);

        var allTools = new List<AITool>(tools) { completeTaskTool };
        var options = new ChatOptions { Tools = allTools };

        // Step 3: load or initialise conversation history
        var sessionKey = $"action:{action.Id}";
        var messages = new List<ChatMessage>(
            await chatHistory.LoadAsync(tenantId, agent.Id, sessionKey, ct: ct));

        if (messages.Count == 0)
        {
            var systemMsg = new ChatMessage(ChatRole.System, BuildWorkSystemPrompt(agent, action));
            var userContent = $"Execute this action: {action.Title}";
            if (!string.IsNullOrWhiteSpace(action.Description))
                userContent += $"\n\n{action.Description}";
            var userMsg = new ChatMessage(ChatRole.User, userContent);
            messages.Add(systemMsg);
            messages.Add(userMsg);
            await chatHistory.SaveAsync(tenantId, agent.Id, sessionKey, messages, ct);
        }

        // Step 4: tool-use loop
        string finalStatus = "done";
        string finalSummary = string.Empty;

        for (int turn = 0; turn < MaxTurns; turn++)
        {
            var response = await chatClient.GetResponseAsync(messages, options, ct);

            // Add response messages to conversation and save to history
            var responseMessages = response.Messages.ToList();
            messages.AddRange(responseMessages);
            await chatHistory.SaveAsync(tenantId, agent.Id, sessionKey, responseMessages, ct);

            // No tool calls → implicit done
            var toolCalls = responseMessages
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .ToList();

            if (toolCalls.Count == 0)
            {
                finalStatus = "done";
                finalSummary = response.Text ?? string.Empty;
                break;
            }

            // Process each tool call
            var resultContents = new List<AIContent>();
            bool blocked = false;

            foreach (var tc in toolCalls)
            {
                if (tc.Name == "complete_task")
                {
                    var fn = allTools.OfType<AIFunction>()
                        .FirstOrDefault(t => t.Name == "complete_task");
                    if (fn is not null)
                        await fn.InvokeAsync(
                            new AIFunctionArguments(tc.Arguments ?? new Dictionary<string, object>()),
                            ct);
                    resultContents.Add(new FunctionResultContent(tc.CallId, "ok"));
                    break; // TCS set; detected below
                }

                var toolArgs = (tc.Arguments ?? new Dictionary<string, object>())
                    .ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

                var approval = await approvalService.CheckLongLivedAsync(
                    tenantId, agent.Id, tc.Name, toolArgs, ct);

                if (approval is null)
                {
                    // No long-lived approval — request one and block
                    await approvalService.CreateAsync(
                        tenantId, agent.Id, tc.Name, toolArgs,
                        $"{agent.Name} wants to call {tc.Name}", ct: ct);

                    await actionService.UpdateAsync(tenantId, action.Id,
                        new UpdateActionRequest(Status: "blocked"), ct);
                    await PushActionUpdated(tenantId, taskId, action.Id, agent.Id, "blocked", ct);

                    await agentRegistry.UpdateStatusAsync(tenantId, agent.Id, AgentStatus.WaitingApproval, ct);
                    await PushAgentStatus(tenantId, taskId, agent, "waitingApproval", ct);

                    blocked = true;
                    break;
                }

                if (!approval.Approved)
                {
                    resultContents.Add(new FunctionResultContent(tc.CallId,
                        $"Tool {tc.Name} was denied by user."));
                    continue;
                }

                // Approved — execute the tool
                var tool = allTools.OfType<AIFunction>()
                    .FirstOrDefault(t => t.Name == tc.Name);

                object? result;
                if (tool is null)
                {
                    result = $"Unknown tool: {tc.Name}";
                }
                else
                {
                    try
                    {
                        result = await tool.InvokeAsync(
                            new AIFunctionArguments(tc.Arguments ?? new Dictionary<string, object>()),
                            ct);
                    }
                    catch (Exception ex)
                    {
                        result = $"Tool error: {ex.Message}";
                        logger.LogWarning(ex, "Tool {ToolName} threw during invocation", tc.Name);
                    }
                }

                resultContents.Add(new FunctionResultContent(tc.CallId, result?.ToString()));
            }

            if (blocked) return;

            if (resultContents.Count > 0)
            {
                var toolResultMsg = new ChatMessage(ChatRole.Tool, resultContents);
                messages.Add(toolResultMsg);
                await chatHistory.SaveAsync(tenantId, agent.Id, sessionKey,
                    new List<ChatMessage> { toolResultMsg }, ct);
            }

            // Check if complete_task was called
            if (completionSignal.Task.IsCompleted)
            {
                (finalStatus, finalSummary) = await completionSignal.Task;
                break;
            }

            // Last turn without completion → blocked
            if (turn == MaxTurns - 1)
            {
                finalStatus = "blocked";
                finalSummary = "Max turns reached without completing the task.";
            }
        }

        // Step 5: finalise
        var updatedDesc = (action.Description ?? string.Empty).TrimEnd();
        if (!string.IsNullOrEmpty(finalSummary))
            updatedDesc = (updatedDesc.Length > 0 ? updatedDesc + "\n\n" : string.Empty)
                + $"Result: {finalSummary}";

        await actionService.UpdateAsync(tenantId, action.Id,
            new UpdateActionRequest(Status: finalStatus, Description: updatedDesc), ct);
        await PushActionUpdated(tenantId, taskId, action.Id, agent.Id, finalStatus, ct);

        await agentRegistry.UpdateStatusAsync(tenantId, agent.Id, AgentStatus.Idle, ct);
        await PushAgentStatus(tenantId, taskId, agent, "idle", ct);
    }

    private Task PushActionUpdated(
        Guid tenantId, Guid taskId, Guid actionId, Guid agentId, string status, CancellationToken ct) =>
        notifications.NotifyAsync(tenantId, NotificationKind.ActionUpdated,
            $"Action {status}", string.Empty,
            new Dictionary<string, object?>
            {
                ["taskId"]   = taskId.ToString(),
                ["agentId"]  = agentId.ToString(),
                ["actionId"] = actionId.ToString(),
                ["status"]   = status,
            }, ct);

    private Task PushAgentStatus(
        Guid tenantId, Guid taskId, AgentDefinition agent, string status, CancellationToken ct) =>
        notifications.NotifyAsync(tenantId, NotificationKind.AgentStatusChanged,
            $"{agent.Name} {status}", string.Empty,
            new Dictionary<string, object?>
            {
                ["taskId"]  = taskId.ToString(),
                ["agentId"] = agent.Id.ToString(),
                ["status"]  = status,
            }, ct);

    private static string BuildWorkSystemPrompt(AgentDefinition agent, ActionRecord action)
    {
        var desc = string.IsNullOrWhiteSpace(action.Description) ? string.Empty : action.Description;
        return $"""
            You are {agent.Name}, {agent.Role}.
            Charter: {agent.Charter}

            You have been assigned the following task:
            "{action.Title}"
            {desc}

            Use the tools available to you to complete this task.
            When you are finished — or if you cannot proceed — call complete_task with:
              status: "done" if you completed the task
              status: "blocked" if you cannot proceed (explain why in summary)
              summary: a brief description of what was done or why you are blocked

            Be concrete. Prefer action over deliberation. Do not ask clarifying questions — make reasonable assumptions.
            """;
    }
}
