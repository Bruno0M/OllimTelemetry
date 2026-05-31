using OllimTelemetry.Core.Queue;
using OllimTelemetry.Models;

namespace OllimTelemetry.Tests;

public sealed class SyncQueueTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _dbPath;

    public SyncQueueTests()
    {
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "queue.db");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private SyncQueue Queue() => new(_dbPath);

    private static SyncBatch Batch(string agent = "claude-code", long input = 100) =>
        new(agent, input, 50, 10, 0, "2026-01-01T00:00:00Z", "2026-01-01T00:05:00Z");

    // AC-04: enqueue 3 → dequeue returns all 3 → MarkSent clears → dequeue returns empty
    [Fact]
    public void Enqueue_Dequeue_MarkSent_ClearsQueue()
    {
        using var queue = Queue();

        queue.Enqueue(Batch(input: 100));
        queue.Enqueue(Batch(input: 200));
        queue.Enqueue(Batch(input: 300));

        var items = queue.Dequeue(50);
        Assert.Equal(3, items.Count);

        queue.MarkSent(items.Select(x => x.Id));

        Assert.Empty(queue.Dequeue(50));
    }

    [Fact]
    public void MarkFailed_PutsItemInBackoff_NotReturnedImmediately()
    {
        using var queue = Queue();
        queue.Enqueue(Batch());

        var items = queue.Dequeue(50);
        Assert.Single(items);

        queue.MarkFailed(items[0].Id);

        // next_retry_at is now in the future — should not be returned
        Assert.Empty(queue.Dequeue(50));
    }

    [Fact]
    public void GetOffset_DefaultsToZero()
    {
        using var queue = Queue();

        Assert.Equal(0L, queue.GetOffset("/some/file.jsonl"));
    }

    [Fact]
    public void SetOffset_ThenGet_RoundTrips()
    {
        using var queue = Queue();

        queue.SetOffset("/path/to/file.jsonl", 4096L);

        Assert.Equal(4096L, queue.GetOffset("/path/to/file.jsonl"));
    }

    [Fact]
    public void SetOffset_Upserts_ExistingEntry()
    {
        using var queue = Queue();

        queue.SetOffset("/file.jsonl", 100L);
        queue.SetOffset("/file.jsonl", 999L);

        Assert.Equal(999L, queue.GetOffset("/file.jsonl"));
    }

    [Fact]
    public void GetBatchesSince_ReturnsOnlyBatchesAfterCutoff()
    {
        using var queue = Queue();

        queue.Enqueue(new SyncBatch("claude-code", 1, 1, 0, 0, "2026-01-01T00:00:00Z", "2026-01-01T00:05:00Z"));
        queue.Enqueue(new SyncBatch("claude-code", 2, 2, 0, 0, "2026-01-10T00:00:00Z", "2026-01-10T00:05:00Z"));

        var recent = queue.GetBatchesSince("2026-01-05T00:00:00Z");

        Assert.Single(recent);
        Assert.Equal(2, recent[0].InputTokens);
    }

    [Fact]
    public void Enqueue_WithRepoName_RoundTripsViaDequeue()
    {
        using var queue = Queue();

        var batch = new SyncBatch("claude-code", 100, 50, 10, 0,
            "2026-01-01T00:00:00Z", "2026-01-01T00:05:00Z", "MyProject");
        queue.Enqueue(batch);

        var items = queue.Dequeue(1);
        Assert.Single(items);
        Assert.Equal("MyProject", items[0].Batch.RepoName);
    }

    [Fact]
    public void Enqueue_WithNullRepoName_RoundTripsNull()
    {
        using var queue = Queue();

        queue.Enqueue(Batch());
        var items = queue.Dequeue(1);
        Assert.Single(items);
        Assert.Null(items[0].Batch.RepoName);
    }
}
