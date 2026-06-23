using System.Text;
using System.Text.Json;

namespace OllimTelemetry.Core.Hook;

public static class CodexHookManager
{
    private const string HookEvent = "Stop";

    public static string HooksPath => Path.Combine(OllimPaths.CodexConfigDir, "hooks.json");

    public static bool IsCodexPresent() =>
        Directory.Exists(OllimPaths.CodexConfigDir) || Directory.Exists(OllimPaths.CodexSessionsRoot);

    public static bool IsInstalled(string command)
    {
        if (!File.Exists(HooksPath)) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(HooksPath));
            return HasHook(doc.RootElement, command);
        }
        catch { return false; }
    }

    public static bool IsAnyOllimHookInstalled()
    {
        if (!File.Exists(HooksPath)) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(HooksPath));
            return FindOllimHookCommand(doc.RootElement) is not null;
        }
        catch { return false; }
    }

    public static (bool Changed, string? Error) Install(string command)
    {
        try
        {
            string? existingJson = File.Exists(HooksPath) ? File.ReadAllText(HooksPath) : null;

            if (existingJson is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(existingJson);
                    if (HasHook(doc.RootElement, command))
                        return (false, null);

                    var stale = FindOllimHookCommand(doc.RootElement);
                    if (stale is not null)
                        existingJson = BuildJson(doc.RootElement, stale, add: false);
                }
                catch (JsonException)
                {
                    return (false, $"~/.codex/hooks.json contains invalid JSON — fix it manually, then run `ollim start` again.");
                }
            }

            var newJson = existingJson is not null
                ? MergeHook(existingJson, command)
                : NewHooksJson(command);

            Directory.CreateDirectory(Path.GetDirectoryName(HooksPath)!);
            if (File.Exists(HooksPath))
                File.Copy(HooksPath, HooksPath + ".bak", overwrite: true);

            File.WriteAllText(HooksPath, newJson);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static (bool Removed, string? Error) Uninstall(string command)
    {
        if (!File.Exists(HooksPath)) return (false, null);
        try
        {
            var existingJson = File.ReadAllText(HooksPath);
            using var doc = JsonDocument.Parse(existingJson);

            var toRemove = HasHook(doc.RootElement, command)
                ? command
                : FindOllimHookCommand(doc.RootElement);

            if (toRemove is null) return (false, null);

            var newJson = RemoveHook(existingJson, toRemove);
            File.Copy(HooksPath, HooksPath + ".bak", overwrite: true);
            File.WriteAllText(HooksPath, newJson);
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
        if (!root.TryGetProperty(HookEvent, out var groups)) return false;
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

    private static string? FindOllimHookCommand(JsonElement root)
    {
        if (!root.TryGetProperty(HookEvent, out var groups)) return null;
        if (groups.ValueKind != JsonValueKind.Array) return null;

        foreach (var group in groups.EnumerateArray())
        {
            if (!group.TryGetProperty("hooks", out var items)) continue;
            if (items.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in items.EnumerateArray())
                if (item.TryGetProperty("command", out var cmd) && IsOllimHookCommand(cmd.GetString()))
                    return cmd.GetString();
        }
        return null;
    }

    private static bool IsOllimHookCommand(string? command)
    {
        if (command is null) return false;
        // Matches "... ollim hook --agent codex" regardless of binary path or env prefix
        var trimmed = command.TrimEnd();
        if (!trimmed.Contains(" hook", StringComparison.OrdinalIgnoreCase)) return false;

        var hookIdx = trimmed.LastIndexOf(" hook", StringComparison.OrdinalIgnoreCase);
        var beforeHook = trimmed[..hookIdx].TrimEnd();
        var lastSpace  = beforeHook.LastIndexOf(' ');
        var binaryPart = lastSpace < 0 ? beforeHook : beforeHook[(lastSpace + 1)..];

        return string.Equals(Path.GetFileNameWithoutExtension(binaryPart), "ollim",
            StringComparison.OrdinalIgnoreCase);
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

    // hooks.json is purely the hooks object (no other settings to preserve).
    // Structure: { "Stop": [ { "matcher": "", "hooks": [ { "type": "command", "command": "..." } ] } ] }
    private static string NewHooksJson(string command)
    {
        using var ms = new MemoryStream();
        using var w  = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();
        WriteStopGroup(w, command);
        w.WriteEndObject();
        w.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string BuildJson(JsonElement root, string command, bool add)
    {
        using var ms = new MemoryStream();
        using var w  = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();

        bool wroteStop = false;
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name == HookEvent)
            {
                if (!add && !HasRemainingGroups(prop.Value, command))
                {
                    // omit empty Stop array
                }
                else
                {
                    w.WritePropertyName(HookEvent);
                    if (add)
                        WriteGroupsWithAdded(w, prop.Value, command);
                    else
                        WriteGroupsWithRemoved(w, prop.Value, command);
                }
                wroteStop = true;
            }
            else
            {
                prop.WriteTo(w);
            }
        }

        if (!wroteStop && add)
            WriteStopGroup(w, command);

        w.WriteEndObject();
        w.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static bool HasRemainingGroups(JsonElement existing, string command)
    {
        foreach (var group in existing.EnumerateArray())
        {
            if (!group.TryGetProperty("hooks", out var items)) return true;
            if (items.EnumerateArray().Any(h =>
                    !h.TryGetProperty("command", out var c) || c.GetString() != command))
                return true;
        }
        return false;
    }

    private static void WriteGroupsWithAdded(Utf8JsonWriter w, JsonElement existing, string command)
    {
        w.WriteStartArray();
        bool merged = false;
        foreach (var group in existing.EnumerateArray())
        {
            if (!merged
                && group.TryGetProperty("matcher", out var m) && m.GetString() == ""
                && group.TryGetProperty("hooks", out var items)
                && items.ValueKind == JsonValueKind.Array)
            {
                w.WriteStartObject();
                foreach (var p in group.EnumerateObject())
                {
                    if (p.Name == "hooks")
                    {
                        w.WritePropertyName("hooks");
                        w.WriteStartArray();
                        foreach (var h in items.EnumerateArray()) h.WriteTo(w);
                        WriteHookItem(w, command);
                        w.WriteEndArray();
                    }
                    else { p.WriteTo(w); }
                }
                w.WriteEndObject();
                merged = true;
            }
            else { group.WriteTo(w); }
        }
        if (!merged)
        {
            w.WriteStartObject();
            w.WriteString("matcher", "");
            w.WritePropertyName("hooks");
            w.WriteStartArray();
            WriteHookItem(w, command);
            w.WriteEndArray();
            w.WriteEndObject();
        }
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
                else { p.WriteTo(w); }
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    private static void WriteStopGroup(Utf8JsonWriter w, string command)
    {
        w.WritePropertyName(HookEvent);
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
