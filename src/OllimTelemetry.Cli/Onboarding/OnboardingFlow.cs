using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Daemon;
using Spectre.Console;

namespace OllimTelemetry.Cli.Onboarding;

public sealed class OnboardingFlow
{
    private readonly ConfigManager _configManager;
    private readonly DaemonManager _daemonManager;

    public OnboardingFlow(ConfigManager configManager, DaemonManager daemonManager)
    {
        _configManager = configManager;
        _daemonManager = daemonManager;
    }

    public void Run(string binaryPath)
    {
        AnsiConsole.Write(new Rule("[bold blue]Welcome to Ollim Telemetry[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("This daemon reads your Claude Code token usage and can display");
        AnsiConsole.MarkupLine("it on a global leaderboard — [italic]anonymously[/], with your permission.");
        AnsiConsole.WriteLine();

        var shareGlobal = AnsiConsole.Confirm(
            "[bold]Share your token counts on the global leaderboard?[/]\n  [dim]Only counts are sent. Never message content.[/]",
            defaultValue: false);

        AnsiConsole.WriteLine();

        var syncInterval = 5;
        if (shareGlobal)
        {
            syncInterval = AnsiConsole.Prompt(
                new TextPrompt<int>("[bold]Sync interval in minutes?[/]")
                    .DefaultValue(5)
                    .Validate(v => v >= 1 ? ValidationResult.Success() : ValidationResult.Error("Must be at least 1 minute.")));
            AnsiConsole.WriteLine();
        }

        var config = new AppConfig
        {
            ShareGlobal         = shareGlobal,
            SyncIntervalMinutes = syncInterval
        };

        _configManager.Save(config);
        AnsiConsole.MarkupLine($"[green]✓[/] Config saved to [dim]{_configManager.ConfigFilePath}[/]");

        var (success, message) = _daemonManager.Register(binaryPath);
        if (success)
            AnsiConsole.MarkupLine("[green]✓[/] Daemon registered and starting...");
        else
            AnsiConsole.MarkupLine($"[red]✗[/] Daemon registration failed: {message}");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Run [bold]`ollim status`[/] to confirm it's running.");
    }
}
