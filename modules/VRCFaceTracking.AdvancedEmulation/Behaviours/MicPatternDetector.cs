namespace VRCFaceTracking.AdvancedEmulation.Behaviours;

/// <summary>
/// Detects higher-level speech/audio patterns from a rolling amplitude history:
///
///   • <see cref="LaughterProbability"/> — rapid amplitude bursts with high variance
///   • <see cref="BreathingLevel"/>      — sustained low-amplitude rhythmic signal
///   • <see cref="SighProgress"/>        — rise-then-slow-decay amplitude envelope
///   • <see cref="IsWhispering"/>        — below normal speech but above silence
///   • <see cref="IsLoudSpeaking"/>      — amplitude well above normal speech level
///
/// Call <see cref="Update"/> once per application tick (≈100 Hz).  Internally
/// the detector decimates to ~10 Hz for the ring-buffer analysis so it remains
/// cheap even at high tick rates.
/// </summary>
public sealed class MicPatternDetector
{
    // ---- Ring buffer (10 Hz effective, 5 s history = 50 slots) ---------
    private const int BufSize     = 50;
    private const float DecimateHz = 10f;   // desired analysis rate
    private readonly float[] _buf  = new float[BufSize];
    private int  _bufHead;
    private float _decTimer;

    // ---- Smoothed outputs -----------------------------------------------
    private float _laughterRaw;
    private float _breathingRaw;
    private float _sighRaw;

    // ---- Sigh tracking --------------------------------------------------
    private float _sighPeak;
    private float _sighDecayTimer;
    private const float SighDecayWindow = 2.0f; // seconds for decay phase

    // ---- Outputs --------------------------------------------------------
    /// <summary>[0,1] probability of laughter based on rapid amplitude variance.</summary>
    public float LaughterProbability { get; private set; }

    /// <summary>[0,1] level of sustained quiet breathing-range amplitude.</summary>
    public float BreathingLevel { get; private set; }

    /// <summary>[0,1] progress of a sigh event (rises then decays).</summary>
    public float SighProgress { get; private set; }

    /// <summary>True when amplitude is in whisper range (0.008–0.04).</summary>
    public bool IsWhispering { get; private set; }

    /// <summary>True when amplitude is well above normal speech.</summary>
    public bool IsLoudSpeaking { get; private set; }

    private const float AttackAlpha  = 0.15f;
    private const float ReleaseAlpha = 0.03f;

    /// <param name="amplitude">Current RMS mic amplitude [0,1].</param>
    /// <param name="deltaTime">Elapsed seconds since last call.</param>
    public void Update(float amplitude, float deltaTime)
    {
        // ---- Boolean outputs are instant --------------------------------
        IsWhispering   = amplitude is >= 0.008f and <= 0.04f;
        IsLoudSpeaking = amplitude > 0.18f;

        // ---- Decimate into ring buffer at ~10 Hz -----------------------
        _decTimer += deltaTime;
        float decimatePeriod = 1f / DecimateHz;
        bool newSample = false;
        while (_decTimer >= decimatePeriod)
        {
            _decTimer  -= decimatePeriod;
            _buf[_bufHead] = amplitude;
            _bufHead = (_bufHead + 1) % BufSize;
            newSample = true;
        }

        if (!newSample) return;

        // ---- Laughter: high variance of amplitude in short window -------
        // Use the last 10 samples (~1 s) and compute variance.
        float varLaughter = VarianceLast(10);
        float laughTarget = Math.Clamp((varLaughter - 0.0004f) / 0.003f, 0f, 1f);
        // Also require mean amplitude to be in speech range, not near-silence
        float meanShort = MeanLast(10);
        if (meanShort < 0.04f) laughTarget = 0f; // silence bursts don't count
        _laughterRaw = Smooth(_laughterRaw, laughTarget, AttackAlpha, ReleaseAlpha);
        LaughterProbability = _laughterRaw;

        // ---- Breathing: sustained very-low amplitude with mild variance --
        float meanLong = MeanLast(40); // ~4 s window
        float varLong  = VarianceLast(40);
        float breathTarget = 0f;
        if (meanLong is >= 0.005f and <= 0.035f && varLong < 0.0002f)
            breathTarget = Math.Clamp((meanLong - 0.005f) / 0.03f, 0f, 1f);
        _breathingRaw = Smooth(_breathingRaw, breathTarget, 0.05f, 0.01f);
        BreathingLevel = _breathingRaw;

        // ---- Sigh: amplitude rises to medium then decays slowly ---------
        // Track peak and time since it was last exceeded.
        if (amplitude > _sighPeak)
        {
            _sighPeak = amplitude;
            _sighDecayTimer = 0f;
        }
        else
        {
            _sighDecayTimer += decimatePeriod;
        }

        float sighTarget = 0f;
        if (_sighPeak is >= 0.04f and <= 0.18f && _sighDecayTimer > 0.5f)
        {
            // Progress: 1 at start of decay, falls to 0 over SighDecayWindow
            sighTarget = Math.Clamp(1f - _sighDecayTimer / SighDecayWindow, 0f, 1f);
        }
        if (_sighDecayTimer > SighDecayWindow) { _sighPeak = 0f; _sighDecayTimer = 0f; }
        _sighRaw = Smooth(_sighRaw, sighTarget, 0.3f, 0.05f);
        SighProgress = _sighRaw;
    }

    // ---- Ring buffer statistics -----------------------------------------

    private float MeanLast(int count)
    {
        count = Math.Min(count, BufSize);
        float sum = 0f;
        for (int i = 0; i < count; i++)
            sum += _buf[(_bufHead - 1 - i + BufSize) % BufSize];
        return sum / count;
    }

    private float VarianceLast(int count)
    {
        count = Math.Min(count, BufSize);
        float mean = MeanLast(count);
        float sumSq = 0f;
        for (int i = 0; i < count; i++)
        {
            float d = _buf[(_bufHead - 1 - i + BufSize) % BufSize] - mean;
            sumSq += d * d;
        }
        return sumSq / count;
    }

    private static float Smooth(float current, float target, float attack, float release)
    {
        float alpha = target > current ? attack : release;
        return current + (target - current) * alpha;
    }
}
