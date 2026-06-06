using System.Text.Json.Serialization;
using OllimTelemetry.Cli.Commands;
using OllimTelemetry.Models;

namespace OllimTelemetry.Cli;

[JsonSerializable(typeof(LeaderboardResponse))]
[JsonSerializable(typeof(StopHookInput))]
internal partial class CliJsonContext : JsonSerializerContext { }
