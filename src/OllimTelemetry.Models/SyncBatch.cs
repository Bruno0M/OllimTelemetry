namespace OllimTelemetry.Models;

public sealed record SyncBatch(
    string Agent,
    long   InputTokens,
    long   OutputTokens,
    long   CacheReadTokens,
    long   CacheWriteTokens,
    string PeriodStart,
    string PeriodEnd
);
