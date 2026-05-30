using System.Text.Json.Serialization;
using OllimTelemetry.Models;

namespace OllimTelemetry.Core.Config;

[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(SubmitPayload))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class ConfigJsonContext : JsonSerializerContext { }
