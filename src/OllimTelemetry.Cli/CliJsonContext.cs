using System.Text.Json.Serialization;
using OllimTelemetry.Cli.Auth;
using OllimTelemetry.Cli.Commands;
using OllimTelemetry.Cli.Update;
using OllimTelemetry.Models;

namespace OllimTelemetry.Cli;

[JsonSerializable(typeof(LeaderboardResponse))]
[JsonSerializable(typeof(StopHookInput))]
[JsonSerializable(typeof(UpdateCheckCache))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(LinkDeviceResponse))]
[JsonSerializable(typeof(LinkPollRequest))]
[JsonSerializable(typeof(LinkPollResponse))]
internal partial class CliJsonContext : JsonSerializerContext { }
