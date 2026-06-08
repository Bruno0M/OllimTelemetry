using System.Net;
using System.Text;
using System.Text.Json;
using OllimTelemetry.Cli.Commands;
using OllimTelemetry.Core.Config;

namespace OllimTelemetry.Tests;

public sealed class LinkCommandTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public LinkCommandTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose()     => Directory.Delete(_tempDir, recursive: true);

    private ConfigManager Manager() => new(_tempDir);

    // ── helpers ──────────────────────────────────────────────────────────────

    private static HttpResponseMessage JsonOk(object obj) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(obj),
                Encoding.UTF8, "application/json")
        };

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _queue = new();
        private Func<HttpResponseMessage>?                _default;
        public int CallCount { get; private set; }

        public void Enqueue(HttpResponseMessage r) => _queue.Enqueue(() => r);
        public void SetDefault(Func<HttpResponseMessage> factory) => _default = factory;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            if (_queue.Count > 0) return Task.FromResult(_queue.Dequeue()());
            if (_default is not null) return Task.FromResult(_default());
            throw new InvalidOperationException("No response configured");
        }
    }

    private static readonly object DeviceOk = new
    {
        device_code      = "dc123",
        user_code        = "ABCD-EFGH",
        verification_uri = "https://github.com/login/device",
        interval         = 0,   // no delay in tests
    };

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_DevGuard_ReturnsOneWithoutHttpCall()
    {
        var handler = new FakeHandler();
        using var http = new HttpClient(handler);
        var manager    = Manager();
        var config     = new AppConfig { BackendUrl = "https://api.ollim.dev" };

        var result = await LinkCommand.ExecuteAsync(http, manager, config, "dev", TimeSpan.FromMinutes(5));

        Assert.Equal(1, result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_SavesConfigAndReturnsZero()
    {
        var handler = new FakeHandler();
        handler.Enqueue(JsonOk(DeviceOk));
        handler.Enqueue(JsonOk(new { status = "pending" }));
        handler.Enqueue(JsonOk(new { status = "pending" }));
        handler.Enqueue(JsonOk(new { status = "complete", session_token = "tok123", github_login = "testuser" }));

        using var http = new HttpClient(handler);
        var manager    = Manager();
        var config     = new AppConfig { BackendUrl = "http://localhost:5000" };

        var result = await LinkCommand.ExecuteAsync(http, manager, config, null, TimeSpan.FromMinutes(5));

        Assert.Equal(0, result);
        var saved = manager.LoadOrCreate();
        Assert.Equal("tok123",   saved.SessionToken);
        Assert.Equal("testuser", saved.GitHubLogin);
        Assert.True(saved.ShareGlobal);
    }

    [Fact]
    public async Task ExecuteAsync_Expired_ReturnsOne()
    {
        var handler = new FakeHandler();
        handler.Enqueue(JsonOk(DeviceOk));
        handler.Enqueue(JsonOk(new { status = "expired" }));

        using var http = new HttpClient(handler);
        var manager    = Manager();
        var config     = new AppConfig { BackendUrl = "http://localhost:5000" };

        var result = await LinkCommand.ExecuteAsync(http, manager, config, null, TimeSpan.FromMinutes(5));

        Assert.Equal(1, result);
        Assert.Null(manager.LoadOrCreate().SessionToken);
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ReturnsOne()
    {
        var handler = new FakeHandler();
        handler.Enqueue(JsonOk(DeviceOk));
        handler.SetDefault(() => JsonOk(new { status = "pending" }));

        using var http = new HttpClient(handler);
        var manager    = Manager();
        var config     = new AppConfig { BackendUrl = "http://localhost:5000" };

        // maxWait so short the loop exits before getting a complete
        var result = await LinkCommand.ExecuteAsync(
            http, manager, config, null, TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, result);
        Assert.Null(manager.LoadOrCreate().SessionToken);
    }
}
