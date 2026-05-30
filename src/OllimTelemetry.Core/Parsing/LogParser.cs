using System.Text.Json;
using OllimTelemetry.Models;

namespace OllimTelemetry.Core.Parsing;

public sealed class LogParser
{
    public IReadOnlyList<TokenUsage> Parse(string filePath, long startOffset, out long newOffset, string agent)
    {
        newOffset = startOffset;

        if (!File.Exists(filePath))
            return [];

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(startOffset, SeekOrigin.Begin);

        var results = new List<TokenUsage>();
        using var reader = new StreamReader(stream, leaveOpen: false);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            newOffset = stream.Position;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            TryParseLine(line, agent, results);
        }

        return results;
    }

    private static void TryParseLine(string line, string agent, List<TokenUsage> results)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (!root.TryGetProperty("usage", out var usage)) // PRIVACY: usage only
                return;

            // PRIVACY: timestamp only — no content
            var timestamp = root.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String
                ? DateTime.Parse(ts.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : DateTime.UtcNow;

            var inputTokens      = usage.TryGetProperty("input_tokens",                  out var i)   ? i.GetInt64()  : 0; // PRIVACY: usage only
            var outputTokens     = usage.TryGetProperty("output_tokens",                 out var o)   ? o.GetInt64()  : 0; // PRIVACY: usage only
            var cacheWriteTokens = usage.TryGetProperty("cache_creation_input_tokens",   out var cw)  ? cw.GetInt64() : 0; // PRIVACY: usage only
            var cacheReadTokens  = usage.TryGetProperty("cache_read_input_tokens",       out var cr)  ? cr.GetInt64() : 0; // PRIVACY: usage only

            results.Add(new TokenUsage(agent, inputTokens, outputTokens, cacheReadTokens, cacheWriteTokens, timestamp));
        }
        catch (JsonException)
        {
            // malformed line — skip silently
        }
    }
}
