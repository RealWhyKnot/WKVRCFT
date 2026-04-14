namespace VRCFaceTracking.V2;

/// <summary>
/// Module-specific settings that persist across sessions.
/// </summary>
public interface IModuleSettings
{
    /// <summary>
    /// Read a setting value, returning defaultValue if not found.
    /// </summary>
    T GetSetting<T>(string key, T defaultValue);

    /// <summary>
    /// Write a setting value.
    /// </summary>
    void SetSetting<T>(string key, T value);

    /// <summary>
    /// Persist all settings to disk.
    /// </summary>
    Task SaveAsync();
}
