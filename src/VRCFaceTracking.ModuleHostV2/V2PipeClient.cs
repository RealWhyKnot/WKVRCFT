using System.IO.Pipes;
using System.Text.Json;
using VRCFaceTracking.Core.Sandboxing.V2;

namespace VRCFaceTracking.ModuleHostV2;

/// <summary>
/// Module-side Named Pipe client. Connects to the host's V2PipeServer,
/// receives commands, and sends tracking data + log messages.
/// </summary>
public class V2PipeClient : IDisposable
{
    private readonly string _pipeName;
    private NamedPipeClientStream? _pipe;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public bool IsConnected => _pipe?.IsConnected ?? false;

    public event Action<V2Message>? OnMessageReceived;

    public V2PipeClient(string pipeName)
    {
        _pipeName = pipeName;
    }

    public async Task<bool> ConnectAsync(CancellationToken ct)
    {
        try
        {
            _pipe = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            // Wait up to 30s for the server pipe to appear
            await _pipe.ConnectAsync(30000, ct);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"V2 pipe connect failed: {ex.Message}");
            return false;
        }
    }

    public async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_pipe == null) return;

        while (!ct.IsCancellationRequested && _pipe.IsConnected)
        {
            try
            {
                var msg = await V2PipeProtocol.ReadAsync(_pipe, ct);
                if (msg == null) break; // pipe closed
                OnMessageReceived?.Invoke(msg);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                break; // Host disconnected
            }
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
        catch { /* silently absorb send errors */ }
        finally
        {
            _sendLock.Release();
        }
    }

    public Task SendHandshakeAckAsync(string name, string author, string version, int capabilities, CancellationToken ct)
    {
        var ack = new V2HandshakeAckPayload(name, author, version, capabilities);
        return SendAsync(new V2Message(V2MessageType.HandshakeAck, JsonSerializer.Serialize(ack)), ct);
    }

    public Task SendInitResultAsync(bool success, CancellationToken ct)
    {
        var result = new V2InitResultPayload(success);
        return SendAsync(new V2Message(V2MessageType.InitResult, JsonSerializer.Serialize(result)), ct);
    }

    public Task SendTrackingDataAsync(V2TrackingDataPayload payload, CancellationToken ct)
    {
        return SendAsync(new V2Message(V2MessageType.TrackingData, JsonSerializer.Serialize(payload)), ct);
    }

    public Task SendLogAsync(int level, string message, CancellationToken ct)
    {
        var log = new V2LogPayload(level, message);
        return SendAsync(new V2Message(V2MessageType.Log, JsonSerializer.Serialize(log)), ct);
    }

    public Task SendShutdownAckAsync(CancellationToken ct)
    {
        return SendAsync(new V2Message(V2MessageType.ShutdownAck), ct);
    }

    public void Dispose()
    {
        _pipe?.Dispose();
        _sendLock.Dispose();
    }
}
