using System.Text.Json;
using VRCFaceTracking.V2;

namespace VRCFaceTracking.ModuleHostV2;

/// <summary>
/// Implements IModuleSettings using a local JSON file in the module's directory.
/// No pipe round-trip needed — the module host process has direct filesystem access.
/// </summary>
public class V2ModuleSettings : IModuleSettings
{
    private readonly string _settingsFilePath;
    private Dictionary<string, JsonElement> _data = new();
    private bool _dirty = false;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public V2ModuleSettings(string moduleDirPath)
    {
        _settingsFilePath = Path.Combine(moduleDirPath, "settings.json");
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_settingsFilePath)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_settingsFilePath));
            _data = doc.RootElement.Deserialize<Dictionary<string, JsonElement>>()
                    ?? new Dictionary<string, JsonElement>();
        }
        catch { /* corrupt settings — start fresh */ }
    }

    public T GetSetting<T>(string key, T defaultValue)
    {
        if (!_data.TryGetValue(key, out var el)) return defaultValue;
        try { return el.Deserialize<T>() ?? defaultValue; }
        catch { return defaultValue; }
    }

    public void SetSetting<T>(string key, T value)
    {
        // Round-trip through JSON to get a JsonElement
        var json = JsonSerializer.Serialize(value);
        using var doc = JsonDocument.Parse(json);
        _data[key] = doc.RootElement.Clone();
        _dirty = true;
    }

    /// <summary>
    /// Apply a host-pushed settings dictionary. Returns the keys that actually changed
    /// (compared by JSON equality); keys absent from the new dict are left alone.
    /// Doesn't mark the store dirty — the host has already persisted the values.
    /// </summary>
    public IReadOnlyList<string> ApplyFromHost(Dictionary<string, JsonElement> incoming)
    {
        var changed = new List<string>();
        foreach (var (k, v) in incoming)
        {
            if (!_data.TryGetValue(k, out var existing) ||
                !JsonElement.DeepEquals(existing, v))
            {
                _data[k] = v.Clone();
                changed.Add(k);
            }
        }
        return changed;
    }

    public async Task SaveAsync()
    {
        if (!_dirty) return;
        try
        {
            var json = JsonSerializer.Serialize(_data, JsonOpts);
            await File.WriteAllTextAsync(_settingsFilePath, json);
            _dirty = false;
        }
        catch { /* non-fatal */ }
    }
}
