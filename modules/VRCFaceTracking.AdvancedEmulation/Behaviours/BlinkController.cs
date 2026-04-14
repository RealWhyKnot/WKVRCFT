namespace VRCFaceTracking.AdvancedEmulation.Behaviours;

/// <summary>
/// Generates a natural blink rhythm and optionally adds gaze saccades.
///
/// Blink model
///   • Between blinks a random interval in [rateMin, rateMax] seconds is drawn.
///   • Emotional-state multiplier shortens the interval when tired/sleeping.
///   • Each blink is a triangular eyelid excursion: openness drops to 0 in
///     <see cref="HalfDurationSec"/> seconds then recovers in the same time.
///   • With probability <see cref="DoubleBlink"/> a second blink immediately follows.
///
/// Saccade model
///   • A target gaze offset is chosen uniformly in a disc of radius <see cref="SaccadeRadius"/>.
///   • The gaze position drifts toward that target at <see cref="SaccadeSpeed"/> units/s.
///   • A new target is chosen after reaching the current one or after a timeout.
/// </summary>
public sealed class BlinkController
{
    // ---- Configurable properties (set before each update tick) ---------
    public float RateMinSec      { get; set; } = 3.0f;
    public float RateMaxSec      { get; set; } = 4.0f;
    public float HalfDurationSec { get; set; } = 0.08f; // 80 ms each half → ~160 ms total
    public float DoubleBlink     { get; set; } = 0.10f;
    public float SaccadeRadius   { get; set; } = 0.06f;
    public float SaccadeSpeed    { get; set; } = 0.40f;

    // ---- Outputs -------------------------------------------------------
    /// <summary>Current eye openness contribution from blink state [0, 1].</summary>
    public float BlinkOpenness  { get; private set; } = 1.0f;

    /// <summary>Current saccade gaze offset X (horizontal) in [-1, 1] normalised.</summary>
    public float SaccadeX { get; private set; }

    /// <summary>Current saccade gaze offset Y (vertical) in [-1, 1] normalised.</summary>
    public float SaccadeY { get; private set; }

    // ---- Internal state ------------------------------------------------
    private enum BlinkPhase { Idle, Closing, Opening, DoublePause }

    private BlinkPhase _phase = BlinkPhase.Idle;
    private float _phaseTimer;
    private float _nextBlink;
    private bool  _pendingDouble;

    private float _saccadeTargetX;
    private float _saccadeTargetY;
    private float _saccadeTimeout;

    private static readonly Random Rng = Random.Shared;

    public BlinkController()
    {
        _nextBlink = NextInterval();
        PickSaccadeTarget();
    }

    /// <param name="deltaTime">Elapsed time since the last call, in seconds.</param>
    /// <param name="intervalMultiplier">
    /// Scales the blink interval.  Values &lt; 1 increase blink rate (tired/sleepy).
    /// </param>
    /// <param name="saccadesEnabled">Whether saccade logic should run this frame.</param>
    public void Tick(float deltaTime, float intervalMultiplier = 1f, bool saccadesEnabled = true)
    {
        TickBlink(deltaTime, intervalMultiplier);
        if (saccadesEnabled)
            TickSaccade(deltaTime);
    }

    // ---- Blink state machine -------------------------------------------

    private void TickBlink(float dt, float intervalMultiplier)
    {
        switch (_phase)
        {
            case BlinkPhase.Idle:
                _phaseTimer += dt;
                float interval = _nextBlink * Math.Max(0.1f, intervalMultiplier);
                if (_phaseTimer >= interval)
                {
                    _phaseTimer    = 0f;
                    _pendingDouble = Rng.NextDouble() < DoubleBlink;
                    _phase         = BlinkPhase.Closing;
                }
                BlinkOpenness = 1f;
                break;

            case BlinkPhase.Closing:
                _phaseTimer += dt;
                float closeProgress = _phaseTimer / HalfDurationSec;
                BlinkOpenness = Math.Clamp(1f - closeProgress, 0f, 1f);
                if (_phaseTimer >= HalfDurationSec)
                {
                    _phaseTimer   = 0f;
                    _phase        = BlinkPhase.Opening;
                    BlinkOpenness = 0f;
                }
                break;

            case BlinkPhase.Opening:
                _phaseTimer += dt;
                float openProgress = _phaseTimer / HalfDurationSec;
                BlinkOpenness = Math.Clamp(openProgress, 0f, 1f);
                if (_phaseTimer >= HalfDurationSec)
                {
                    _phaseTimer   = 0f;
                    BlinkOpenness = 1f;
                    if (_pendingDouble)
                    {
                        _pendingDouble = false;
                        _phase         = BlinkPhase.DoublePause;
                    }
                    else
                    {
                        _nextBlink = NextInterval();
                        _phase     = BlinkPhase.Idle;
                    }
                }
                break;

            case BlinkPhase.DoublePause:
                // Brief pause between double-blink (one additional half-duration)
                _phaseTimer += dt;
                BlinkOpenness = 1f;
                if (_phaseTimer >= HalfDurationSec)
                {
                    _phaseTimer = 0f;
                    _phase      = BlinkPhase.Closing;
                }
                break;
        }
    }

    // ---- Saccade state machine -----------------------------------------

    private void TickSaccade(float dt)
    {
        // Drift toward target
        float dx = _saccadeTargetX - SaccadeX;
        float dy = _saccadeTargetY - SaccadeY;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        float step = SaccadeSpeed * dt;
        if (dist <= step || dist < 0.001f)
        {
            SaccadeX = _saccadeTargetX;
            SaccadeY = _saccadeTargetY;
            _saccadeTimeout -= dt;
            if (_saccadeTimeout <= 0f)
                PickSaccadeTarget();
        }
        else
        {
            SaccadeX += (dx / dist) * step;
            SaccadeY += (dy / dist) * step;
        }
    }

    private void PickSaccadeTarget()
    {
        // Uniform random point inside a disc
        double angle  = Rng.NextDouble() * 2 * Math.PI;
        double radius = SaccadeRadius * Math.Sqrt(Rng.NextDouble());
        _saccadeTargetX = (float)(radius * Math.Cos(angle));
        _saccadeTargetY = (float)(radius * Math.Sin(angle));
        // Hold position for 0.5–3 seconds before moving again
        _saccadeTimeout = (float)(0.5 + Rng.NextDouble() * 2.5);
    }

    private float NextInterval() =>
        (float)(RateMinSec + Rng.NextDouble() * (RateMaxSec - RateMinSec));
}
