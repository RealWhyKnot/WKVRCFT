using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Params.Expressions;

namespace VRCFaceTracking.Core.Sandboxing.V2;

/// <summary>
/// Host-side Named Pipe server for a single V2 module process.
/// Creates the server stream, manages the connection lifecycle, and dispatches messages.
/// </summary>
public class V2PipeServer : IDisposable
{
    private readonly ILogger<V2PipeServer> _logger;
    private readonly string _pipeName;
    private NamedPipeServerStream? _pipe;
    private CancellationTokenSource _cts = new();
    private Task? _readTask;

    public string ModuleId { get; }
    public string PipeName => _pipeName;
    public bool IsConnected => _pipe?.IsConnected ?? false;

    public event Action<V2Message>? OnMessageReceived;
    public event Action? OnDisconnected;

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public V2PipeServer(string moduleId, ILoggerFactory loggerFactory)
    {
        ModuleId = moduleId;
        _pipeName = "vrcft-module-" + moduleId;
        _logger = loggerFactory.CreateLogger<V2PipeServer>();
    }

    // Modules have this long to connect before we declare them dead on arrival.
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Create the server pipe and wait for the client (module process) to connect.
    /// Fails with a clear error if the module does not connect within 30 seconds.
    /// </summary>
    public async Task<bool> StartAndWaitForConnectionAsync(CancellationToken ct = default)
    {
        using var timeoutCts = new CancellationTokenSource(HandshakeTimeout);
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            _pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            _logger.LogInformation($"V2 pipe server '{_pipeName}' waiting for client (timeout {HandshakeTimeout.TotalSeconds}s)...");
            await _pipe.WaitForConnectionAsync(linkedCts.Token);
            _logger.LogInformation($"V2 pipe server '{_pipeName}' client connected");

            _cts = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
            return true;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogError(
                $"V2 pipe '{_pipeName}' handshake timed out after {HandshakeTimeout.TotalSeconds}s — " +
                $"module process probably crashed during startup. Check the crash log in %APPDATA%\\VRCFaceTracking.");
            _pipe?.Dispose();
            _pipe = null;
            return false;
        }
        catch (OperationCanceledException)
        {
            _pipe?.Dispose();
            _pipe = null;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"V2 pipe server '{_pipeName}' failed to start: {ex.Message}");
            _pipe?.Dispose();
            _pipe = null;
            return false;
        }
    }

    public async Task SendAsync(V2Message message, CancellationToken ct = default)
    {
        if (_pipe == null || !_pipe.IsConnected) return;

        await _sendLock.WaitAsync(ct);
        try
        {
            await V2PipeProtocol.WriteAsync(_pipe, message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"V2 send failed: {ex.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task SendHandshakeAsync(CancellationToken ct = default)
    {
        var payload = new V2HandshakePayload(ModuleId, "2.0");
        await SendAsync(new V2Message(V2MessageType.Handshake, JsonSerializer.Serialize(payload)), ct);
    }

    public async Task SendInitAsync(bool eyeAvailable, bool expressionAvailable, CancellationToken ct = default)
    {
        var payload = new V2InitPayload(eyeAvailable, expressionAvailable);
        await SendAsync(new V2Message(V2MessageType.Init, JsonSerializer.Serialize(payload)), ct);
    }

    public async Task SendShutdownAsync(CancellationToken ct = default)
    {
        await SendAsync(new V2Message(V2MessageType.Shutdown), ct);
    }

    public async Task SendSettingsAsync(Dictionary<string, string> settings, CancellationToken ct = default)
    {
        await SendAsync(new V2Message(V2MessageType.Settings, JsonSerializer.Serialize(settings)), ct);
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _pipe != null && _pipe.IsConnected)
        {
            try
            {
                var message = await V2PipeProtocol.ReadAsync(_pipe, ct);
                if (message == null)
                {
                    _logger.LogInformation($"V2 pipe '{_pipeName}' client disconnected");
                    OnDisconnected?.Invoke();
                    break;
                }

                OnMessageReceived?.Invoke(message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                // Pipe broken - module crashed
                _logger.LogWarning($"V2 pipe '{_pipeName}' broken, module likely crashed");
                OnDisconnected?.Invoke();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError($"V2 pipe '{_pipeName}' read error: {ex.Message}");
            }
        }
    }

    // ---- Global data lock -----------------------------------------------
    // Multiple V2 modules write to UnifiedTracking.Data concurrently (each on
    // its own pipe read thread).  This lock serialises all writes so a reader
    // (e.g. the OSC send loop) never sees a torn struct.
    private static readonly object _dataLock = new();

    /// <summary>
    /// Merges a V2TrackingDataPayload from this module into the global UnifiedTracking.Data,
    /// respecting capability ownership.  Thread-safe: serialised by <see cref="_dataLock"/>.
    /// </summary>
    public static void ApplyTrackingData(V2TrackingDataPayload data)
    {
        lock (_dataLock)
        {
            if (data.EyeLeft != null)
            {
                UnifiedTracking.Data.Eye.Left.Gaze.x = data.EyeLeft.GazeX;
                UnifiedTracking.Data.Eye.Left.Gaze.y = data.EyeLeft.GazeY;
                UnifiedTracking.Data.Eye.Left.Openness = data.EyeLeft.Openness;
                UnifiedTracking.Data.Eye.Left.PupilDiameter_MM = data.EyeLeft.PupilMM;
            }

            if (data.EyeRight != null)
            {
                UnifiedTracking.Data.Eye.Right.Gaze.x = data.EyeRight.GazeX;
                UnifiedTracking.Data.Eye.Right.Gaze.y = data.EyeRight.GazeY;
                UnifiedTracking.Data.Eye.Right.Openness = data.EyeRight.Openness;
                UnifiedTracking.Data.Eye.Right.PupilDiameter_MM = data.EyeRight.PupilMM;
            }

            if (data.HeadRot != null)
            {
                UnifiedTracking.Data.Head.HeadYaw = data.HeadRot.Yaw;
                UnifiedTracking.Data.Head.HeadPitch = data.HeadRot.Pitch;
                UnifiedTracking.Data.Head.HeadRoll = data.HeadRot.Roll;
            }

            if (data.HeadPos != null)
            {
                UnifiedTracking.Data.Head.HeadPosX = data.HeadPos.X;
                UnifiedTracking.Data.Head.HeadPosY = data.HeadPos.Y;
                UnifiedTracking.Data.Head.HeadPosZ = data.HeadPos.Z;
            }

            if (data.Shapes != null)
            {
                int limit = Math.Min(data.Shapes.Length, UnifiedTracking.Data.Shapes.Length);
                for (int i = 0; i < limit; i++)
                {
                    if (!float.IsNaN(data.Shapes[i]))
                        UnifiedTracking.Data.Shapes[i].Weight = data.Shapes[i];
                }
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _pipe?.Dispose();
        _sendLock.Dispose();
    }
}
