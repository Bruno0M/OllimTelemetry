namespace OllimTelemetry.Models;

public sealed record TokenUsage(
    string   Agent,
    long     InputTokens,
    long     OutputTokens,
    long     CacheReadTokens,
    long     CacheWriteTokens,
    DateTime Timestamp
);
