using System.Text;
using System.Text.Json;

namespace OllimTelemetry.Core.Hook;

public static class ClaudeHookManager
{
    private const string HookEvent = "Stop";

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "settings.json");

    public static bool IsInstalled(string command)
    {
        if (!File.Exists(SettingsPath)) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            return HasHook(doc.RootElement, command);
        }
        catch { return false; }
    }

    public static (bool Changed, string? Error) Install(string command)
    {
        try
        {
            var path = SettingsPath;
            string? existingJson = File.Exists(path) ? File.ReadAllText(path) : null;

            if (existingJson is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(existingJson);
                    if (HasHook(doc.RootElement, command))
                        return (false, null);
                }
                catch (JsonException) { existingJson = null; }
            }

            var newJson = existingJson is not null
                ? MergeHook(existingJson, command)
                : NewSettingsJson(command);

            if (File.Exists(path))
                File.Copy(path, path + ".bak", overwrite: true);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, newJson);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static (bool Removed, string? Error) Uninstall(string command)
    {
        var path = SettingsPath;
        if (!File.Exists(path)) return (false, null);

        try
        {
            var existingJson = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(existingJson);
            if (!HasHook(doc.RootElement, command))
                return (false, null);

            var newJson = RemoveHook(existingJson, command);
            File.Copy(path, path + ".bak", overwrite: true);
            File.WriteAllText(path, newJson);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static bool HasHook(JsonElement root, string command)
    {
        if (!root.TryGetProperty("hooks", out var hooks)) return false;
        if (!hooks.TryGetProperty(HookEvent, out var groups)) return false;
        if (groups.ValueKind != JsonValueKind.Array) return false;

        foreach (var group in groups.EnumerateArray())
        {
            if (!group.TryGetProperty("hooks", out var items)) continue;
            if (items.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in items.EnumerateArray())
                if (item.TryGetProperty("command", out var cmd) && cmd.GetString() == command)
                    return true;
        }
        return false;
    }

    private static string MergeHook(string existingJson, string command)
    {
        using var doc = JsonDocument.Parse(existingJson);
        return BuildJson(doc.RootElement, command, add: true);
    }

    private static string RemoveHook(string existingJson, string command)
    {
        using var doc = JsonDocument.Parse(existingJson);
        return BuildJson(doc.RootElement, command, add: false);
    }

    private static string NewSettingsJson(string command)
    {
        using var ms = new MemoryStream();
        using var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();
        WriteHookEventGroup(w, command);
        w.WriteEndObject();
        w.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // Rewrites the entire settings doc, adding or removing our hook entry.
    private static string BuildJson(JsonElement root, string command, bool add)
    {
        using var ms = new MemoryStream();
        using var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });

        w.WriteStartObject();

        bool wroteHooks = false;
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name == "hooks")
            {
                w.WritePropertyName("hooks");
                WriteHooksSection(w, prop.Value, command, add);
                wroteHooks = true;
            }
            else
            {
                prop.WriteTo(w);
            }
        }

        if (!wroteHooks && add)
            WriteHookEventGroup(w, command);

        w.WriteEndObject();
        w.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteHooksSection(Utf8JsonWriter w, JsonElement hooks, string command, bool add)
    {
        w.WriteStartObject();

        bool wroteEvent = false;
        foreach (var prop in hooks.EnumerateObject())
        {
            if (prop.Name == HookEvent)
            {
                w.WritePropertyName(HookEvent);
                if (add)
                    WriteGroupsWithAdded(w, prop.Value, command);
                else
                    WriteGroupsWithRemoved(w, prop.Value, command);
                wroteEvent = true;
            }
            else
            {
                prop.WriteTo(w);
            }
        }

        if (!wroteEvent && add)
        {
            w.WritePropertyName(HookEvent);
            WriteSingleGroup(w, command);
        }

        w.WriteEndObject();
    }

    private static void WriteGroupsWithAdded(Utf8JsonWriter w, JsonElement existing, string command)
    {
        w.WriteStartArray();
        foreach (var item in existing.EnumerateArray())
            item.WriteTo(w);
        w.WriteStartObject();
        w.WriteString("matcher", "");
        w.WritePropertyName("hooks");
        w.WriteStartArray();
        WriteHookItem(w, command);
        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndArray();
    }

    private static void WriteGroupsWithRemoved(Utf8JsonWriter w, JsonElement existing, string command)
    {
        w.WriteStartArray();
        foreach (var group in existing.EnumerateArray())
        {
            if (!group.TryGetProperty("hooks", out var items))
            {
                group.WriteTo(w);
                continue;
            }

            var remaining = items.EnumerateArray()
                .Where(h => !h.TryGetProperty("command", out var c) || c.GetString() != command)
                .ToList();

            if (remaining.Count == 0) continue;

            w.WriteStartObject();
            foreach (var p in group.EnumerateObject())
            {
                if (p.Name == "hooks")
                {
                    w.WritePropertyName("hooks");
                    w.WriteStartArray();
                    foreach (var h in remaining) h.WriteTo(w);
                    w.WriteEndArray();
                }
                else
                {
                    p.WriteTo(w);
                }
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    // Writes:  "Stop": [{ "matcher": "", "hooks": [{ "type": "command", "command": "..." }] }]
    private static void WriteHookEventGroup(Utf8JsonWriter w, string command)
    {
        w.WritePropertyName("hooks");
        w.WriteStartObject();
        w.WritePropertyName(HookEvent);
        WriteSingleGroup(w, command);
        w.WriteEndObject();
    }

    private static void WriteSingleGroup(Utf8JsonWriter w, string command)
    {
        w.WriteStartArray();
        w.WriteStartObject();
        w.WriteString("matcher", "");
        w.WritePropertyName("hooks");
        w.WriteStartArray();
        WriteHookItem(w, command);
        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndArray();
    }

    private static void WriteHookItem(Utf8JsonWriter w, string command)
    {
        w.WriteStartObject();
        w.WriteString("type", "command");
        w.WriteString("command", command);
        w.WriteEndObject();
    }
}
