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
        if (!File.Exists(_configPath))
        {
            var fresh = new AppConfig();
            Save(fresh);
            return fresh;
        }

        var json = File.ReadAllText(_configPath);
        return JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig) ?? new AppConfig();
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
