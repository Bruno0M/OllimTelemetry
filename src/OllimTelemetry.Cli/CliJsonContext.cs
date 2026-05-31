using System.Text.Json.Serialization;
using OllimTelemetry.Models;

namespace OllimTelemetry.Cli;

[JsonSerializable(typeof(LeaderboardResponse))]
internal partial class CliJsonContext : JsonSerializerContext { }
