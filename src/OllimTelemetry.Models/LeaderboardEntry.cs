namespace OllimTelemetry.Models;

public sealed record LeaderboardEntry(int Rank, string UserId, long TotalTokens, string? RepoName);
