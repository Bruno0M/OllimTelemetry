using OllimTelemetry.Core.Config;
using Spectre.Console;

namespace OllimTelemetry.Cli.Commands;

internal static class ConfigCommand
{
    /// <summary>Open the ollim config file in your default editor.</summary>
    public static Task<int> RunAsync()
    {
        var editor = Environment.GetEnvironmentVariable("VISUAL")
                  ?? Environment.GetEnvironmentVariable("EDITOR")
                  ?? "vi";

        var manager    = new ConfigManager();
        manager.LoadOrCreate();
        var configPath = manager.ConfigFilePath;

        using var proc = new System.Diagnostics.Process();
        proc.StartInfo = new System.Diagnostics.ProcessStartInfo(editor, configPath)
        {
            UseShellExecute = false
        };
        proc.Start();
        proc.WaitForExit();

        return Task.FromResult(0);
    }
}
