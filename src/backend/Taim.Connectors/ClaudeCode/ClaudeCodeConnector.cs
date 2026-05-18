using Microsoft.Extensions.AI;
using Taim.Connectors.Sdk;

namespace Taim.Connectors.ClaudeCode;

/// <summary>
/// Connector that exposes Claude Code CLI as a tool for developer agents.
/// Developer agents can use this to write, edit, test, and commit code.
///
/// Preferred over raw API coding: the CLI handles file system operations,
/// git commands, test execution, and interactive approval natively.
///
/// Requires: `claude` CLI installed and authenticated on the host.
/// </summary>
public sealed class ClaudeCodeConnector : IConnector
{
    public string ConnectorId => "claude-code";
    public string DisplayName => "Claude Code";
    public string Description => "Write, edit, test, and commit code using the Claude Code CLI.";

    private IReadOnlyList<AITool>? _tools;

    public Task StartAsync(CancellationToken ct = default)
    {
        _tools =
        [
            AIFunctionFactory.Create(RunClaudeCode, "claude_code",
                "Run the Claude Code CLI with a prompt to write, edit, or test code in a given workspace directory."),
        ];
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken ct = default) =>
        Task.FromResult(_tools ?? throw new InvalidOperationException("ClaudeCodeConnector not started."));

    private static async Task<string> RunClaudeCode(
        string prompt,
        string workingDirectory,
        bool nonInteractive = true)
    {
        var args = nonInteractive
            ? $"--print \"{EscapeArg(prompt)}\""
            : $"\"{EscapeArg(prompt)}\"";

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "claude",
                Arguments = args,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            return $"Claude Code exited with code {process.ExitCode}:\n{error}";

        return output;
    }

    private static string EscapeArg(string arg) => arg.Replace("\"", "\\\"");
}
