using System.Text.Json.Serialization;

namespace OllimTelemetry.Cli.Auth;

internal sealed record LinkDeviceResponse(
    [property: JsonPropertyName("device_code")]      string DeviceCode,
    [property: JsonPropertyName("user_code")]        string UserCode,
    [property: JsonPropertyName("verification_uri")] string VerificationUri,
    [property: JsonPropertyName("interval")]         int    Interval
);

internal sealed record LinkPollRequest(
    [property: JsonPropertyName("device_code")] string DeviceCode
);

internal sealed record LinkPollResponse(
    [property: JsonPropertyName("status")]        string  Status,
    [property: JsonPropertyName("session_token")] string? SessionToken,
    [property: JsonPropertyName("github_login")]  string? GitHubLogin
);
