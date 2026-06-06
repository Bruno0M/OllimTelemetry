using System.Text.Json.Serialization;

namespace OllimTelemetry.Cli.Commands;

/// <summary>JSON payload Claude Code sends to Stop hooks via stdin.</summary>
internal sealed class StopHookInput
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("transcript_path")]
    public string? TranscriptPath { get; set; }
}
