using OllimTelemetry.Core.Config;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class UnlinkCommand
{
    public static Task<int> RunAsync()
    {
        var configManager = new ConfigManager();
        var config        = configManager.LoadOrCreate();

        // D-09: keep UserId — only disable sharing
        configManager.Save(config with { ShareGlobal = false });

        AnsiConsole.MarkupLine("[green]✓[/] Sharing disabled. Your data will no longer be submitted to the leaderboard.");
        AnsiConsole.MarkupLine("[dim]Your local data and UserId are preserved. Run `ollim start` to re-enable.[/]");

        return Task.FromResult(0);
    }
}
