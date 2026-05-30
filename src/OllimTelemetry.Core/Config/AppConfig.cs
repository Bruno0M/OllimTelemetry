namespace OllimTelemetry.Core.Config;

public sealed record AppConfig
{
    public int     Version             { get; init; } = 1;
    public string  UserId              { get; init; } = Guid.NewGuid().ToString();
    public bool    ShareGlobal         { get; init; } = false;
    public bool    ShareRepoName       { get; init; } = false;
    public int     SyncIntervalMinutes { get; init; } = 5;
    public string  Agent               { get; init; } = "claude-code";
    public string  BackendUrl          { get; init; } = "https://api.ollim.dev";
    public string  CreatedAt           { get; init; } = DateTime.UtcNow.ToString("O");
    public string? LastSyncAt          { get; init; } = null;
}
