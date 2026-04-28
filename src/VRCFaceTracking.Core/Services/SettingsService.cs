using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VRCFaceTracking.Core.Services;

public class AppConfig
{
    public bool DebugMode { get; set; }
    public string Theme { get; set; } = "dark";

    /// <summary>
    /// URL the host queries for the third-party module registry. Defaults to
    /// upstream's registry. Override in app_config.json (or via the UI, once exposed)
    /// to point at a self-hosted registry or a static manifest list.
    /// </summary>
    public string RegistryUrl { get; set; } = "https://registry.vrcft.io/modules";
}

public class OscTargetConfig
{
    [Required]
    public string Ip { get; set; } = "127.0.0.1";

    [Range(1, 65535)]
    public int SendPort { get; set; } = 9000;

    [Range(1, 65535)]
    public int RecvPort { get; set; } = 9001;

    /// <summary>True iff every field is valid. Cheap to call from the IPC handler before persisting.</summary>
    public bool IsValid(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Ip))
        {
            error = "OSC IP must not be empty.";
            return false;
        }
        // Accept any string that parses as an IPAddress, plus the literal "localhost".
        if (!string.Equals(Ip, "localhost", StringComparison.OrdinalIgnoreCase) && !IPAddress.TryParse(Ip, out _))
        {
            error = $"OSC IP '{Ip}' is not a valid IP address.";
            return false;
        }
        if (SendPort is < 1 or > 65535)
        {
            error = $"OSC SendPort {SendPort} must be between 1 and 65535.";
            return false;
        }
        if (RecvPort is < 1 or > 65535)
        {
            error = $"OSC RecvPort {RecvPort} must be between 1 and 65535.";
            return false;
        }
        error = null;
        return true;
    }
}

public class ModuleConfig
{
    public Dictionary<string, bool> EnabledModules { get; set; } = new();
    public Dictionary<string, string> CapabilityAssignments { get; set; } = new();
}

[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(OscTargetConfig))]
[JsonSerializable(typeof(ModuleConfig))]
internal partial class SettingsJsonContext : JsonSerializerContext { }

public class SettingsService
{
    private readonly string _settingsDir;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppConfig AppConfig { get; private set; } = new();
    public OscTargetConfig OscTarget { get; private set; } = new();
    public ModuleConfig Modules { get; private set; } = new();

    public SettingsService(string baseDir)
    {
        _settingsDir = Path.Combine(baseDir, "settings");
        Directory.CreateDirectory(_settingsDir);
        Load();
    }

    public void Load()
    {
        AppConfig = LoadFile<AppConfig>("app_config.json") ?? new AppConfig();
        OscTarget = LoadFile<OscTargetConfig>("osc_target.json") ?? new OscTargetConfig();
        Modules = LoadFile<ModuleConfig>("modules.json") ?? new ModuleConfig();
    }

    public void Save()
    {
        SaveFile("app_config.json", AppConfig);
        SaveFile("osc_target.json", OscTarget);
        SaveFile("modules.json", Modules);
    }

    public void SaveAppConfig()
    {
        SaveFile("app_config.json", AppConfig);
    }

    public void SaveOscTarget()
    {
        SaveFile("osc_target.json", OscTarget);
    }

    public void SaveModules()
    {
        SaveFile("modules.json", Modules);
    }

    private T? LoadFile<T>(string fileName) where T : class
    {
        var path = Path.Combine(_settingsDir, fileName);
        if (!File.Exists(path)) return null;

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            LogService.AddEntry(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "Warning",
                Source = "SettingsService",
                Message = $"Cannot read {fileName}: {ex.Message}. Using defaults."
            });
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            // File exists but is not valid JSON — most likely a partial write.
            // Save the corrupt file alongside the original so the user can inspect it.
            string corruptPath = path + ".corrupted";
            try { File.Copy(path, corruptPath, overwrite: true); } catch { }

            LogService.AddEntry(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "Warning",
                Source = "SettingsService",
                Message = $"{fileName} is corrupted and could not be loaded ({ex.Message}). " +
                          $"Falling back to defaults. A copy of the corrupt file was saved to {Path.GetFileName(corruptPath)}."
            });
            return null;
        }
    }

    private void SaveFile<T>(string fileName, T data)
    {
        var path    = Path.Combine(_settingsDir, fileName);
        var tmpPath = path + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            // Write to a temp file then atomically rename so a crash mid-write
            // cannot produce a partially-written (corrupt) settings file.
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, path, overwrite: true);
        }
        catch (Exception ex)
        {
            LogService.AddEntry(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "Error",
                Source = "SettingsService",
                Message = "Failed to save " + fileName + ": " + ex.Message
            });
        }
        finally
        {
            // Clean up the temp file if it survived (e.g. Move threw after WriteAllText)
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
        }
    }
}