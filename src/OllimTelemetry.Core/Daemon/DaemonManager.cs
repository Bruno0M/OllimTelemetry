using System.Runtime.InteropServices;

namespace OllimTelemetry.Core.Daemon;

public sealed class DaemonManager
{
    private static readonly string PlistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", "com.ollim.plist");

    private static readonly string SystemdPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "systemd", "user", "ollim.service");

    public (bool Success, string Message) Register(string binaryPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RegisterLaunchd(binaryPath);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return RegisterSystemd(binaryPath);

        return (false, "Ollim Telemetry is not supported on Windows in the MVP.");
    }

    public (bool Success, string Message) Unregister()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return UnregisterLaunchd();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return UnregisterSystemd();

        return (false, "Ollim Telemetry is not supported on Windows in the MVP.");
    }

    public bool IsRunning()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var result = RunProcess("launchctl", "list com.ollim");
                return result.ExitCode == 0;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var result = RunProcess("systemctl", "--user is-active ollim");
                return result.Output.Trim() == "active";
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private (bool Success, string Message) RegisterLaunchd(string binaryPath)
    {
        try
        {
            var template = LoadTemplate("com.ollim.plist.template");
            var plist    = template.Replace("{{BINARY_PATH}}", binaryPath);

            Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
            File.WriteAllText(PlistPath, plist);

            var result = RunProcess("launchctl", $"load {PlistPath}");
            return result.ExitCode == 0
                ? (true, "Daemon registered via launchd.")
                : (false, $"launchctl load failed: {result.Output}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to register launchd daemon: {ex.Message}");
        }
    }

    private (bool Success, string Message) UnregisterLaunchd()
    {
        try
        {
            if (!File.Exists(PlistPath))
                return (true, "Daemon plist not found — nothing to unregister.");

            var result = RunProcess("launchctl", $"unload {PlistPath}");
            File.Delete(PlistPath);
            return result.ExitCode == 0
                ? (true, "Daemon unregistered.")
                : (false, $"launchctl unload failed: {result.Output}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to unregister launchd daemon: {ex.Message}");
        }
    }

    private (bool Success, string Message) RegisterSystemd(string binaryPath)
    {
        try
        {
            var template = LoadTemplate("ollim.service.template");
            var unit     = template.Replace("{{BINARY_PATH}}", binaryPath);

            Directory.CreateDirectory(Path.GetDirectoryName(SystemdPath)!);
            File.WriteAllText(SystemdPath, unit);

            RunProcess("systemctl", "--user daemon-reload");
            var result = RunProcess("systemctl", "--user enable --now ollim");
            return result.ExitCode == 0
                ? (true, "Daemon registered via systemd --user.")
                : (false, $"systemctl enable failed: {result.Output}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to register systemd daemon: {ex.Message}");
        }
    }

    private (bool Success, string Message) UnregisterSystemd()
    {
        try
        {
            var result = RunProcess("systemctl", "--user disable --now ollim");
            if (File.Exists(SystemdPath)) File.Delete(SystemdPath);
            RunProcess("systemctl", "--user daemon-reload");
            return result.ExitCode == 0
                ? (true, "Daemon unregistered.")
                : (false, $"systemctl disable failed: {result.Output}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to unregister systemd daemon: {ex.Message}");
        }
    }

    private static string LoadTemplate(string name)
    {
        var stream = typeof(DaemonManager).Assembly.GetManifestResourceStream(name);
        if (stream is not null)
            using (var reader = new System.IO.StreamReader(stream))
                return reader.ReadToEnd();

        throw new FileNotFoundException($"Embedded template not found: {name}");
    }

    private static (int ExitCode, string Output) RunProcess(string cmd, string args)
    {
        using var proc = new System.Diagnostics.Process();
        proc.StartInfo = new System.Diagnostics.ProcessStartInfo(cmd, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false
        };
        proc.Start();
        var output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return (proc.ExitCode, output);
    }
}
