using NAudio.Wave;

namespace VRCFaceTracking.EmulatedTracking.Audio;

/// <summary>
/// Captures microphone input at 16kHz mono PCM using NAudio.
/// Provides a ring buffer for gRPC streaming.
/// </summary>
public class AudioCaptureService : IDisposable
{
    private WaveInEvent? _waveIn;
    private readonly Queue<byte[]> _audioQueue = new();
    private readonly object _queueLock = new();
    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

    public bool IsCapturing { get; private set; }
    public float GainMultiplier { get; set; } = 1.0f;

    // Chunk size: 100ms of audio = 16000 * 0.1 * 2 bytes = 3200 bytes
    private const int ChunkBytes = SampleRate / 10 * (BitsPerSample / 8) * Channels;

    public event Action<byte[]>? OnAudioChunk;

    public bool Start(int deviceIndex = -1)
    {
        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 100
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
            IsCapturing = true;
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Audio capture start failed: {ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;
        IsCapturing = false;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        byte[] chunk = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, chunk, 0, e.BytesRecorded);

        // Apply gain
        if (Math.Abs(GainMultiplier - 1.0f) > 0.001f)
            ApplyGain(chunk, GainMultiplier);

        OnAudioChunk?.Invoke(chunk);
    }

    private static void ApplyGain(byte[] pcm, float gain)
    {
        for (int i = 0; i < pcm.Length - 1; i += 2)
        {
            short sample = BitConverter.ToInt16(pcm, i);
            sample = (short)Math.Clamp(sample * gain, short.MinValue, short.MaxValue);
            var bytes = BitConverter.GetBytes(sample);
            pcm[i] = bytes[0];
            pcm[i + 1] = bytes[1];
        }
    }

    public static IEnumerable<(int index, string name)> GetDevices()
    {
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var cap = WaveInEvent.GetCapabilities(i);
            yield return (i, cap.ProductName);
        }
    }

    public void Dispose() => Stop();
}
