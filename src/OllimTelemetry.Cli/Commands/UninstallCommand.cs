using OllimTelemetry.Core;
using OllimTelemetry.Core.Hook;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class UninstallCommand
{
    /// <summary>Remove the hook, config, and all local data.</summary>
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

        var binaryPath       = Environment.ProcessPath ?? "ollim";
        var claudeHookCmd    = $"{binaryPath} hook";
        var codexHookCmd     = $"{binaryPath} hook --agent codex";

        var (_, claudeError) = ClaudeHookManager.Uninstall(claudeHookCmd);
        if (claudeError is not null)
            AnsiConsole.MarkupLine($"[yellow]![/] Claude Code hook removal failed: {claudeError}");

        var (_, codexError) = CodexHookManager.Uninstall(codexHookCmd);
        if (codexError is not null)
            AnsiConsole.MarkupLine($"[yellow]![/] Codex hook removal failed: {codexError}");

        foreach (var dir in new[] { OllimPaths.ConfigDir, OllimPaths.DataDir, OllimPaths.LegacyDir })
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }

        AnsiConsole.MarkupLine(claudeError is null ? "[green]✓[/] Claude Code hook removed." : "[yellow]![/] Claude Code hook may still be registered — remove it manually from ~/.claude/settings.json");
        AnsiConsole.MarkupLine(codexError is null ? "[green]✓[/] Codex hook removed." : "[yellow]![/] Codex hook may still be registered — remove it manually from ~/.codex/hooks.json");
        AnsiConsole.MarkupLine("[green]✓[/] All local data removed.");
        AnsiConsole.MarkupLine("[dim]The ollim binary itself was not removed — delete it manually if desired.[/]");

        return Task.FromResult(0);
    }
}
