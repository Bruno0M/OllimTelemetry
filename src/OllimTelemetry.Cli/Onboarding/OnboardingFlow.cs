using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Hook;
using Spectre.Console;

namespace OllimTelemetry.Cli.Onboarding;

public sealed class OnboardingFlow
{
    private readonly ConfigManager _configManager;

    public OnboardingFlow(ConfigManager configManager)
    {
        _configManager = configManager;
    }

    public void Run(string claudeHookCommand, string codexHookCommand)
    {
        AnsiConsole.Write(new Rule("[bold blue]Welcome to Ollim Telemetry[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Ollim reads your AI coding agent token usage and can display");
        AnsiConsole.MarkupLine("it on a global leaderboard — [italic]anonymously[/], with your permission.");
        AnsiConsole.WriteLine();

        var shareGlobal = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Share your token counts on the global leaderboard?[/]\n  [dim]Only counts are sent. Never message content.[/]")
                .AddChoices("No", "Yes")) == "Yes";

        AnsiConsole.WriteLine();

        var config = new AppConfig { ShareGlobal = shareGlobal };
        _configManager.Save(config);
        AnsiConsole.MarkupLine($"[green]✓[/] Config saved to [dim]{_configManager.ConfigFilePath}[/]");

        var (_, claudeError) = ClaudeHookManager.Install(claudeHookCommand);
        if (claudeError is null)
            AnsiConsole.MarkupLine("[green]✓[/] Hook registered in ~/.claude/settings.json");
        else
            AnsiConsole.MarkupLine($"[red]✗[/] Claude Code hook install failed: {claudeError}");

        if (CodexHookManager.IsCodexPresent())
        {
            var (_, codexError) = CodexHookManager.Install(codexHookCommand);
            if (codexError is null)
                AnsiConsole.MarkupLine("[green]✓[/] Hook registered in ~/.codex/hooks.json");
            else
                AnsiConsole.MarkupLine($"[yellow]⚠[/] Codex hook install failed: {codexError}");
        }

        if (!shareGlobal)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Run [bold]`ollim status`[/] to confirm hooks are active.");
        }
    }
}
