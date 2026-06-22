namespace OllimTelemetry.Models;

public sealed record SubmitPayload(
    string  UserId,
    string  Agent,
    long    InputTokens,
    long    OutputTokens,
    long    CacheReadTokens,
    long    CacheWriteTokens,
    string  PeriodStart,
    string  PeriodEnd,
    string  ClientVersion,
    string? RepoName = null,
    string? ModelId  = null
);
