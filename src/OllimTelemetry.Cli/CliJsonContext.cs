using System.Text.Json.Serialization;
using OllimTelemetry.Cli.Update;
using OllimTelemetry.Models;

namespace OllimTelemetry.Cli;

[JsonSerializable(typeof(LeaderboardResponse))]
[JsonSerializable(typeof(StopHookInput))]
[JsonSerializable(typeof(UpdateCheckCache))]
[JsonSerializable(typeof(GitHubRelease))]
internal partial class CliJsonContext : JsonSerializerContext { }

/// <summary>JSON payload Claude Code sends to Stop hooks via stdin.</summary>
internal sealed class StopHookInput
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("transcript_path")]
    public string? TranscriptPath { get; set; }
}
