using OllimTelemetry.Core.Config;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class UnlinkCommand
{
    public static Task<int> RunAsync()
    {
        var configManager = new ConfigManager();
        var config        = configManager.LoadOrCreate();

        // keep UserId — only disable sharing and clear auth
        var login = config.GitHubLogin;
        configManager.Save(config with { ShareGlobal = false, SessionToken = null, GitHubLogin = null });

        if (login is not null)
            AnsiConsole.MarkupLine($"[dim]Unlinked from @{Markup.Escape(login)}.[/]");
        AnsiConsole.MarkupLine("[green]✓[/] Sharing disabled. Your data will no longer be submitted to the leaderboard.");
        AnsiConsole.MarkupLine("[dim]Your local data and UserId are preserved. Run `ollim start` to re-enable.[/]");

        return Task.FromResult(0);
    }
}
