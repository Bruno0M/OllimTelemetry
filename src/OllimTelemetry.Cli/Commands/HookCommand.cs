using System.Text.Json;
using ConsoleAppFramework;
using OllimTelemetry.Cli.Update;
using OllimTelemetry.Core;
using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Ingestion;
using OllimTelemetry.Core.Parsing;
using OllimTelemetry.Core.Queue;
using OllimTelemetry.Core.Sync;

namespace OllimTelemetry.Cli.Commands;

internal static class HookCommand
{
    // Always exits 0 — hook failures must never interrupt Claude Code.
    [Hidden]
    public static async Task<int> RunAsync()
    {
        try
        {
            var input = await ReadStdinAsync();

            var filePath = ResolveFilePath(input);
            if (filePath is null)
            {
                await Console.Error.WriteLineAsync("[ollim] hook: could not resolve JSONL file from hook input");
                return 0;
            }

            var configManager = new ConfigManager();
            var config        = configManager.LoadOrCreate();

            using var queue  = new SyncQueue();
            var parser        = new LogParser();

            LogIngester.ProcessFile(filePath, config.Agent, parser, queue);

            // Flush unconditionally: even if the current session produced no new records,
            // there may be pre-existing failed batches ready for retry.
            // Timeout kept short so a network hang never stalls Claude Code's shutdown.
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var syncService = new SyncService(configManager, queue, http,
                UpdateChecker.CurrentVersion ?? "unknown");
            await syncService.FlushOnceAsync();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[ollim] hook error: {ex.Message}");
        }

        return 0;
    }

    private static async Task<StopHookInput?> ReadStdinAsync()
    {
        // 5-second guard prevents an unexpectedly large stdin pipe from stalling
        // Claude Code's shutdown sequence while waiting for this hook to exit.
        using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var json = await Console.In.ReadToEndAsync(cts.Token);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize(json, CliJsonContext.Default.StopHookInput);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ResolveFilePath(StopHookInput? input)
    {
        if (input is null) return null;

        // transcript_path is the most direct: Claude Code hands us the exact file.
        // Trust it without File.Exists — if the file isn't flushed yet the fallback scan
        // would O(N) traverse all sessions, which is worse than a graceful parse miss.
        if (!string.IsNullOrWhiteSpace(input.TranscriptPath))
            return input.TranscriptPath;

        if (!string.IsNullOrWhiteSpace(input.SessionId) && Directory.Exists(OllimPaths.ClaudeProjectsRoot))
        {
            var match = Directory.EnumerateFiles(
                OllimPaths.ClaudeProjectsRoot, $"{input.SessionId}.jsonl", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (match is not null) return match;
        }

        return null;
    }
}
