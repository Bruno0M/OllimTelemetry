using System.Net.Http.Json;
using OllimTelemetry.Core.Config;
using OllimTelemetry.Models;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class LeaderboardCommand
{
    public static async Task<int> RunAsync()
    {
        var config = new ConfigManager().LoadOrCreate();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        try
        {
            var response = await http.GetAsync($"{config.BackendUrl}/v1/leaderboard");

            if (!response.IsSuccessStatusCode)
            {
                ShowComingSoon();
                return 0;
            }

            var data = await response.Content.ReadFromJsonAsync(
                CliJsonContext.Default.LeaderboardResponse);

            if (data is null || data.Entries.Length == 0)
            {
                ShowComingSoon();
                return 0;
            }

            RenderTable(data, config);
        }
        catch
        {
            ShowComingSoon();
        }

        return 0;
    }

    private static void RenderTable(LeaderboardResponse data, AppConfig config)
    {
        var table = new Table()
            .AddColumn(new TableColumn("Rank").RightAligned())
            .AddColumn("User")
            .AddColumn(new TableColumn("Total Tokens").RightAligned())
            .AddColumn("Repo");

        foreach (var entry in data.Entries)
        {
            var isMe = config.GitHubLogin is not null
                ? entry.GitHubLogin == config.GitHubLogin
                : entry.UserId == config.UserId;

            var displayUser = entry.GitHubLogin is not null
                ? $"@{Markup.Escape(entry.GitHubLogin)}"
                : (entry.UserId.Length >= 8 ? $"{entry.UserId[..8]}..." : entry.UserId);

            var rank   = isMe ? $"[bold green]{entry.Rank}[/]"              : $"{entry.Rank}";
            var user   = isMe ? $"[bold green]{displayUser}[/]"             : displayUser;
            var tokens = isMe ? $"[bold green]{entry.TotalTokens:N0}[/]"    : $"{entry.TotalTokens:N0}";
            var repo   = entry.RepoName is not null ? Markup.Escape(entry.RepoName) : "[dim]-[/]";

            table.AddRow(rank, user, tokens, repo);
        }

        AnsiConsole.Write(table);
    }

    private static void ShowComingSoon() =>
        AnsiConsole.MarkupLine("[dim]Leaderboard coming soon at ollim.dev[/]");
}
