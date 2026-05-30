using OllimTelemetry.Core.Daemon;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OllimTelemetry.Cli.Commands;

public sealed class StopCommand : AsyncCommand
{
    public override Task<int> ExecuteAsync(CommandContext context)
    {
        var daemonManager = new DaemonManager();
        var (success, message) = daemonManager.Unregister();

        if (success)
            AnsiConsole.MarkupLine("[green]✓[/] Daemon stopped.");
        else
            AnsiConsole.MarkupLine($"[red]✗[/] {message}");

        return Task.FromResult(success ? 0 : 1);
    }
}
