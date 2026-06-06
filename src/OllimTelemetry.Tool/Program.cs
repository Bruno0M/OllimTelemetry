using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;

const string Repo = "Bruno0M/OllimTelemetry";

var rid = DetectRid();
if (rid is null)
{
    Console.Error.WriteLine("ollim: unsupported platform — only linux-x64, linux-arm64, osx-arm64, and osx-x64 are supported.");
    return 1;
}

var version   = GetVersion();
var cacheDir  = GetCacheDir(version);
var binary    = Path.Combine(cacheDir, "ollim");
var url       = $"https://github.com/{Repo}/releases/download/v{version}/ollim-{rid}.tar.gz";

if (!File.Exists(binary))
{
    if (!await DownloadAndExtractAsync(url, cacheDir))
        return 1;

    SetExecutable(binary);
}

var psi = new ProcessStartInfo(binary) { UseShellExecute = false };
psi.Environment["OLLIM_INSTALL_METHOD"] = "nuget";
foreach (var arg in args) psi.ArgumentList.Add(arg);

using var proc = new Process { StartInfo = psi };
proc.Start();
proc.WaitForExit();
return proc.ExitCode;

// ── helpers ────────────────────────────────────────────────────────────────

static string? DetectRid()
{
    bool linux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    bool mac   = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    bool arm   = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

    return (linux, mac, arm) switch
    {
        (true,  false, false) => "linux-x64",
        (true,  false, true)  => "linux-arm64",
        (false, true,  true)  => "osx-arm64",
        (false, true,  false) => "osx-x64",
        _                     => null
    };
}

static string GetVersion() =>
    typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

static string GetCacheDir(string version)
{
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var dir  = Path.Combine(home, ".ollim", "bin", version);
    Directory.CreateDirectory(dir);
    return dir;
}

static async Task<bool> DownloadAndExtractAsync(string url, string destDir)
{
    Console.Error.WriteLine("ollim: downloading native binary...");
    Console.Error.WriteLine($"       {url}");
    try
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ollim-tool/1.0");

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var gz     = new GZipStream(stream, CompressionMode.Decompress);
        await TarFile.ExtractToDirectoryAsync(gz, destDir, overwriteFiles: true);

        Console.Error.WriteLine("ollim: download complete.");
        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ollim: download failed — {ex.Message}");
        return false;
    }
}

static void SetExecutable(string path) =>
    File.SetUnixFileMode(path,
        UnixFileMode.UserRead    | UnixFileMode.UserWrite   | UnixFileMode.UserExecute  |
        UnixFileMode.GroupRead   | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead   | UnixFileMode.OtherExecute);
