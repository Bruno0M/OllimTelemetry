using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Daemon;
using OllimTelemetry.Core.Queue;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OllimTelemetry.Cli.Commands;

public sealed class StatusCommand : AsyncCommand
{
    public override Task<int> ExecuteAsync(CommandContext context)
    {
        var configManager = new ConfigManager();
        var daemonManager = new DaemonManager();
        var config        = configManager.LoadOrCreate();

        var running = daemonManager.IsRunning();

        AnsiConsole.Write(new Rule("[bold]Ollim Telemetry Status[/]").RuleStyle("grey"));
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
