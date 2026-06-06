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

    public void Run(string hookCommand)
    {
        AnsiConsole.Write(new Rule("[bold blue]Welcome to Ollim Telemetry[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Ollim reads your Claude Code token usage and can display");
        AnsiConsole.MarkupLine("it on a global leaderboard — [italic]anonymously[/], with your permission.");
        AnsiConsole.WriteLine();

        var shareGlobal = AnsiConsole.Confirm(
            "[bold]Share your token counts on the global leaderboard?[/]\n  [dim]Only counts are sent. Never message content.[/]",
            defaultValue: false);

        AnsiConsole.WriteLine();

        var config = new AppConfig { ShareGlobal = shareGlobal };
        _configManager.Save(config);
        AnsiConsole.MarkupLine($"[green]✓[/] Config saved to [dim]{_configManager.ConfigFilePath}[/]");

        var (_, error) = ClaudeHookManager.Install(hookCommand);
        if (error is null)
            AnsiConsole.MarkupLine("[green]✓[/] Hook registered in ~/.claude/settings.json");
        else
            AnsiConsole.MarkupLine($"[red]✗[/] Hook install failed: {error}");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Run [bold]`ollim status`[/] to confirm the hook is active.");
    }
}
