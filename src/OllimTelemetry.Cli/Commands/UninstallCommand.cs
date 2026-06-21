using OllimTelemetry.Core;
using OllimTelemetry.Core.Hook;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class UninstallCommand
{
    public static Task<int> RunAsync()
    {
        var confirmed = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[red]This will remove the hook, config, and all local data. Continue?[/]")
                .AddChoices("No", "Yes")) == "Yes";

        if (!confirmed)
        {
            AnsiConsole.MarkupLine("[dim]Uninstall cancelled.[/]");
            return Task.FromResult(0);
        }

        var binaryPath  = Environment.ProcessPath ?? "ollim";
        var hookCommand = $"{binaryPath} hook";
        var (_, hookError) = ClaudeHookManager.Uninstall(hookCommand);
        if (hookError is not null)
            AnsiConsole.MarkupLine($"[yellow]![/] Hook removal failed: {hookError}");

        foreach (var dir in new[] { OllimPaths.ConfigDir, OllimPaths.DataDir, OllimPaths.LegacyDir })
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }

        AnsiConsole.MarkupLine(hookError is null ? "[green]✓[/] Hook removed." : "[yellow]![/] Hook may still be registered — remove it manually from ~/.claude/settings.json");
        AnsiConsole.MarkupLine("[green]✓[/] All local data removed.");
        AnsiConsole.MarkupLine("[dim]The ollim binary itself was not removed — delete it manually if desired.[/]");

        return Task.FromResult(0);
    }
}
