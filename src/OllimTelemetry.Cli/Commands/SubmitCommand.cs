using OllimTelemetry.Cli.Update;
using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Queue;
using OllimTelemetry.Core.Sync;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class SubmitCommand
{
    /// <summary>Manually flush all pending sessions to the backend.</summary>
    public static async Task<int> RunAsync()
    {
        var configManager = new ConfigManager();
        var config        = configManager.LoadOrCreate();

        if (!config.ShareGlobal)
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] Sharing is disabled. Enable it with [bold]ollim config[/].");
            return 1;
        }

        var isDev = Environment.GetEnvironmentVariable("OLLIM_ENV") == "dev";
        if (config.SessionToken is null && !isDev)
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] Not authenticated — run [bold]ollim login[/] to sign in.");
            return 1;
        }

        using var queue = new SyncQueue();
        var pending     = queue.CountPending();

        if (pending == 0)
        {
            AnsiConsole.MarkupLine("[dim]No pending sessions to submit.[/]");
            return 0;
        }

        // Reset backoff timers so all batches are eligible immediately.
        queue.ResetRetryTimers();

        AnsiConsole.MarkupLine($"Submitting [bold]{pending}[/] pending batch(es)...");

        using var http  = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var syncService = new SyncService(configManager, queue, http, UpdateChecker.CurrentVersion ?? "unknown");
        await syncService.FlushOnceAsync();

        var remaining = queue.CountPending();
        var sent      = pending - remaining;

        if (sent > 0)
            AnsiConsole.MarkupLine($"[green]✓[/] {sent} batch(es) submitted.");

        if (remaining > 0)
            AnsiConsole.MarkupLine($"[yellow]⚠[/] {remaining} batch(es) could not be submitted and will retry automatically.");

        return remaining == pending ? 1 : 0;
    }
}
