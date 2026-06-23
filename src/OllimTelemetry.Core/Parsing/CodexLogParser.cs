using System.Text.Json;
using OllimTelemetry.Models;

namespace OllimTelemetry.Core.Parsing;

public sealed class CodexLogParser
{
    // Returns the last cumulative token total found from startOffset to EOF,
    // and the new byte offset. LastCumulative is null when no token_count events exist in the delta.
    public (TokenUsage? LastCumulative, long NewOffset) Parse(string filePath, long startOffset)
    {
        if (!File.Exists(filePath))
            return (null, startOffset);

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(startOffset, SeekOrigin.Begin);

        long newOffset = startOffset;
        TokenUsage? lastCumulative = null;

        using var reader = new StreamReader(stream, leaveOpen: false);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            newOffset = stream.Position;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var usage = TryParseTokenCount(line);
            if (usage is not null)
                lastCumulative = usage;
        }

        return (lastCumulative, newOffset);
    }

    private static TokenUsage? TryParseTokenCount(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            // PRIVACY: usage only
            // Handles two known layouts:
            //   {"type":"event_msg","payload":{"type":"token_count","info":{...}}}
            //   {"type":"token_count","info":{...}}
            // PRIVACY: usage only
            // Handles two known layouts:
            //   {"type":"event_msg","payload":{"type":"token_count","info":{...}}}
            //   {"type":"token_count","info":{...}}
            JsonElement infoEl;
            JsonElement payloadOrRoot;
            if (root.TryGetProperty("payload", out var payload)
                && payload.TryGetProperty("type", out var payloadType)
                && payloadType.GetString() == "token_count"
                && payload.TryGetProperty("info", out infoEl))
            {
                payloadOrRoot = payload;
            }
            else if (root.TryGetProperty("type", out var rootType)
                     && rootType.GetString() == "token_count"
                     && root.TryGetProperty("info", out infoEl))
            {
                payloadOrRoot = root;
            }
            else
            {
                return null;
            }

            // info can be null in early Codex versions — skip those events
            if (infoEl.ValueKind != JsonValueKind.Object)
                return null;

            if (!infoEl.TryGetProperty("total_token_usage", out var total)
                || total.ValueKind != JsonValueKind.Object)
                return null;

            // PRIVACY: timestamp only — no content
            var timestamp = root.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String
                ? DateTime.Parse(ts.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : DateTime.UtcNow;

            // PRIVACY: model name only — no content
            string? modelId = payloadOrRoot.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : null;

            var inputTokens     = total.TryGetProperty("input_tokens",            out var i) ? i.GetInt64() : 0; // PRIVACY: usage only
            var cachedTokens    = total.TryGetProperty("cached_input_tokens",     out var c) ? c.GetInt64() : 0; // PRIVACY: usage only
            var outputTokens    = total.TryGetProperty("output_tokens",           out var o) ? o.GetInt64() : 0; // PRIVACY: usage only
            var reasoningTokens = total.TryGetProperty("reasoning_output_tokens", out var r) ? r.GetInt64() : 0; // PRIVACY: usage only

            return new TokenUsage(
                "codex",
                inputTokens,
                outputTokens + reasoningTokens, // reasoning billed as output
                cachedTokens,
                0,
                timestamp,
                modelId
            );
        }
        catch (Exception)
        {
            // malformed line or unexpected JSON structure (e.g. info:null) — skip silently
            return null;
        }
    }
}
