using Microsoft.Extensions.Logging;
using VRCFaceTracking.V2.Configuration;

namespace VRCFaceTracking.V2;

/// <summary>
/// Context provided to V2 modules during initialization.
/// Provides access to logging, settings, tracking data, and events.
/// </summary>
public interface IModuleContext
{
    /// <summary>
    /// Logger instance for this module.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Module-specific persistent settings.
    /// </summary>
    IModuleSettings Settings { get; }

    /// <summary>
    /// Write access to tracking data (expressions, eye, head).
    /// </summary>
    ITrackingDataWriter TrackingData { get; }

    /// <summary>
    /// Publish an event from this module to the host application.
    /// </summary>
    void PublishEvent(string eventType, object? payload = null);

    /// <summary>
    /// Register a configuration schema that the host will render as a settings UI.
    /// </summary>
    void RegisterConfigSchema(ConfigSchema schema);

    /// <summary>
    /// Raised when the host pushes a settings change while the module is running.
    /// Gives modules a chance to rebind state (e.g. swap mic device, recompute gains)
    /// without a full restart. The handler runs on a background pipe-reader thread —
    /// implementations should be quick and thread-safe.
    /// </summary>
    /// <remarks>
    /// Arguments: (settingKey, newValue). The new value is the JSON-deserialised form
    /// (boxed). For typed reads, prefer calling <see cref="IModuleSettings.GetSetting{T}"/>
    /// inside the handler — by the time the event fires, <see cref="IModuleContext.Settings"/>
    /// is already updated, so reads return the new value.
    /// </remarks>
    event Action<string, object?>? OnSettingChanged;
}
