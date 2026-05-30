using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Parsing;
using OllimTelemetry.Core.Queue;
using OllimTelemetry.Core.Sync;
using OllimTelemetry.Core.Watching;
using OllimTelemetry.Models;
using Spectre.Console.Cli;

namespace OllimTelemetry.Cli.Commands;

// Internal entry point invoked by the OS service (launchd / systemd).
// Not intended for direct user invocation.
public sealed class DaemonCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var configManager = new ConfigManager();
        var config        = configManager.LoadOrCreate();

        using var queue  = new SyncQueue();
        var parser        = new LogParser();
        var watcher       = new LogWatcher();
        var http          = new System.Net.Http.HttpClient();
        var syncService   = new SyncService(configManager, queue, http);

        var cts = new CancellationTokenSource();

        // REQ-36: handle SIGTERM and SIGINT
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            cts.Cancel();
            // give StopAsync a moment to flush before the process exits
            syncService.StopAsync().GetAwaiter().GetResult();
        };

        // REQ-37: on each file change, parse delta and enqueue batch
        watcher.Start(filePath => ProcessFile(filePath, config.Agent, parser, queue));

        syncService.Start();

        await Console.Error.WriteLineAsync("[ollim] daemon started");

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException) { }

        // graceful shutdown — REQ-37
        watcher.Stop();
        await syncService.StopAsync();
        await Console.Error.WriteLineAsync("[ollim] daemon stopped");

        return 0;
    }

    private static void ProcessFile(string filePath, string agent, LogParser parser, SyncQueue queue)
    {
        try
        {
            var offset  = queue.GetOffset(filePath);
            var records = parser.Parse(filePath, offset, out var newOffset, agent);

            if (records.Count == 0) return;

            queue.SetOffset(filePath, newOffset);

            // aggregate the entire delta into one batch
            var batch = new SyncBatch(
                agent,
                records.Sum(r => r.InputTokens),
                records.Sum(r => r.OutputTokens),
                records.Sum(r => r.CacheReadTokens),
                records.Sum(r => r.CacheWriteTokens),
                records[0].Timestamp.ToString("O"),
                records[^1].Timestamp.ToString("O")
            );

            queue.Enqueue(batch);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ollim] error processing {filePath}: {ex.Message}");
        }
    }
}
