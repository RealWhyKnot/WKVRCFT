namespace VRCFaceTracking.EmulatedTracking.Inference;

/// <summary>
/// Self-contained audio → ARKit blendshape estimator using spectral signal processing.
///
/// No external service or GPU required — runs entirely in-process on the CPU.
///
/// Algorithm per 100ms audio chunk:
///   1. RMS energy  → jaw openness
///   2. Goertzel energy at three key frequencies (400 Hz, 1500 Hz, 3500 Hz) →
///      frequency band ratios used to distinguish vowel classes:
///        low-dominant  (400 Hz)  → rounded/back vowels  (oo, oh)  → funnel/pucker
///        high-dominant (3500 Hz) → front/unrounded       (ee, eh)  → stretch/smile
///   3. ZCR (zero-crossing rate) → suppresses smile during unvoiced fricatives (s, f)
///   4. All outputs pass through an asymmetric exponential smoother (fast attack,
///      slow release) to prevent jitter without adding noticeable lag.
/// </summary>
public class SignalProcessingBackend
{
    // ARKit blendshape indices used
    private const int JawOpen       = 17;
    private const int MouthClose    = 18;
    private const int MouthFunnel   = 19;
    private const int MouthPucker   = 20;
    private const int MouthSmileL   = 23;
    private const int MouthSmileR   = 24;
    private const int MouthStretchL = 29;
    private const int MouthStretchR = 30;
    private const int ArkitCount    = 52;

    private const float SampleRate = 16000f;

    // Smoothed state
    private float _jaw, _close, _funnel, _stretch, _smile;

    /// <summary>
    /// Process one chunk of 16-bit little-endian 16 kHz mono PCM.
    /// Returns a 52-element ARKit blendshape array (same layout expected by ARKitMapper).
    /// </summary>
    public float[] Process(byte[] pcm16le)
    {
        int n = pcm16le.Length / 2;
        var samples = new float[n];
        for (int i = 0; i < n; i++)
            samples[i] = BitConverter.ToInt16(pcm16le, i * 2) / 32768f;

        // ── RMS energy ────────────────────────────────────────────────────────
        float rms = 0f;
        for (int i = 0; i < n; i++) rms += samples[i] * samples[i];
        rms = MathF.Sqrt(rms / n);

        // ── Zero-crossing rate (voiced vs unvoiced discriminator) ─────────────
        int crossings = 0;
        for (int i = 1; i < n; i++)
            if (samples[i - 1] * samples[i] < 0) crossings++;
        float zcr = (float)crossings / n;

        // ── Spectral band energies via Goertzel ───────────────────────────────
        // 400 Hz  → fundamental / F1 low (rounded/back vowels: oo, oh)
        // 1500 Hz → F1/F2 overlap  (open vowels: ah, ae)
        // 3500 Hz → F2 high / sibilance (front vowels: ee, eh; fricatives: s, f)
        float eLow  = Goertzel(samples,  400f);
        float eMid  = Goertzel(samples, 1500f);
        float eHigh = Goertzel(samples, 3500f);
        float eTotal = eLow + eMid + eHigh + 1e-8f;

        float lowRatio  = eLow  / eTotal;
        float highRatio = eHigh / eTotal;
        float rmsActive = Math.Clamp(rms * 6f, 0f, 1f);

        // ── Target blendshape values ──────────────────────────────────────────
        float jawTarget     = Math.Clamp(rms * 7f, 0f, 1f);
        float closeTarget   = Math.Clamp(1f - rms * 5f, 0f, 1f);
        // Funnel/pucker: low-frequency dominant, voiced (low ZCR)
        float funnelTarget  = Math.Clamp(lowRatio * rmsActive * 1.5f * (1f - zcr * 4f), 0f, 1f);
        // Stretch: high-frequency dominant (front vowels)
        float stretchTarget = Math.Clamp(highRatio * rmsActive * 1.5f, 0f, 1f);
        // Smile: front vowel, voiced (ZCR suppresses fricatives)
        float smileTarget   = Math.Clamp(highRatio * rmsActive * (1f - zcr * 5f), 0f, 1f);

        // ── Asymmetric smoothing (fast attack, slow release) ──────────────────
        _jaw     = Smooth(_jaw,     jawTarget,     0.40f, 0.07f);
        _close   = Smooth(_close,   closeTarget,   0.30f, 0.10f);
        _funnel  = Smooth(_funnel,  funnelTarget,  0.35f, 0.05f);
        _stretch = Smooth(_stretch, stretchTarget, 0.35f, 0.05f);
        _smile   = Smooth(_smile,   smileTarget,   0.30f, 0.04f);

        // ── Build ARKit array ─────────────────────────────────────────────────
        var arkit = new float[ArkitCount];
        arkit[JawOpen]       = _jaw;
        arkit[MouthClose]    = _close;
        arkit[MouthFunnel]   = _funnel;
        arkit[MouthPucker]   = _funnel * 0.6f;
        arkit[MouthStretchL] = _stretch;
        arkit[MouthStretchR] = _stretch;
        arkit[MouthSmileL]   = _smile;
        arkit[MouthSmileR]   = _smile;
        return arkit;
    }

    public void Reset()
    {
        _jaw = _close = _funnel = _stretch = _smile = 0f;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Goertzel algorithm: computes the spectral energy at a single frequency
    /// in O(N) without a full FFT.  Returns energy normalised by sample count.
    /// </summary>
    private static float Goertzel(float[] samples, float freq)
    {
        int n = samples.Length;
        float w    = 2f * MathF.PI * freq / SampleRate;
        float cosW = MathF.Cos(w);
        float q1 = 0f, q2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float q0 = 2f * cosW * q1 - q2 + samples[i];
            q2 = q1;
            q1 = q0;
        }
        float energy = q1 * q1 + q2 * q2 - q1 * q2 * 2f * cosW;
        return Math.Max(0f, energy) / n;
    }

    private static float Smooth(float current, float target, float attack, float release)
    {
        float alpha = target > current ? attack : release;
        return current + alpha * (target - current);
    }
}
