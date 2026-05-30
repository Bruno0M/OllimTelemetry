#!/usr/bin/env dotnet
#:property PublishAot=false

using System.Diagnostics;

var rids = new[] { "osx-arm64", "osx-x64", "linux-x64", "linux-arm64" };

var rootDir   = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
var cliProject = Path.Combine(rootDir, "src", "OllimTelemetry.Cli", "OllimTelemetry.Cli.csproj");
var version   = GetVersion();

Console.WriteLine($"Building ollim {version}");
Console.WriteLine();

var failed = new List<string>();

foreach (var rid in rids)
{
    Console.WriteLine($"→ Building {rid}...");

    var outDir = Path.Combine(rootDir, "dist", rid);
    var exitCode = Run("dotnet", [
        "publish", cliProject,
        "-c", "Release",
        "-r", rid,
        "--self-contained", "true",
        $"/p:PublishAot=true",
        $"/p:AssemblyVersion={version}",
        "-o", outDir
    ]);

    if (exitCode == 0)
        Console.WriteLine($"✓ dist/{rid}/ollim");
    else
    {
        Console.WriteLine($"✗ {rid} failed (exit {exitCode})");
        failed.Add(rid);
    }

    Console.WriteLine();
}

if (failed.Count > 0)
{
    Console.Error.WriteLine($"Failed RIDs: {string.Join(", ", failed)}");
    return 1;
}

Console.WriteLine("Done. Binaries in dist/");
return 0;

// ── helpers ────────────────────────────────────────────────────────────────

static string GetVersion()
{
    // try git tag first (e.g. v0.2.1 → 0.2.1)
    var tag = Capture("git", ["describe", "--tags", "--abbrev=0"]);
    if (!string.IsNullOrWhiteSpace(tag))
        return tag.TrimStart('v').Trim();

    return "0.1.0";
}

static int Run(string cmd, string[] args)
{
    using var proc = new Process
    {
        StartInfo = new ProcessStartInfo(cmd, args)
        {
            UseShellExecute = false
        }
    };
    proc.Start();
    proc.WaitForExit();
    return proc.ExitCode;
}

static string Capture(string cmd, string[] args)
{
    try
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo(cmd, args)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            }
        };
        proc.Start();
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return proc.ExitCode == 0 ? output : string.Empty;
    }
    catch
    {
        return string.Empty;
    }
}
