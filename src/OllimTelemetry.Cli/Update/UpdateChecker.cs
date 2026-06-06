using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OllimTelemetry.Core;

namespace OllimTelemetry.Cli.Update;

internal sealed record UpdateCheckCache(
    [property: JsonPropertyName("checkedAt")]    DateTimeOffset CheckedAt,
    [property: JsonPropertyName("latestVersion")] string LatestVersion);

internal sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string? TagName);

internal static class UpdateChecker
{
    private static readonly string CacheFile =
        Path.Combine(OllimPaths.DataDir, "version_check.json");

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private const string ApiUrl = "https://api.github.com/repos/Bruno0M/OllimTelemetry/releases/latest";

    // AssemblyInformationalVersion carries the NuGet <Version> from the .csproj (e.g. "0.1.0").
    // AssemblyVersion defaults to "1.0.0.0" and does not track releases.
    internal static string? CurrentVersion =>
        typeof(UpdateChecker).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            .Split('+')[0]; // strip build-metadata suffix (e.g. "+abc1234")

    // Fire-and-forget — never blocks the command path.
    internal static void ScheduleRefresh()
    {
        if (IsCacheFresh()) return;
        Task.Run(RefreshAsync);
    }

    private static bool IsCacheFresh()
    {
        try
        {
            if (!File.Exists(CacheFile)) return false;
            var cache = JsonSerializer.Deserialize(
                File.ReadAllText(CacheFile), CliJsonContext.Default.UpdateCheckCache);
            return cache is not null && DateTimeOffset.UtcNow - cache.CheckedAt <= CacheTtl;
        }
        catch { return false; }
    }

    internal static void PrintNoticeIfAvailable(string installMethod)
    {
        try
        {
            if (!File.Exists(CacheFile)) return;

            var json = File.ReadAllText(CacheFile);
            var cache = JsonSerializer.Deserialize(json, CliJsonContext.Default.UpdateCheckCache);
            if (cache is null) return;
            if (DateTimeOffset.UtcNow - cache.CheckedAt > CacheTtl) return;

            var current = CurrentVersion;
            if (current is null) return;
            if (!Version.TryParse(current,           out var cur))    return;
            if (!Version.TryParse(cache.LatestVersion, out var latest)) return;
            if (latest <= cur) return;

            var updateCmd = installMethod switch
            {
                "npm"   => "npm install -g ollim-telemetry",
                "nuget" => "dotnet tool update -g ollim-telemetry",
                _       => "curl -fsSL https://ollim.dev/install.sh | bash",
            };

            Console.Error.WriteLine();
            Console.Error.WriteLine($"  A new version of ollim is available: {current} → {cache.LatestVersion}");
            Console.Error.WriteLine($"  Update: {updateCmd}");
        }
        catch { }
    }

    private static async Task RefreshAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ollim-telemetry/1.0");
            http.Timeout = TimeSpan.FromSeconds(5);

            using var resp = await http.GetAsync(ApiUrl);
            if (!resp.IsSuccessStatusCode) return;

            var body    = await resp.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize(body, CliJsonContext.Default.GitHubRelease);
            if (release?.TagName is null) return;

            var version = release.TagName.TrimStart('v');
            if (!Version.TryParse(version, out _)) return; // reject malformed tags

            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
            var cache     = new UpdateCheckCache(DateTimeOffset.UtcNow, version);
            var cacheJson = JsonSerializer.Serialize(cache, CliJsonContext.Default.UpdateCheckCache);
            // Write to a temp file then rename — atomic on Linux so PrintNoticeIfAvailable
            // always reads a complete file, never partial content from a concurrent write.
            var tmp = CacheFile + ".tmp";
            await File.WriteAllTextAsync(tmp, cacheJson);
            File.Move(tmp, CacheFile, overwrite: true);
        }
        catch { }
    }
}
