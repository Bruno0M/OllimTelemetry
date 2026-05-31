namespace OllimTelemetry.Core.Parsing;

public static class ProjectPathResolver
{
    public static string? Resolve(string filePath)
    {
        try
        {
            var hashDir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(hashDir)) return null;

            var hashName = Path.GetFileName(hashDir);
            if (string.IsNullOrEmpty(hashName)) return null;

            var decoded = hashName.Replace('-', '/');
            var name    = Path.GetFileName(decoded.TrimEnd('/'));
            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }
}
