using OllimTelemetry.Cli.Onboarding;
using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Daemon;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class StartCommand
{
    public static Task<int> RunAsync()
    {
        var configManager = new ConfigManager();
        var daemonManager = new DaemonManager();
        var binaryPath    = Environment.ProcessPath ?? "ollim";

        if (!File.Exists(configManager.ConfigFilePath))
        {
            var flow = new OnboardingFlow(configManager, daemonManager);
            flow.Run(binaryPath);
            return Task.FromResult(0);
        }

        var (success, message) = daemonManager.Register(binaryPath);
        if (success)
            AnsiConsole.MarkupLine("[green]✓[/] Daemon started.");
        else
            AnsiConsole.MarkupLine($"[red]✗[/] {message}");

        return Task.FromResult(success ? 0 : 1);
    }
}
