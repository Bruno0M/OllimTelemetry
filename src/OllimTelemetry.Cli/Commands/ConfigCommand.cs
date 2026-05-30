using OllimTelemetry.Core.Config;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OllimTelemetry.Cli.Commands;

public sealed class ConfigCommand : AsyncCommand
{
    public override Task<int> ExecuteAsync(CommandContext context)
    {
        var editor = Environment.GetEnvironmentVariable("EDITOR");

        var configPath = ConfigManager.DefaultConfigFilePath;

        if (!string.IsNullOrWhiteSpace(editor))
        {
            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo(editor, configPath)
            {
                UseShellExecute = false
            };
            proc.Start();
            proc.WaitForExit();
        }
        else
        {
            AnsiConsole.MarkupLine($"Config path: [bold]{configPath}[/]");
            AnsiConsole.MarkupLine("[dim]Set $EDITOR to open it automatically.[/]");
        }

        return Task.FromResult(0);
    }
}
