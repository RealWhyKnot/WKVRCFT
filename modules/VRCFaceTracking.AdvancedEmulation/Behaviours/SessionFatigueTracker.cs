namespace VRCFaceTracking.AdvancedEmulation.Behaviours;

/// <summary>
/// Tracks how long the current session has been running and derives a fatigue level
/// that increases gradually over time.  The fatigue level drives yawn probability,
/// extra eye droop, and a slower blink rate in the outer module.
///
/// Yawn scheduling: a Poisson-like process fires yawn triggers at an average rate
/// proportional to the current fatigue level, clamped so yawns never occur more
/// often than once every 2 minutes.
/// </summary>
public sealed class SessionFatigueTracker
{
    // ---- Configuration -------------------------------------------------
    /// <summary>Multiplier on yawn frequency [0, 3].  1 = default schedule.</summary>
    public float YawnFrequency { get; set; } = 1f;

    // ---- Session state -------------------------------------------------
    private readonly DateTime _sessionStart = DateTime.UtcNow;
    private float _yawnCooldown;        // seconds until a yawn is allowed
    private const float YawnCooldownMin = 120f; // 2-minute minimum gap

    private static readonly Random Rng = Random.Shared;

    // ---- Outputs -------------------------------------------------------
    /// <summary>
    /// Session fatigue in [0, 1].  0 for the first 15 minutes, rising to 1 at ~2 h.
    /// </summary>
    public float FatigueLevel { get; private set; }

    /// <summary>
    /// True for exactly one tick when a yawn should be triggered.
    /// The caller is responsible for starting the yawn animation.
    /// </summary>
    public bool YawnTriggered { get; private set; }

    /// <param name="deltaTime">Elapsed seconds since last call.</param>
    public void Update(float deltaTime)
    {
        YawnTriggered = false;

        double elapsedMin = (DateTime.UtcNow - _sessionStart).TotalMinutes;

        // Fatigue ramp: 0 for first 15 min, then linear to 1 at 120 min
        FatigueLevel = (float)Math.Clamp((elapsedMin - 15.0) / 105.0, 0.0, 1.0);

        if (FatigueLevel < 0.01f || YawnFrequency <= 0f)
        {
            _yawnCooldown = Math.Max(0f, _yawnCooldown - deltaTime);
            return;
        }

        _yawnCooldown = Math.Max(0f, _yawnCooldown - deltaTime);

        if (_yawnCooldown > 0f) return;

        // Poisson process: base rate = 1 yawn per 10 min at full fatigue,
        // scaled by FatigueLevel and YawnFrequency.
        // P(trigger this frame) = rate * deltaTime
        float ratePerSec = (FatigueLevel * YawnFrequency) / (10f * 60f);
        if (Rng.NextDouble() < ratePerSec * deltaTime)
        {
            YawnTriggered = true;
            // Min cooldown + random extra [0, 4 min] so yawns aren't rhythmically predictable
            _yawnCooldown = YawnCooldownMin + (float)(Rng.NextDouble() * 240f);
        }
    }
}
