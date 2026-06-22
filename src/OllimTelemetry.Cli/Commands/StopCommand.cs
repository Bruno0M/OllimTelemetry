using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Hook;
using OllimTelemetry.Core.Queue;
using OllimTelemetry.Core.Sync;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class StopCommand
{
    /// <summary>Unregister the Claude Code hook and stop tracking token usage.</summary>
    public static async Task<int> RunAsync()
    {
        var configManager = new ConfigManager();
        var binaryPath    = Environment.ProcessPath ?? "ollim";
        var hookCommand   = $"{binaryPath} hook";

        // Attempt to flush before removing the hook so pending batches are not abandoned.
        using (var queue = new SyncQueue())
        {
            if (queue.CountPending() > 0)
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var syncService = new SyncService(configManager, queue, http);
                await syncService.FlushOnceAsync();

                var remaining = queue.CountPending();
                if (remaining > 0)
                    AnsiConsole.MarkupLine($"  [yellow]⚠[/] {remaining} batch(es) could not be synced and will be lost.");
            }
        }

        var (removed, error) = ClaudeHookManager.Uninstall(hookCommand);

        if (error is not null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] {error}");
            return 1;
        }

        if (removed)
            AnsiConsole.MarkupLine("[green]✓[/] Hook removed from ~/.claude/settings.json");
        else
            AnsiConsole.MarkupLine("[dim]Hook was not installed — nothing to remove.[/]");

        return 0;
    }
}
