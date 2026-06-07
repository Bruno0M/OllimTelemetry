using OllimTelemetry.Core.Config;

namespace OllimTelemetry.Tests;

public sealed class ConfigManagerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public ConfigManagerTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private ConfigManager Manager() => new(_tempDir);

    [Fact]
    public void LoadOrCreate_CreatesConfigWithSafeDefaults()
    {
        var manager = Manager();
        var config  = manager.LoadOrCreate();

        Assert.False(config.ShareGlobal);
        Assert.False(config.ShareRepoName);
        Assert.Equal(1,             config.Version);
        Assert.Equal("claude-code", config.Agent);
        Assert.NotNull(config.UserId);
        Assert.Null(config.LastSyncAt);
        Assert.True(File.Exists(manager.ConfigFilePath));
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var manager  = Manager();
        var original = new AppConfig { ShareGlobal = true, LastSyncAt = "2026-01-01T00:00:00Z" };

        manager.Save(original);
        var loaded = manager.LoadOrCreate();

        Assert.Equal(original.ShareGlobal, loaded.ShareGlobal);
        Assert.Equal(original.LastSyncAt,  loaded.LastSyncAt);
        Assert.Equal(original.UserId,      loaded.UserId);
    }

    [Fact]
    public void LoadOrCreate_ExistingConfig_DoesNotOverwrite()
    {
        var manager = Manager();
        var saved   = new AppConfig { ShareGlobal = true };
        manager.Save(saved);

        var loaded = manager.LoadOrCreate();

        Assert.True(loaded.ShareGlobal);
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        var dir     = Path.Combine(_tempDir, "nested", "path");
        var manager = new ConfigManager(dir);

        manager.Save(new AppConfig());

        Assert.True(File.Exists(Path.Combine(dir, "config.json")));
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips_SessionTokenAndGitHubLogin()
    {
        var manager  = Manager();
        var original = new AppConfig
        {
            ShareGlobal  = true,
            SessionToken = "tok-abc123",
            GitHubLogin  = "testuser",
        };

        manager.Save(original);
        var loaded = manager.LoadOrCreate();

        Assert.Equal(original.SessionToken, loaded.SessionToken);
        Assert.Equal(original.GitHubLogin,  loaded.GitHubLogin);
        Assert.Equal(original.UserId,       loaded.UserId);
    }
}
