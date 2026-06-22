using OllimTelemetry.Core.Parsing;
using OllimTelemetry.Core.Queue;
using OllimTelemetry.Models;

namespace OllimTelemetry.Core.Ingestion;

public static class LogIngester
{
    /// <summary>
    /// Reads the delta from <paramref name="filePath"/> (from stored offset to EOF),
    /// enqueues a SyncBatch if any usage records were found, and returns true if
    /// any records were processed.
    /// </summary>
    public static bool ProcessFile(string filePath, string agent, LogParser parser, SyncQueue queue)
    {
        try
        {
            var offset  = queue.GetOffset(filePath);
            var records = parser.Parse(filePath, offset, out var newOffset, agent);

            if (records.Count == 0)
            {
                // Advance the offset so the file isn't re-read from the same position
                // on the next hook invocation (e.g. tool-call-only sessions with no usage).
                if (newOffset > offset)
                    queue.SetOffset(filePath, newOffset);
                return false;
            }

            var modelId = records.FirstOrDefault(r => r.ModelId is not null)?.ModelId;
            var batch = new SyncBatch(
                agent,
                records.Sum(r => r.InputTokens),
                records.Sum(r => r.OutputTokens),
                records.Sum(r => r.CacheReadTokens),
                records.Sum(r => r.CacheWriteTokens),
                records[0].Timestamp.ToString("O"),
                records[^1].Timestamp.ToString("O"),
                ProjectPathResolver.Resolve(filePath),
                modelId
            );

            queue.SetOffsetAndEnqueue(filePath, newOffset, batch);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ollim] error processing {filePath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Processes all existing .jsonl files under ~/.claude/projects/ from their stored
    /// offsets. Used on first run to ingest historical sessions.
    /// </summary>
    public static int BackfillAll(string agent, LogParser parser, SyncQueue queue)
    {
        if (!Directory.Exists(OllimPaths.ClaudeProjectsRoot)) return 0;

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(OllimPaths.ClaudeProjectsRoot, "*.jsonl", SearchOption.AllDirectories))
        {
            if (ProcessFile(file, agent, parser, queue))
                count++;
        }

        return count;
    }
}
