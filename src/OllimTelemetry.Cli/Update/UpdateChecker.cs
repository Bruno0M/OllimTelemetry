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

    private static string? CurrentVersion =>
        typeof(UpdateChecker).Assembly.GetName().Version?.ToString(3);

    // Fire-and-forget — never blocks the command path.
    internal static void ScheduleRefresh() => Task.Run(RefreshAsync);

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
            await File.WriteAllTextAsync(CacheFile, cacheJson);
        }
        catch { }
    }
}
