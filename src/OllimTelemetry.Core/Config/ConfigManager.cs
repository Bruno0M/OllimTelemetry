using System.Text.Json;

namespace OllimTelemetry.Core.Config;

public sealed class ConfigManager
{
    private static readonly string ConfigDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollim");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public AppConfig LoadOrCreate()
    {
        if (!File.Exists(ConfigPath))
        {
            var fresh = new AppConfig();
            Save(fresh);
            return fresh;
        }

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig) ?? new AppConfig();
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.AppConfig);
        File.WriteAllText(ConfigPath, json);
    }

    public static string ConfigFilePath => ConfigPath;
}
