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
    public static async Task<int> RunAsync()
    {
        var configManager = new ConfigManager();
        const string hookCommand = "ollim hook";

        if (!File.Exists(configManager.ConfigFilePath))
        {
            var flow = new OnboardingFlow(configManager);
            flow.Run(hookCommand);
            await BackfillAndSyncAsync(configManager);
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
        using var queue = new SyncQueue();
        if (!queue.HasAnyOffsets())
            await BackfillAndSyncAsync(configManager, queue);

        return 0;
    }

    private static async Task BackfillAndSyncAsync(ConfigManager configManager, SyncQueue? existingQueue = null)
    {
        var config = configManager.LoadOrCreate();
        var parser = new LogParser();

        SyncQueue? owned = existingQueue is null ? new SyncQueue() : null;
        var queue = existingQueue ?? owned!;

        try
        {
            var count = LogIngester.BackfillAll(config.Agent, parser, queue);
            if (count > 0)
            {
                AnsiConsole.MarkupLine($"[dim]Backfilled {count} session(s) from existing logs.[/]");
                using var http  = new System.Net.Http.HttpClient();
                var syncService  = new SyncService(configManager, queue, http,
                    UpdateChecker.CurrentVersion ?? "unknown");
                await syncService.FlushOnceAsync();
            }
        }
        finally
        {
            owned?.Dispose();
        }
    }
}
