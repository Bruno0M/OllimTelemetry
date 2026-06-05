namespace OllimTelemetry.Core;

public static class XdgMigration
{
    /// <summary>
    /// Moves ~/.ollim/{config.json,queue.db} to XDG paths if the legacy dir exists.
    /// Returns true if a migration was performed.
    /// </summary>
    public static bool TryMigrate()
    {
        var legacyDir = OllimPaths.LegacyDir;
        if (!Directory.Exists(legacyDir)) return false;

        var legacyConfig = Path.Combine(legacyDir, "config.json");
        var legacyDb     = Path.Combine(legacyDir, "queue.db");

        if (File.Exists(legacyConfig) && !File.Exists(OllimPaths.ConfigFile))
        {
            Directory.CreateDirectory(OllimPaths.ConfigDir);
            File.Copy(legacyConfig, OllimPaths.ConfigFile);
        }

        if (File.Exists(legacyDb) && !File.Exists(OllimPaths.DbFile))
        {
            Directory.CreateDirectory(OllimPaths.DataDir);
            File.Copy(legacyDb, OllimPaths.DbFile);
        }

        Directory.Delete(legacyDir, recursive: true);
        return true;
    }
}
