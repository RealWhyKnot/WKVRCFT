using System.Text;
using System.Text.Json;

namespace VRCFaceTracking.Core.Sandboxing.V2;

/// <summary>
/// Message types used on the V2 Named Pipe channel.
/// </summary>
public enum V2MessageType
{
    Unknown = 0,

    // Connection handshake
    Handshake = 1,       // host → module: {"moduleId":"...","version":"2.0"}
    HandshakeAck = 2,    // module → host: {"name":"...","author":"...","version":"...","capabilities":3}

    // Lifecycle
    Init = 3,            // host → module: {"eyeAvailable":true,"expressionAvailable":true}
    InitResult = 4,      // module → host: {"success":true}

    // Tracking data (push model: module sends whenever UpdateAsync completes)
    TrackingData = 5,    // module → host: V2TrackingDataPayload

    // Logging
    Log = 6,             // module → host: {"level":2,"message":"..."}

    // Settings
    GetSettings = 7,     // module → host: {}
    Settings = 8,        // host → module: {"key":"value",...}
    SaveSettings = 9,    // module → host: {"key":"value",...}

    // Teardown
    Shutdown = 10,       // host → module
    ShutdownAck = 11,    // module → host

    // Module events (for future UI integration)
    Event = 12,          // module → host: {"eventType":"...","data":null}

    // Config schema (sent during Init)
    ConfigSchema = 13,   // module → host: ConfigSchema JSON
}

/// <summary>
/// A V2 pipe message: type discriminator + optional JSON payload string.
/// </summary>
public sealed record V2Message(V2MessageType Type, string? Payload = null)
{
    public T? DeserializePayload<T>() where T : class
    {
        if (Payload is null) return null;
        return JsonSerializer.Deserialize<T>(Payload);
    }
}

/// <summary>
/// Framing helpers: 4-byte little-endian message type + 4-byte payload length + UTF-8 JSON.
/// </summary>
public static class V2PipeProtocol
{
    public static async Task WriteAsync(Stream stream, V2Message message, CancellationToken ct = default)
    {
        byte[] payloadBytes = message.Payload is null
            ? Array.Empty<byte>()
            : Encoding.UTF8.GetBytes(message.Payload);

        byte[] typeBytes = BitConverter.GetBytes((int)message.Type);
        byte[] lengthBytes = BitConverter.GetBytes(payloadBytes.Length);

        await stream.WriteAsync(typeBytes, ct);
        await stream.WriteAsync(lengthBytes, ct);
        if (payloadBytes.Length > 0)
            await stream.WriteAsync(payloadBytes, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<V2Message?> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        byte[] header = new byte[8]; // 4-byte type + 4-byte length
        int read = await ReadExactAsync(stream, header, ct);
        if (read < 8) return null;

        var type = (V2MessageType)BitConverter.ToInt32(header, 0);
        int payloadLength = BitConverter.ToInt32(header, 4);

        string? payload = null;
        if (payloadLength > 0)
        {
            byte[] payloadBytes = new byte[payloadLength];
            if (await ReadExactAsync(stream, payloadBytes, ct) < payloadLength)
                return null;
            payload = Encoding.UTF8.GetString(payloadBytes);
        }

        return new V2Message(type, payload);
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(totalRead), ct);
            if (n == 0) break; // stream closed
            totalRead += n;
        }
        return totalRead;
    }
}

// ─── Payload POCOs ───────────────────────────────────────────────────────────

public sealed record V2HandshakePayload(string ModuleId, string Version);

public sealed record V2HandshakeAckPayload(
    string Name,
    string Author,
    string Version,
    int Capabilities); // ModuleCapabilities flags

public sealed record V2InitPayload(bool EyeAvailable, bool ExpressionAvailable);

public sealed record V2InitResultPayload(bool Success);

public sealed record V2EyeDataPayload(
    float GazeX,
    float GazeY,
    float Openness,
    float PupilMM);

public sealed record V2HeadRotPayload(float Yaw, float Pitch, float Roll);

public sealed record V2HeadPosPayload(float X, float Y, float Z);

public sealed record V2TrackingDataPayload(
    V2EyeDataPayload? EyeLeft,
    V2EyeDataPayload? EyeRight,
    V2HeadRotPayload? HeadRot,
    V2HeadPosPayload? HeadPos,
    float[]? Shapes);  // null = not updated; array of length UnifiedExpressions.Max+1

public sealed record V2LogPayload(int Level, string Message);

public sealed record V2EventPayload(string EventType, string? Data);
