using OllimTelemetry.Core.Hook;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class StopCommand
{
    public static Task<int> RunAsync()
    {
        const string hookCommand = "ollim hook";

        var (removed, error) = ClaudeHookManager.Uninstall(hookCommand);

        if (error is not null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] {error}");
            return Task.FromResult(1);
        }

        if (removed)
            AnsiConsole.MarkupLine("[green]✓[/] Hook removed from ~/.claude/settings.json");
        else
            AnsiConsole.MarkupLine("[dim]Hook was not installed — nothing to remove.[/]");

        return Task.FromResult(0);
    }
}
