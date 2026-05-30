#!/usr/bin/env dotnet
#:property PublishAot=false
#pragma warning disable CA1416 // SetUnixFileMode: Windows is unsupported by design

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

var rootDir = Environment.CurrentDirectory; // must be run from the repo root
var version = GetVersion();

Console.WriteLine($"Packaging ollim npm packages — version {version}");
Console.WriteLine();

// RID → (npm package name, native SQLite library filename)
var packages = new Dictionary<string, (string PkgName, string NativeLib)>
{
    ["linux-x64"]   = ("ollim-linux-x64",    "libe_sqlite3.so"),
    ["linux-arm64"] = ("ollim-linux-arm64",   "libe_sqlite3.so"),
    ["osx-arm64"]   = ("ollim-darwin-arm64",  "libe_sqlite3.dylib"),
    ["osx-x64"]     = ("ollim-darwin-x64",    "libe_sqlite3.dylib"),
};

// ── 1. Update versions in all package.json files ────────────────────────────

var allPackageDirs = new[] { "ollim" }
    .Concat(packages.Values.Select(v => v.PkgName))
    .Select(pkg => Path.Combine(rootDir, "npm", pkg))
    .ToArray();

Console.WriteLine("Updating package.json versions...");
foreach (var dir in allPackageDirs)
{
    var pkgJson = Path.Combine(dir, "package.json");
    SetVersion(pkgJson, version);
    Console.WriteLine($"  ✓ {Path.GetRelativePath(rootDir, pkgJson)}");
}

// Update optionalDependencies versions in main package
var mainPkgJson = Path.Combine(rootDir, "npm", "ollim", "package.json");
SetOptionalDepsVersion(mainPkgJson, version);
Console.WriteLine();

// ── 2. Copy native binaries into platform packages ──────────────────────────

Console.WriteLine("Copying native binaries...");
var failed = new List<string>();

foreach (var (rid, (pkgName, nativeLib)) in packages)
{
    var srcBinary = Path.Combine(rootDir, "dist", rid, "ollim");
    var srcLib    = Path.Combine(rootDir, "dist", rid, nativeLib);
    var binDir    = Path.Combine(rootDir, "npm", pkgName, "bin");

    if (!File.Exists(srcBinary))
    {
        Console.Error.WriteLine($"  ✗ dist/{rid}/ollim not found — run 'dotnet scripts/build.cs' first");
        failed.Add(rid);
        continue;
    }

    CopyExecutable(srcBinary, Path.Combine(binDir, "ollim"));
    Console.WriteLine($"  ✓ dist/{rid}/ollim → npm/{pkgName}/bin/ollim");

    if (File.Exists(srcLib))
    {
        File.Copy(srcLib, Path.Combine(binDir, nativeLib), overwrite: true);
        Console.WriteLine($"  ✓ dist/{rid}/{nativeLib} → npm/{pkgName}/bin/{nativeLib}");
    }
    else
    {
        Console.WriteLine($"  ⚠ dist/{rid}/{nativeLib} not found — skipped (expected after full build)");
    }
}

if (failed.Count > 0)
{
    Console.Error.WriteLine($"\nMissing RIDs: {string.Join(", ", failed)}");
    Console.Error.WriteLine("Build missing targets first, then re-run pack-npm.");
    return 1;
}

Console.WriteLine();

// ── 3. npm pack each package ────────────────────────────────────────────────

Console.WriteLine("Running npm pack...");

// Platform packages first so they're available when packing the main package
var packOrder = packages.Values.Select(v => v.PkgName).Append("ollim").ToArray();

foreach (var pkgName in packOrder)
{
    var dir = Path.Combine(rootDir, "npm", pkgName);
    var exit = Run("npm", ["pack"], workingDir: dir);
    if (exit == 0)
        Console.WriteLine($"  ✓ {pkgName}");
    else
    {
        Console.Error.WriteLine($"  ✗ npm pack failed in npm/{pkgName}");
        return 1;
    }
}

Console.WriteLine();
Console.WriteLine("Done. Tarballs created in npm/<package>/");
Console.WriteLine();
Console.WriteLine("Next steps:");
Console.WriteLine($"  1. Create GitHub Release v{version} and upload dist/<rid>/ollim assets");
Console.WriteLine($"  2. Publish platform packages first:");
foreach (var (_, (pkgName, _)) in packages)
    Console.WriteLine($"       npm publish npm/{pkgName}/{pkgName}-{version}.tgz --access public");
Console.WriteLine($"  3. Publish main package:");
Console.WriteLine($"       npm publish npm/ollim/ollim-{version}.tgz --access public");
Console.WriteLine();
Console.WriteLine("Verify with: npm info ollim version");

return 0;

// ── helpers ─────────────────────────────────────────────────────────────────

static string GetVersion()
{
    var tag = Capture("git", ["describe", "--tags", "--abbrev=0"]);
    if (!string.IsNullOrWhiteSpace(tag))
        return tag.TrimStart('v').Trim();
    return "0.1.0";
}

static void CopyExecutable(string src, string dest)
{
    File.Copy(src, dest, overwrite: true);
    File.SetUnixFileMode(dest,
        UnixFileMode.UserRead    | UnixFileMode.UserWrite   | UnixFileMode.UserExecute  |
        UnixFileMode.GroupRead   | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead   | UnixFileMode.OtherExecute);
}

static void SetVersion(string pkgJsonPath, string version)
{
    var json = JsonNode.Parse(File.ReadAllText(pkgJsonPath))!;
    json["version"] = version;
    File.WriteAllText(pkgJsonPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
}

static void SetOptionalDepsVersion(string pkgJsonPath, string version)
{
    var json = JsonNode.Parse(File.ReadAllText(pkgJsonPath))!;
    var deps = json["optionalDependencies"]?.AsObject();
    if (deps is null) return;
    foreach (var key in deps.Select(kv => kv.Key).ToArray())
        deps[key] = version;
    File.WriteAllText(pkgJsonPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
}

static int Run(string cmd, string[] args, string? workingDir = null)
{
    using var proc = new Process
    {
        StartInfo = new ProcessStartInfo(cmd, args)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDir ?? string.Empty
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
    catch { return string.Empty; }
}
