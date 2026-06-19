using System.Text.Json.Serialization;

namespace OllimTelemetry.Cli.Auth;

internal sealed record CliInitResponse(
    [property: JsonPropertyName("state_token")]      string StateToken,
    [property: JsonPropertyName("verification_url")] string VerificationUrl,
    [property: JsonPropertyName("expires_in")]       int    ExpiresIn
);

internal sealed record CliPollResponse(
    [property: JsonPropertyName("status")]        string  Status,
    [property: JsonPropertyName("session_token")] string? SessionToken,
    [property: JsonPropertyName("github_login")]  string? GitHubLogin
);
