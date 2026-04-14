namespace VRCFaceTracking.V2;

/// <summary>
/// V2 module interface. Modules implement this to provide tracking data.
/// Unlike V1's abstract class, this is an interface allowing more flexibility.
/// </summary>
public interface ITrackingModuleV2
{
    /// <summary>
    /// Declares what tracking capabilities this module provides.
    /// </summary>
    ModuleCapabilities Capabilities { get; }

    /// <summary>
    /// Initialize the module. Called once when the module is loaded.
    /// </summary>
    /// <param name="context">Module context providing services and data access.</param>
    /// <returns>True if initialization succeeded.</returns>
    Task<bool> InitializeAsync(IModuleContext context);

    /// <summary>
    /// Called at ~100Hz to update tracking data. Write to context.TrackingData.
    /// </summary>
    Task UpdateAsync(CancellationToken ct);

    /// <summary>
    /// Clean shutdown. Release resources, close connections.
    /// </summary>
    Task ShutdownAsync();
}
