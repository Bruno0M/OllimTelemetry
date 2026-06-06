using System.Text.Json;

namespace OllimTelemetry.Core.Config;

public sealed class ConfigManager
{
    private readonly string _configDir;
    private readonly string _configPath;

    public ConfigManager(string? configDir = null)
    {
        _configDir  = configDir ?? OllimPaths.ConfigDir;
        _configPath = configDir is not null
            ? Path.Combine(configDir, "config.json")
            : OllimPaths.ConfigFile;
    }

    public AppConfig LoadOrCreate()
    {
        AppConfig config;

        if (!File.Exists(_configPath))
        {
            config = new AppConfig();
            Save(config);
        }
        else
        {
            var json = File.ReadAllText(_configPath);
            config = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig) ?? new AppConfig();
        }

        var backendUrlOverride = Environment.GetEnvironmentVariable("OLLIM_BACKEND_URL");
        if (!string.IsNullOrWhiteSpace(backendUrlOverride))
            config = config with { BackendUrl = backendUrlOverride };

        return config;
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(_configDir);
        var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.AppConfig);
        File.WriteAllText(_configPath, json);
    }

    public string ConfigFilePath => _configPath;

    public static string DefaultConfigFilePath => OllimPaths.ConfigFile;
}
