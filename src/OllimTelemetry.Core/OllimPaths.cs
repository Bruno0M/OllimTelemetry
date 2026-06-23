namespace OllimTelemetry.Core;

public static class OllimPaths
{
    private static readonly string Home =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string ConfigDir =>
        Path.Combine(
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(Home, ".config"),
            "ollim");

    public static string DataDir =>
        Path.Combine(
            Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                ?? Path.Combine(Home, ".local", "share"),
            "ollim");

    public static string ConfigFile => Path.Combine(ConfigDir, "config.json");
    public static string DbFile     => Path.Combine(DataDir, "queue.db");

    public static string LegacyDir  => Path.Combine(Home, ".ollim");

    public static string ClaudeProjectsRoot => Path.Combine(Home, ".claude", "projects");

    private static string CodexHome =>
        Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(Home, ".codex");

    public static string CodexConfigDir  => CodexHome;
    public static string CodexSessionsRoot => Path.Combine(CodexHome, "sessions");
}
