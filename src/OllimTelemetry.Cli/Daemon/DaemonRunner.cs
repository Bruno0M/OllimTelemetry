using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Parsing;
using OllimTelemetry.Core.Queue;
using OllimTelemetry.Core.Sync;
using OllimTelemetry.Core.Watching;
using OllimTelemetry.Models;

namespace OllimTelemetry.Cli.Daemon;

internal static class DaemonRunner
{
    public static async Task RunAsync(CancellationToken ct)
    {
        var configManager = new ConfigManager();
        var config        = configManager.LoadOrCreate();

        using var queue   = new SyncQueue();
        var parser         = new LogParser();
        using var watcher  = new LogWatcher();
        using var http     = new System.Net.Http.HttpClient();
        var syncService    = new SyncService(configManager, queue, http);

        // Backfill: process all existing .jsonl files before watching for new changes.
        // GetOffset returns 0 for unseen files and the stored byte offset for files
        // already partially processed, so this is safe to run on every startup.
        BackfillExistingFiles(config.Agent, parser, queue);

        // REQ-37: on each file change, parse delta and enqueue batch
        watcher.Start(filePath => ProcessFile(filePath, config.Agent, parser, queue));

        syncService.Start();

        await Console.Error.WriteLineAsync("[ollim] daemon started");

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException) { }

        await syncService.StopAsync();
        await Console.Error.WriteLineAsync("[ollim] daemon stopped");
    }

    private static readonly string WatchPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    private static void BackfillExistingFiles(string agent, LogParser parser, SyncQueue queue)
    {
        if (!Directory.Exists(WatchPath)) return;

        var files = Directory.EnumerateFiles(WatchPath, "*.jsonl", SearchOption.AllDirectories);
        var count = 0;

        foreach (var file in files)
        {
            ProcessFile(file, agent, parser, queue);
            count++;
        }

        if (count > 0)
            Console.Error.WriteLine($"[ollim] backfilled {count} existing log file(s)");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static void ProcessFile(string filePath, string agent, LogParser parser, SyncQueue queue)
    {
        try
        {
            var offset  = queue.GetOffset(filePath);
            var records = parser.Parse(filePath, offset, out var newOffset, agent);

            if (records.Count == 0) return;

            queue.SetOffset(filePath, newOffset);

            var batch = new SyncBatch(
                agent,
                records.Sum(r => r.InputTokens),
                records.Sum(r => r.OutputTokens),
                records.Sum(r => r.CacheReadTokens),
                records.Sum(r => r.CacheWriteTokens),
                records[0].Timestamp.ToString("O"),
                records[^1].Timestamp.ToString("O"),
                ProjectPathResolver.Resolve(filePath)
            );

            queue.Enqueue(batch);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ollim] error processing {filePath}: {ex.Message}");
        }
    }
}
