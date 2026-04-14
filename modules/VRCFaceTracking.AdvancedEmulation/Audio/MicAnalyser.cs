using NAudio.Wave;

namespace VRCFaceTracking.AdvancedEmulation.Audio;

/// <summary>
/// Lightweight microphone analyser.  Captures 16 kHz mono PCM and derives:
///   • RMS amplitude     → jaw openness signal
///   • Goertzel at four formant frequencies → vowel shape weights (A / E / I / O)
/// No external service required; runs entirely in-process.
/// </summary>
public sealed class MicAnalyser : IDisposable
{
    // ---- Audio constants -----------------------------------------------
    private const int SampleRate    = 16000;
    private const int BitsPerSample = 16;
    private const int Channels      = 1;
    // 100 ms chunks (16 000 samples/s × 0.1 s × 2 bytes)
    private const int ChunkBytes = SampleRate / 10 * (BitsPerSample / 8) * Channels;
    private const int SamplesPerChunk = ChunkBytes / 2;

    // ---- Formant frequencies used for vowel detection ------------------
    // F1 (low frequency — jaw height / vowel height)
    private const float FreqLow   =  500f; // "ah"
    // F2 mid and high (tongue position / vowel frontness)
    private const float FreqMid   = 1300f; // "oh"
    private const float FreqHigh  = 2500f; // "ee"
    // sibilant / fricative band
    private const float FreqFric  = 4000f;

    // ---- Smoothing (fast attack, slow release) --------------------------
    private const float AttackAlpha  = 0.7f;
    private const float ReleaseAlpha = 0.05f;

    // ---- Public outputs (written from audio thread, read from Update) ---
    private float _rawAmplitude;
    private float _vowelA;   // low F1      → "ah/aw"
    private float _vowelO;   // mid F1+F2   → "oh"
    private float _vowelEE;  // high F2     → "ee/ih"
    private float _fricative;
    private readonly object _lock = new();

    public float Amplitude  { get { lock (_lock) return _rawAmplitude; } }
    public float VowelA     { get { lock (_lock) return _vowelA; } }
    public float VowelO     { get { lock (_lock) return _vowelO; } }
    public float VowelEE    { get { lock (_lock) return _vowelEE; } }
    public float Fricative  { get { lock (_lock) return _fricative; } }

    // ---- NAudio state --------------------------------------------------
    private WaveInEvent? _waveIn;
    private readonly float[] _prevOutput = new float[4]; // smoothing state

    public bool IsCapturing { get; private set; }

    public float GainMultiplier { get; set; } = 1.0f;

    public bool Start(int deviceIndex = -1)
    {
        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber     = deviceIndex,
                WaveFormat       = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 100
            };
            _waveIn.DataAvailable += OnData;
            _waveIn.StartRecording();
            IsCapturing = true;
            return true;
        }
        catch
        {
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

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded < 2) return;

        int numSamples = e.BytesRecorded / 2;
        Span<float> samples = numSamples <= 4096
            ? stackalloc float[numSamples]
            : new float[numSamples];

        // Convert S16 PCM → float [-1,1], applying gain
        float gain = GainMultiplier;
        for (int i = 0; i < numSamples; i++)
        {
            short s = BitConverter.ToInt16(e.Buffer, i * 2);
            samples[i] = Math.Clamp(s / 32768f * gain, -1f, 1f);
        }

        float rms   = Rms(samples);
        float low   = Goertzel(samples, FreqLow,  SampleRate);
        float mid   = Goertzel(samples, FreqMid,  SampleRate);
        float high  = Goertzel(samples, FreqHigh, SampleRate);
        float fric  = Goertzel(samples, FreqFric, SampleRate);

        // Normalise Goertzel magnitudes against RMS energy so quiet frames → 0
        float energy = rms + 1e-6f;
        float normLow  = Math.Min(1f, low  / energy * 0.5f);
        float normMid  = Math.Min(1f, mid  / energy * 0.4f);
        float normHigh = Math.Min(1f, high / energy * 0.3f);
        float normFric = Math.Min(1f, fric / energy * 0.25f);

        // Smooth each output
        float amp = Smooth(rms, ref _prevOutput[0]);
        float vA  = Smooth(normLow,  ref _prevOutput[1]);
        float vO  = Smooth(normMid,  ref _prevOutput[2]);
        float vEE = Smooth(normHigh, ref _prevOutput[3]);

        lock (_lock)
        {
            _rawAmplitude = amp;
            _vowelA       = vA;
            _vowelO       = vO;
            _vowelEE      = vEE;
            _fricative    = normFric;
        }
    }

    // ---- DSP helpers ---------------------------------------------------

    private static float Rms(Span<float> samples)
    {
        if (samples.Length == 0) return 0f;
        float sum = 0f;
        foreach (float s in samples) sum += s * s;
        return MathF.Sqrt(sum / samples.Length);
    }

    /// <summary>
    /// Single-frequency Goertzel magnitude.  Returns unnormalised power at
    /// <paramref name="freq"/> Hz.
    /// </summary>
    private static float Goertzel(Span<float> samples, float freq, int sr)
    {
        float omega = 2f * MathF.PI * freq / sr;
        float coeff = 2f * MathF.Cos(omega);
        float q1 = 0f, q2 = 0f;
        foreach (float s in samples)
        {
            float q0 = coeff * q1 - q2 + s;
            q2 = q1;
            q1 = q0;
        }
        float mag = MathF.Sqrt(q1 * q1 + q2 * q2 - coeff * q1 * q2);
        return mag / Math.Max(1, samples.Length);
    }

    private static float Smooth(float input, ref float prev)
    {
        float alpha = input > prev ? AttackAlpha : ReleaseAlpha;
        prev = alpha * input + (1f - alpha) * prev;
        return prev;
    }

    public void Dispose() => Stop();
}
