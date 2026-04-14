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
}
