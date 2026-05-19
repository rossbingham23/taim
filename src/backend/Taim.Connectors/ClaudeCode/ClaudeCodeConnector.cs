using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Taim.Connectors.Sdk;

namespace Taim.Connectors.ClaudeCode;

public sealed class ClaudeCodeConnector(IConfiguration config) : IConnector
{
    private readonly string _workspaceRoot = config["Workspace:Root"] ?? "/app/workspaces";
    private IReadOnlyList<AITool>? _tools;

    public string ConnectorId => "claude-code";
    public string DisplayName => "Claude Code";
    public string Description => "Write, edit, test, and commit code using the Claude Code CLI.";

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

    private async Task<string> RunClaudeCode(
        string prompt,
        string? workingDirectory = null,
        bool nonInteractive = true)
    {
        var dir = workingDirectory ?? _workspaceRoot;
        Directory.CreateDirectory(dir);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "claude",
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        if (nonInteractive)
            psi.ArgumentList.Add("--print");
        psi.ArgumentList.Add(prompt);

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            return $"Claude Code exited with code {process.ExitCode}:\n{error}";

        return output;
    }

}
