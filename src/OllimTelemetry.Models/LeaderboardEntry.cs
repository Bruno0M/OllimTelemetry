namespace OllimTelemetry.Models;

public sealed record LeaderboardEntry(
    int     Rank,
    string  UserId,
    long    TotalTokens,
    string? RepoName,
    string? GitHubLogin      = null,
    long    InputTokens      = 0,
    long    OutputTokens     = 0,
    long    CacheReadTokens  = 0,
    long    CacheWriteTokens = 0
);
