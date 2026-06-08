using System.Net.Http.Json;
using OllimTelemetry.Cli.Auth;
using OllimTelemetry.Core.Config;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class LinkCommand
{
    public static async Task<int> RunAsync()
    {
        var configManager = new ConfigManager();
        var config        = configManager.LoadOrCreate();
        using var http    = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var env           = Environment.GetEnvironmentVariable("OLLIM_ENV");
        return await ExecuteAsync(http, configManager, config, env, TimeSpan.FromMinutes(5));
    }

    internal static async Task<int> ExecuteAsync(
        HttpClient    http,
        ConfigManager configManager,
        AppConfig     config,
        string?       env,
        TimeSpan      maxWait)
    {
        // AUTH-R18: block prod links from dev environments
        if (env == "dev" && config.BackendUrl.Contains("ollim.dev", StringComparison.OrdinalIgnoreCase))
        {
            await Console.Error.WriteLineAsync(
                "[ollim] error: cannot link to the production API from a dev environment. " +
                "Set OLLIM_BACKEND_URL to point at a local backend.");
            return 1;
        }

        // AUTH-R07: confirm before re-linking
        if (config.SessionToken is not null)
        {
            var relink = AnsiConsole.Confirm(
                $"Already linked as [bold]@{Markup.Escape(config.GitHubLogin ?? "unknown")}[/]. Re-link?",
                defaultValue: false);
            if (!relink) return 0;
        }

        // AUTH-R01: start device flow
        LinkDeviceResponse? device;
        try
        {
            var deviceResp = await http.PostAsync($"{config.BackendUrl}/auth/device", content: null);
            deviceResp.EnsureSuccessStatusCode();
            device = await deviceResp.Content.ReadFromJsonAsync(CliJsonContext.Default.LinkDeviceResponse);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[ollim] error starting link: {ex.Message}");
            return 1;
        }

        if (device is null)
        {
            await Console.Error.WriteLineAsync("[ollim] error: invalid response from server.");
            return 1;
        }

        // AUTH-R02: print instructions
        AnsiConsole.MarkupLine($"  Open: [link]{Markup.Escape(device.VerificationUri)}[/]");
        AnsiConsole.MarkupLine($"  Code: [bold yellow]{Markup.Escape(device.UserCode)}[/]");
        AnsiConsole.WriteLine();

        // AUTH-R03/R04: poll until complete or deadline
        var deadline     = DateTime.UtcNow.Add(maxWait);
        string? linked   = null;
        string? errMsg   = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync("Waiting for GitHub authorization...", async _ =>
            {
                while (DateTime.UtcNow < deadline)
                {
                    var poll = await TryPollAsync(http, config.BackendUrl, device.DeviceCode);
                    if (poll is null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(device.Interval));
                        continue;
                    }

                    if (poll.Status == "complete"
                        && poll.SessionToken is not null
                        && poll.GitHubLogin  is not null)
                    {
                        configManager.Save(config with
                        {
                            ShareGlobal  = true,
                            SessionToken = poll.SessionToken,
                            GitHubLogin  = poll.GitHubLogin,
                        });
                        linked = poll.GitHubLogin;
                        return;
                    }

                    if (poll.Status == "expired")
                    {
                        errMsg = "device code expired. Run `ollim link` again.";
                        return;
                    }

                    // "pending" or unknown — keep polling
                    await Task.Delay(TimeSpan.FromSeconds(device.Interval));
                }

                errMsg = "timed out waiting for authorization. Run `ollim link` again.";
            });

        if (linked is not null)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Linked as [bold]@{Markup.Escape(linked)}[/]");
            return 0;
        }

        await Console.Error.WriteLineAsync($"[ollim] error: {errMsg}");
        return 1;
    }

    private static async Task<LinkPollResponse?> TryPollAsync(
        HttpClient http, string baseUrl, string deviceCode)
    {
        try
        {
            var content  = JsonContent.Create(
                new LinkPollRequest(deviceCode),
                CliJsonContext.Default.LinkPollRequest);
            var response = await http.PostAsync($"{baseUrl}/auth/device/poll", content);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync(CliJsonContext.Default.LinkPollResponse);
        }
        catch { return null; }
    }
}
