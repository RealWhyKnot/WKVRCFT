namespace VRCFaceTracking.AdvancedEmulation.Behaviours;

/// <summary>
/// Infers a simple emotional/arousal state from microphone activity and elapsed
/// quiet time.  The state governs eye openness droop and blink-rate multiplier.
///
/// State machine:
///   Engaged → (silence &gt; IdleTimeout)  → Idle
///   Idle    → (silence &gt; SleepTimeout) → Sleeping
///   Sleeping → (sound detected)          → Engaged  (snaps back immediately)
///   Idle    → (sound detected)           → Engaged  (snaps back immediately)
///
/// Outputs:
///   • <see cref="EyeDroop"/>           [0, 1] — extra lid droop (0 = fully alert)
///   • <see cref="BlinkIntervalScale"/> [0.3, 1] — multiply blink interval (lower = blinks faster)
/// </summary>
public sealed class EmotionalStateTracker
{
    public enum State { Engaged, Idle, Sleeping }

    // ---- Configuration -------------------------------------------------
    public float IdleTimeoutSec  { get; set; } = 8f;
    public float SleepTimeoutSec { get; set; } = 30f;
    /// <summary>RMS amplitude threshold that counts as "active speech".</summary>
    public float ActivityThreshold { get; set; } = 0.015f;

    // ---- State ---------------------------------------------------------
    public State CurrentState    { get; private set; } = State.Engaged;

    /// <summary>Extra eyelid droop in [0, 1]. 0 = fully alert, 1 = fully asleep.</summary>
    public float EyeDroop        { get; private set; }

    /// <summary>
    /// Blink interval multiplier.  1 = normal rate.
    /// Tired/sleeping → lower value → faster blinks.
    /// </summary>
    public float BlinkIntervalScale { get; private set; } = 1f;

    private float _silenceTimer;
    // Smoothed droop target (avoids instant jumps when state changes)
    private float _droopTarget;
    private float _smoothedDroop;

    private const float DroopSmoothRate = 0.002f; // per frame at 100 Hz → ~5s full transition

    /// <param name="amplitude">Current RMS mic amplitude [0, 1].</param>
    /// <param name="deltaTime">Elapsed seconds since last call.</param>
    public void Update(float amplitude, float deltaTime)
    {
        if (amplitude >= ActivityThreshold)
        {
            _silenceTimer = 0f;
            if (CurrentState != State.Engaged)
                CurrentState = State.Engaged;
        }
        else
        {
            _silenceTimer += deltaTime;
            if (_silenceTimer >= SleepTimeoutSec)
                CurrentState = State.Sleeping;
            else if (_silenceTimer >= IdleTimeoutSec)
                CurrentState = State.Idle;
        }

        // Compute target droop and blink multiplier for each state
        switch (CurrentState)
        {
            case State.Engaged:
                _droopTarget        = 0.0f;
                BlinkIntervalScale  = 1.00f;
                break;
            case State.Idle:
                // linearly increase droop as silence accumulates past idle timeout
                float idleFrac = Math.Clamp(
                    (_silenceTimer - IdleTimeoutSec) / Math.Max(0.1f, SleepTimeoutSec - IdleTimeoutSec),
                    0f, 1f);
                _droopTarget       = 0.08f + idleFrac * 0.18f; // 8%–26% droop
                BlinkIntervalScale = 0.75f - idleFrac * 0.20f; // blink faster as eyes get heavy
                break;
            case State.Sleeping:
                _droopTarget       = 0.45f;  // almost half-closed
                BlinkIntervalScale = 0.40f;  // slow, infrequent blinks
                break;
        }

        // Smooth toward target so state transitions are gradual
        _smoothedDroop = _smoothedDroop + (_droopTarget - _smoothedDroop) * DroopSmoothRate;
        EyeDroop       = _smoothedDroop;
    }
}
