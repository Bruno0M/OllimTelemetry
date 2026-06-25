using OllimTelemetry.Cli.Onboarding;
using OllimTelemetry.Cli.Update;
using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Hook;
using OllimTelemetry.Core.Ingestion;
using OllimTelemetry.Core.Parsing;
using OllimTelemetry.Core.Queue;
using OllimTelemetry.Core.Sync;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class StartCommand
{
    /// <summary>Register hooks for all supported agents and start tracking token usage.</summary>
    public static async Task<int> RunAsync()
    {
        var configManager    = new ConfigManager();
        var binaryPath       = Environment.ProcessPath ?? "ollim";
        var claudeHookCmd    = BuildHookCommand(binaryPath);
        var codexHookCmd     = BuildHookCommand(binaryPath, " --agent codex");

        using var queue = new SyncQueue();

        if (!File.Exists(configManager.ConfigFilePath))
        {
            var flow = new OnboardingFlow(configManager);
            flow.Run(claudeHookCmd, codexHookCmd);
            await BackfillAndSyncAsync(configManager, queue);
        }
        else
        {
            // Claude Code hook
            var (claudeChanged, claudeError) = ClaudeHookManager.Install(claudeHookCmd);
            if (claudeError is not null)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Claude Code hook install failed: {claudeError}");
                return 1;
            }
            if (claudeChanged)
                AnsiConsole.MarkupLine("[green]✓[/] Hook registered in ~/.claude/settings.json");
            else
                AnsiConsole.MarkupLine("[dim]Claude Code hook already registered.[/]");

            // Codex hook — skip silently if Codex is not installed on this machine.
            // Always backfill Codex sessions when the hook is newly registered so that
            // existing users who upgrade get their historical Codex sessions ingested.
            bool codexJustInstalled = false;
            if (CodexHookManager.IsCodexPresent())
            {
                var (codexChanged, codexError) = CodexHookManager.Install(codexHookCmd);
                if (codexError is not null)
                    AnsiConsole.MarkupLine($"[yellow]⚠[/] Codex hook install failed: {codexError}");
                else if (codexChanged)
                {
                    AnsiConsole.MarkupLine("[green]✓[/] Hook registered in ~/.codex/hooks.json");
                    codexJustInstalled = true;
                }
                else
                    AnsiConsole.MarkupLine("[dim]Codex hook already registered.[/]");
            }

            // First run (no offsets at all): backfill everything.
            // Upgrade run (Codex hook just registered): backfill only Codex so that
            // existing users don't re-process Claude Code sessions they already submitted.
            if (!queue.HasAnyOffsets())
                await BackfillAndSyncAsync(configManager, queue);
            else if (codexJustInstalled)
                await BackfillCodexOnlyAsync(configManager, queue);
        }

        // Offer GitHub login if the user hasn't authenticated yet.
        var cfg = configManager.LoadOrCreate();
        if (cfg.ShareGlobal && cfg.SessionToken is null)
        {
            AnsiConsole.WriteLine();
            var doLogin = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Link your GitHub account now to start syncing?[/]")
                    .AddChoices("Yes", "No")) == "Yes";
            if (doLogin)
                await LoginCommand.RunAsync();
        }

        return 0;
    }

    private static string BuildHookCommand(string binaryPath, string suffix = "")
    {
        // In dev mode, propagate the current process's isolation env vars into the
        // registered hook command so the agent fires the hook with the same context.
        static string? Env(string key) => Environment.GetEnvironmentVariable(key);

        var ollimEnv = Env("OLLIM_ENV");
        if (ollimEnv != "dev")
            return $"{binaryPath} hook{suffix}";

        static string ShellQuote(string val) => "'" + val.Replace("'", "'\\''") + "'";

        var parts = new System.Text.StringBuilder();
        foreach (var key in new[] { "OLLIM_ENV", "OLLIM_BACKEND_URL", "XDG_CONFIG_HOME", "XDG_DATA_HOME" })
        {
            var val = Env(key);
            if (!string.IsNullOrEmpty(val))
                parts.Append($"{key}={ShellQuote(val)} ");
        }
        parts.Append($"{binaryPath} hook{suffix}");
        return parts.ToString();
    }

    private static async Task BackfillAndSyncAsync(ConfigManager configManager, SyncQueue queue)
    {
        var config       = configManager.LoadOrCreate();
        var claudeParser = new LogParser();
        var codexParser  = new CodexLogParser();

        var count = LogIngester.BackfillAll(config.Agent, claudeParser, queue);
        count    += LogIngester.BackfillCodex(codexParser, queue);

        if (count > 0)
        {
            AnsiConsole.MarkupLine($"[dim]Backfilled {count} session(s) from existing logs.[/]");
            await FlushAsync(configManager, queue);
        }
    }

    private static async Task BackfillCodexOnlyAsync(ConfigManager configManager, SyncQueue queue)
    {
        var count = LogIngester.BackfillCodex(new CodexLogParser(), queue);
        if (count > 0)
        {
            AnsiConsole.MarkupLine($"[dim]Backfilled {count} Codex session(s) from existing logs.[/]");
            await FlushAsync(configManager, queue);
        }
    }

    private static async Task FlushAsync(ConfigManager configManager, SyncQueue queue)
    {
        using var http  = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var syncService = new SyncService(configManager, queue, http,
            UpdateChecker.CurrentVersion ?? "unknown");
        await syncService.FlushOnceAsync();
    }
}
