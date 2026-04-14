namespace VRCFaceTracking.AdvancedEmulation.Behaviours;

/// <summary>
/// Animates a naturalistic yawn sequence over ~4 seconds.
///
/// State machine:
///   Idle → WindUp (0.4s) → Opening (1.5s) → Peak (0.8–1.4s) → Closing (1.0s) → Recovering (0.5s) → Idle
///
/// Outputs are blendable expression weights in [0,1].
/// </summary>
public sealed class YawnAnimator
{
    private enum Phase { Idle, WindUp, Opening, Peak, Closing, Recovering }

    private Phase  _phase = Phase.Idle;
    private float  _phaseTimer;
    private float  _peakDuration;

    // ---- Outputs -------------------------------------------------------
    /// <summary>True while any part of the yawn animation is active.</summary>
    public bool IsYawning     { get; private set; }

    /// <summary>Jaw open weight [0, 0.85].  0 outside yawn.</summary>
    public float JawOpenness  { get; private set; }

    /// <summary>Eye squint weight [0, 1].  Eyes half-close during yawn.</summary>
    public float EyeSquint    { get; private set; }

    /// <summary>Inner brow raise [0, 0.6].  Involuntary during peak.</summary>
    public float BrowRaiseInner { get; private set; }

    /// <summary>Nose wrinkle / nasal constrict [0, 1].  Wind-up only.</summary>
    public float NoseWrinkle  { get; private set; }

    /// <summary>Lip corner pull (slight involuntary grimace) [0, 0.4].</summary>
    public float LipCornerPull { get; private set; }

    private static readonly Random Rng = Random.Shared;

    /// <summary>
    /// Call once per frame to advance the animation.
    /// The caller should call <see cref="Trigger"/> to start a new yawn.
    /// </summary>
    public void Tick(float deltaTime)
    {
        _phaseTimer += deltaTime;

        switch (_phase)
        {
            case Phase.Idle:
                IsYawning      = false;
                JawOpenness    = 0f;
                EyeSquint      = 0f;
                BrowRaiseInner = 0f;
                NoseWrinkle    = 0f;
                LipCornerPull  = 0f;
                return;

            case Phase.WindUp:
            {
                const float dur = 0.4f;
                float t = Math.Clamp(_phaseTimer / dur, 0f, 1f);
                IsYawning      = true;
                JawOpenness    = 0f;
                EyeSquint      = Lerp(0f, 0.35f, EaseIn(t));
                BrowRaiseInner = 0f;
                NoseWrinkle    = Lerp(0f, 0.7f, EaseIn(t));
                LipCornerPull  = Lerp(0f, 0.15f, t);
                if (_phaseTimer >= dur) NextPhase(Phase.Opening);
                break;
            }

            case Phase.Opening:
            {
                const float dur = 1.5f;
                float t = Math.Clamp(_phaseTimer / dur, 0f, 1f);
                IsYawning      = true;
                JawOpenness    = Lerp(0f, 0.85f, EaseOut(t));
                EyeSquint      = Lerp(0.35f, 0.85f, t);
                BrowRaiseInner = Lerp(0f, 0.45f, EaseOut(t));
                NoseWrinkle    = Lerp(0.7f, 0.2f, t);
                LipCornerPull  = Lerp(0.15f, 0.35f, t);
                if (_phaseTimer >= dur) NextPhase(Phase.Peak);
                break;
            }

            case Phase.Peak:
            {
                IsYawning      = true;
                JawOpenness    = 0.85f;
                EyeSquint      = 0.85f;
                BrowRaiseInner = 0.6f;
                NoseWrinkle    = 0.1f;
                LipCornerPull  = 0.35f;
                if (_phaseTimer >= _peakDuration) NextPhase(Phase.Closing);
                break;
            }

            case Phase.Closing:
            {
                const float dur = 1.0f;
                float t = Math.Clamp(_phaseTimer / dur, 0f, 1f);
                IsYawning      = true;
                JawOpenness    = Lerp(0.85f, 0f, EaseIn(t));
                EyeSquint      = Lerp(0.85f, 0.2f, t);
                BrowRaiseInner = Lerp(0.6f,  0f,  EaseIn(t));
                NoseWrinkle    = 0f;
                LipCornerPull  = Lerp(0.35f, 0f, t);
                if (_phaseTimer >= dur) NextPhase(Phase.Recovering);
                break;
            }

            case Phase.Recovering:
            {
                const float dur = 0.5f;
                float t = Math.Clamp(_phaseTimer / dur, 0f, 1f);
                IsYawning      = true;
                JawOpenness    = 0f;
                // Slight residual squint — watery eyes effect
                EyeSquint      = Lerp(0.2f, 0f, t);
                BrowRaiseInner = 0f;
                NoseWrinkle    = 0f;
                LipCornerPull  = 0f;
                if (_phaseTimer >= dur) NextPhase(Phase.Idle);
                break;
            }
        }
    }

    /// <summary>
    /// Trigger a new yawn animation.  Safe to call at any time; ignored while
    /// a yawn is already in progress.
    /// </summary>
    public void Trigger()
    {
        if (_phase != Phase.Idle) return;
        _peakDuration = 0.8f + (float)(Rng.NextDouble() * 0.6f); // 0.8–1.4 s
        NextPhase(Phase.WindUp);
    }

    private void NextPhase(Phase next)
    {
        _phase      = next;
        _phaseTimer = 0f;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float EaseIn(float t)  => t * t;
    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
