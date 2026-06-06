using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Hook;
using OllimTelemetry.Core.Queue;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class StatusCommand
{
    public static Task<int> RunAsync()
    {
        var configManager = new ConfigManager();
        var config        = configManager.LoadOrCreate();
        var binaryPath    = Environment.ProcessPath ?? "ollim";
        var hookCommand   = $"{binaryPath} hook";
        var hookInstalled = ClaudeHookManager.IsInstalled(hookCommand);

        var isDev  = Environment.GetEnvironmentVariable("OLLIM_ENV") == "dev";
        var title  = isDev ? "[bold]Ollim Telemetry Status[/] [yellow][[dev]][/]" : "[bold]Ollim Telemetry Status[/]";

        AnsiConsole.Write(new Rule(title).RuleStyle("grey"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"  Hook:         {(hookInstalled ? "[green]active[/]" : "[red]not installed[/]")}");
        AnsiConsole.MarkupLine($"  Sharing:      {(config.ShareGlobal ? "[green]enabled[/]" : "[yellow]disabled[/]")}");
        AnsiConsole.MarkupLine($"  Last sync:    [dim]{config.LastSyncAt ?? "never"}[/]");

        AnsiConsole.WriteLine();

        using var queue   = new SyncQueue();
        var sessions       = queue.CountTrackedFiles();
        var pending        = queue.CountPending();
        AnsiConsole.MarkupLine($"  Sessions tracked: [dim]{sessions}[/]");
        AnsiConsole.MarkupLine($"  Pending batches:  [dim]{pending}[/]");

        if (!hookInstalled)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [yellow]Run [bold]`ollim start`[/] to register the Claude Code hook.[/]");
        }

        return Task.FromResult(0);
    }
}
