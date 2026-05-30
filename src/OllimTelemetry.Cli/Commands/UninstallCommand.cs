using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Daemon;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class UninstallCommand
{
    public static Task<int> RunAsync()
    {
        var confirmed = AnsiConsole.Confirm(
            "[red]This will remove the daemon, config, and all local data. Continue?[/]",
            defaultValue: false);

        if (!confirmed)
        {
            AnsiConsole.MarkupLine("[dim]Uninstall cancelled.[/]");
            return Task.FromResult(0);
        }

        var daemonManager = new DaemonManager();
        daemonManager.Unregister();

        var ollimDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollim");

        if (Directory.Exists(ollimDir))
            Directory.Delete(ollimDir, recursive: true);

        AnsiConsole.MarkupLine("[green]✓[/] Daemon stopped and unregistered.");
        AnsiConsole.MarkupLine("[green]✓[/] All local data removed.");
        AnsiConsole.MarkupLine("[dim]The ollim binary itself was not removed — delete it manually if desired.[/]");

        return Task.FromResult(0);
    }
}
