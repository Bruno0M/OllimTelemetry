using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Daemon;
using OllimTelemetry.Core.Queue;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class StatusCommand
{
    public static Task<int> RunAsync()
    {
        var configManager = new ConfigManager();
        var daemonManager = new DaemonManager();
        var config        = configManager.LoadOrCreate();

        var running = daemonManager.IsRunning();

        var isDev = Environment.GetEnvironmentVariable("OLLIM_ENV") == "dev";
        var title = isDev ? "[bold]Ollim Telemetry Status[/] [yellow][[dev]][/]" : "[bold]Ollim Telemetry Status[/]";

        AnsiConsole.Write(new Rule(title).RuleStyle("grey"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"  Daemon:       {(running ? "[green]running[/]" : "[red]stopped[/]")}");
        AnsiConsole.MarkupLine($"  Sharing:      {(config.ShareGlobal ? "[green]enabled[/]" : "[yellow]disabled[/]")}");
        AnsiConsole.MarkupLine($"  Sync every:   [dim]{config.SyncIntervalMinutes} minutes[/]");
        AnsiConsole.MarkupLine($"  Last sync:    [dim]{config.LastSyncAt ?? "never"}[/]");

        AnsiConsole.WriteLine();

        using var queue = new SyncQueue();
        var pending = queue.Dequeue(1000);

        AnsiConsole.MarkupLine($"  Pending batches: [dim]{pending.Count}[/]");

        return Task.FromResult(0);
    }
}
