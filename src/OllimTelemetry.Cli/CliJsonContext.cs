using System.Text.Json.Serialization;
using OllimTelemetry.Cli.Update;
using OllimTelemetry.Models;

namespace OllimTelemetry.Cli;

[JsonSerializable(typeof(LeaderboardResponse))]
[JsonSerializable(typeof(UpdateCheckCache))]
[JsonSerializable(typeof(GitHubRelease))]
internal partial class CliJsonContext : JsonSerializerContext { }
