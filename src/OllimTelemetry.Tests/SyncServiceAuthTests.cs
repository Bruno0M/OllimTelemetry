using System.Net;
using System.Net.Http.Headers;
using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Queue;
using OllimTelemetry.Core.Sync;
using OllimTelemetry.Models;

namespace OllimTelemetry.Tests;

public sealed class SyncServiceAuthTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _dbPath;
    private readonly string _configDir;

    public SyncServiceAuthTests()
    {
        Directory.CreateDirectory(_tempDir);
        _dbPath    = Path.Combine(_tempDir, "queue.db");
        _configDir = Path.Combine(_tempDir, "config");
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private SyncQueue     Queue()   => new(_dbPath);
    private ConfigManager Manager() => new(_configDir);

    private static SyncBatch Batch() =>
        new("claude-code", 100, 50, 10, 0, "2026-01-01T00:00:00Z", "2026-01-01T00:05:00Z");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _fn;
        public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage req, CancellationToken ct) => Task.FromResult(_fn(req));
    }

    // ── tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FlushOnceAsync_WithSessionToken_SendsBearerHeader()
    {
        using var queue = Queue();
        queue.Enqueue(Batch());

        var manager = Manager();
        manager.Save(new AppConfig { ShareGlobal = true, SessionToken = "my-token" });

        AuthenticationHeaderValue? captured = null;
        using var http = new HttpClient(new CapturingHandler(req =>
        {
            captured = req.Headers.Authorization;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await new SyncService(manager, queue, http).FlushOnceAsync();

        Assert.NotNull(captured);
        Assert.Equal("Bearer",   captured!.Scheme);
        Assert.Equal("my-token", captured.Parameter);
    }

    [Fact]
    public async Task FlushOnceAsync_WithoutSessionToken_SendsNoAuthHeader()
    {
        using var queue = Queue();
        queue.Enqueue(Batch());

        var manager = Manager();
        manager.Save(new AppConfig { ShareGlobal = true });

        AuthenticationHeaderValue? captured = null;
        using var http = new HttpClient(new CapturingHandler(req =>
        {
            captured = req.Headers.Authorization;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await new SyncService(manager, queue, http).FlushOnceAsync();

        Assert.Null(captured);
    }

    [Fact]
    public async Task FlushOnceAsync_On401_ClearsSessionTokenAndLogin()
    {
        using var queue = Queue();
        queue.Enqueue(Batch());

        var manager = Manager();
        manager.Save(new AppConfig
        {
            ShareGlobal  = true,
            SessionToken = "tok123",
            GitHubLogin  = "testuser",
        });

        using var http = new HttpClient(new CapturingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        await new SyncService(manager, queue, http).FlushOnceAsync();

        var config = manager.LoadOrCreate();
        Assert.Null(config.SessionToken);
        Assert.Null(config.GitHubLogin);
    }

    [Fact]
    public async Task FlushOnceAsync_On401_LeavesUnsentBatchInQueue()
    {
        using var queue = Queue();
        queue.Enqueue(Batch());

        var manager = Manager();
        manager.Save(new AppConfig { ShareGlobal = true, SessionToken = "tok" });

        using var http = new HttpClient(new CapturingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        await new SyncService(manager, queue, http).FlushOnceAsync();

        // Batch must remain pending for the next flush cycle
        Assert.Equal(1, queue.CountPending());
    }
}
