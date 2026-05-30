using System.Text.Json;

namespace OllimTelemetry.Core.Config;

public sealed class ConfigManager
{
    private static readonly string DefaultConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollim");

    private readonly string _configDir;
    private readonly string _configPath;

    public ConfigManager(string? configDir = null)
    {
        _configDir  = configDir ?? DefaultConfigDir;
        _configPath = Path.Combine(_configDir, "config.json");
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

    public static string DefaultConfigFilePath =>
        Path.Combine(DefaultConfigDir, "config.json");
}
