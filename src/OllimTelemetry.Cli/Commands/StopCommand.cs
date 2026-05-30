using OllimTelemetry.Core.Daemon;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class StopCommand
{
    public static Task<int> RunAsync()
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
