using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Hook;
using OllimTelemetry.Core.Queue;
using OllimTelemetry.Core.Sync;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class StopCommand
{
    /// <summary>Unregister hooks for all supported agents and stop tracking token usage.</summary>
    public static async Task<int> RunAsync()
    {
        var configManager = new ConfigManager();
        var binaryPath    = Environment.ProcessPath ?? "ollim";

        // Attempt to flush before removing hooks so pending batches are not abandoned.
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

        var (claudeRemoved, claudeError) = ClaudeHookManager.Uninstall($"{binaryPath} hook");
        if (claudeError is not null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] {claudeError}");
            return 1;
        }
        if (claudeRemoved)
            AnsiConsole.MarkupLine("[green]✓[/] Hook removed from ~/.claude/settings.json");
        else
            AnsiConsole.MarkupLine("[dim]Claude Code hook was not installed — nothing to remove.[/]");

        var (codexRemoved, codexError) = CodexHookManager.Uninstall($"{binaryPath} hook --agent codex");
        if (codexError is not null)
            AnsiConsole.MarkupLine($"[yellow]⚠[/] Codex hook removal failed: {codexError}");
        else if (codexRemoved)
            AnsiConsole.MarkupLine("[green]✓[/] Hook removed from ~/.codex/hooks.json");

        return 0;
    }
}
