using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Sandboxing.V2;
using VRCFaceTracking.V2;
using VRCFaceTracking.V2.Configuration;

namespace VRCFaceTracking.ModuleHostV2;

/// <summary>
/// IModuleContext implementation for V2 module processes.
/// Provides logging (forwarded to host via pipe), settings (local JSON), and tracking data.
/// </summary>
public class V2ModuleContext : IModuleContext
{
    private readonly V2PipeClient _pipe;
    private readonly CancellationToken _ct;

    public ILogger Logger { get; }
    public IModuleSettings Settings { get; }
    public ITrackingDataWriter TrackingData { get; }

    // Expose the writer so Program.cs can call FlushAsync after UpdateAsync
    internal V2TrackingDataWriter Writer => (V2TrackingDataWriter)TrackingData;

    public V2ModuleContext(V2PipeClient pipe, string moduleDirPath, CancellationToken ct)
    {
        _pipe = pipe;
        _ct = ct;

        Logger = new V2PipeLogger(pipe, ct);
        Settings = new V2ModuleSettings(moduleDirPath);
        TrackingData = new V2TrackingDataWriter(pipe);
    }

    public void PublishEvent(string eventType, object? payload = null)
    {
        var p = new V2EventPayload(eventType, payload is null ? null : System.Text.Json.JsonSerializer.Serialize(payload));
        _ = _pipe.SendAsync(
            new V2Message(V2MessageType.Event, System.Text.Json.JsonSerializer.Serialize(p)), _ct);
    }

    public void RegisterConfigSchema(ConfigSchema schema)
    {
        _ = _pipe.SendAsync(
            new V2Message(V2MessageType.ConfigSchema, System.Text.Json.JsonSerializer.Serialize(schema)), _ct);
    }
}

/// <summary>
/// ILogger that forwards log entries to the host via the V2 pipe.
/// </summary>
internal class V2PipeLogger : ILogger
{
    private readonly V2PipeClient _pipe;
    private readonly CancellationToken _ct;

    public V2PipeLogger(V2PipeClient pipe, CancellationToken ct)
    {
        _pipe = pipe;
        _ct = ct;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        string message = formatter(state, exception);
        if (exception != null) message += " | " + exception.Message;
        _ = _pipe.SendLogAsync((int)logLevel, message, _ct);
    }
}
