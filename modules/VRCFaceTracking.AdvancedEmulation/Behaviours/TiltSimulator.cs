namespace VRCFaceTracking.AdvancedEmulation.Behaviours;

/// <summary>
/// Simulates a slow, randomised head-tilt oscillation and derives per-eye
/// openness asymmetry from it.
///
/// In the absence of real IMU data this produces a gentle, plausible head-tilt
/// that makes the avatar feel alive and grounded.  The tilt cycles slowly
/// (period 8–20 s) with small random perturbations.
///
/// Output:
///   <see cref="LeftEyeModifier"/>  and <see cref="RightEyeModifier"/> are in
///   [-<paramref name="scale"/>, +<paramref name="scale"/>].  A positive roll
///   means the head tilts right → right eye is lower → subtract from right
///   openness, add to left openness.
/// </summary>
public sealed class TiltSimulator
{
    /// <summary>Maximum per-eye openness delta per unit of roll.</summary>
    public float Scale { get; set; } = 0.15f;

    // ---- Public outputs ------------------------------------------------
    /// <summary>Additive openness change for the left eye [-scale, +scale].</summary>
    public float LeftEyeModifier  { get; private set; }
    /// <summary>Additive openness change for the right eye [-scale, +scale].</summary>
    public float RightEyeModifier { get; private set; }
    /// <summary>Current simulated roll angle in [-1, 1] normalised.</summary>
    public float CurrentRoll      { get; private set; }

    // ---- Internal state ------------------------------------------------
    private float _phase;         // oscillator phase in radians
    private float _frequency;     // radians per second
    private float _amplitude;     // peak roll magnitude
    private float _perturbTimer;

    private static readonly Random Rng = Random.Shared;

    public TiltSimulator()
    {
        RandParameters();
    }

    public void Tick(float deltaTime)
    {
        _phase += _frequency * deltaTime;
        if (_phase > MathF.PI * 2f) _phase -= MathF.PI * 2f;

        // Slow sine-wave roll
        float roll = MathF.Sin(_phase) * _amplitude;
        CurrentRoll = roll;

        // Right tilt (roll > 0):
        //   right eye is physically lower → slightly droops more
        //   left  eye is physically higher → slightly more open
        float delta = roll * Scale;
        LeftEyeModifier  =  delta;
        RightEyeModifier = -delta;

        // Periodically vary the oscillation parameters so it doesn't feel mechanical
        _perturbTimer -= deltaTime;
        if (_perturbTimer <= 0f)
            RandParameters();
    }

    private void RandParameters()
    {
        // Period 8–20 s
        float period   = 8f + (float)Rng.NextDouble() * 12f;
        _frequency     = MathF.PI * 2f / period;
        // Amplitude 0.05–0.25 (subtle)
        _amplitude     = 0.05f + (float)Rng.NextDouble() * 0.20f;
        // Next parameter change in 10–30 s
        _perturbTimer  = 10f + (float)Rng.NextDouble() * 20f;
    }
}
