using OllimTelemetry.Cli.Onboarding;
using OllimTelemetry.Cli.Update;
using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Hook;
using OllimTelemetry.Core.Ingestion;
using OllimTelemetry.Core.Parsing;
using OllimTelemetry.Core.Queue;
using OllimTelemetry.Core.Sync;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class StartCommand
{
    /// <summary>Register the Claude Code hook and start tracking token usage.</summary>
    public static async Task<int> RunAsync()
    {
        var configManager = new ConfigManager();
        var binaryPath    = Environment.ProcessPath ?? "ollim";
        var hookCommand   = BuildHookCommand(binaryPath);

        using var queue = new SyncQueue();

        if (!File.Exists(configManager.ConfigFilePath))
        {
            var flow = new OnboardingFlow(configManager);
            flow.Run(hookCommand);
            await BackfillAndSyncAsync(configManager, queue);
            return 0;
        }

        var (changed, error) = ClaudeHookManager.Install(hookCommand);
        if (error is not null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Hook install failed: {error}");
            return 1;
        }

        if (changed)
            AnsiConsole.MarkupLine("[green]✓[/] Hook registered in ~/.claude/settings.json");
        else
            AnsiConsole.MarkupLine("[dim]Hook already registered.[/]");

        // First run: no offsets stored yet → backfill historical sessions.
        if (!queue.HasAnyOffsets())
            await BackfillAndSyncAsync(configManager, queue);

        return 0;
    }

    private static string BuildHookCommand(string binaryPath)
    {
        // In dev mode, propagate the current process's isolation env vars into the
        // registered hook command so Claude Code fires the hook with the same context.
        static string? Env(string key) => Environment.GetEnvironmentVariable(key);

        var ollimEnv  = Env("OLLIM_ENV");
        if (ollimEnv != "dev")
            return $"{binaryPath} hook";

        static string ShellQuote(string val) => "'" + val.Replace("'", "'\\''") + "'";

        var parts = new System.Text.StringBuilder();
        foreach (var key in new[] { "OLLIM_ENV", "OLLIM_BACKEND_URL", "XDG_CONFIG_HOME", "XDG_DATA_HOME" })
        {
            var val = Env(key);
            if (!string.IsNullOrEmpty(val))
                parts.Append($"{key}={ShellQuote(val)} ");
        }
        parts.Append($"{binaryPath} hook");
        return parts.ToString();
    }

    private static async Task BackfillAndSyncAsync(ConfigManager configManager, SyncQueue queue)
    {
        var config = configManager.LoadOrCreate();
        var parser = new LogParser();

        var count = LogIngester.BackfillAll(config.Agent, parser, queue);
        if (count > 0)
        {
            AnsiConsole.MarkupLine($"[dim]Backfilled {count} session(s) from existing logs.[/]");
            using var http  = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var syncService  = new SyncService(configManager, queue, http,
                UpdateChecker.CurrentVersion ?? "unknown");
            await syncService.FlushOnceAsync();
        }
    }
}
