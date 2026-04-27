using Microsoft.Extensions.Logging;
using VRCFaceTracking.AdvancedEmulation.Audio;
using VRCFaceTracking.AdvancedEmulation.Behaviours;
using VRCFaceTracking.SDKv2.Expressions;
using VRCFaceTracking.V2;

namespace VRCFaceTracking.AdvancedEmulation;

/// <summary>
/// Advanced Emulation — synthesises plausible eye and face data without dedicated
/// tracking hardware.
///
/// Features (all individually toggleable; <b>all off by default</b>):
///   • Natural blink rhythm  — 15–20 blinks/min, randomised timing, double-blinks
///   • Saccades              — micro gaze drift during idle
///   • Mic → Jaw             — RMS amplitude drives jaw open/close
///   • Mic → Vowels          — spectral formant analysis → A/E/I/O lip shapes
///   • Mic → Laughter        — rapid amplitude bursts → cheek puff, smile, eye squint
///   • Mic → Breathing       — sustained quiet amplitude → subtle jaw movement
///   • Emotional state       — mic-activity history → engaged/idle/sleeping
///                             driving eye droop, brow furrow, blink-rate modulation
///   • Tilt asymmetry        — simulated slow head tilt → per-eye openness delta
///   • Session fatigue       — session duration → increasing fatigue, yawn animation,
///                             brow lowerer, slower blinks
///
/// This module declares Eye and Expression capabilities.
/// </summary>
[ModuleMetadata(
    Name        = "Advanced Emulation",
    Description = "Behavioural eye emulation: natural blink rhythms, saccades, emotional state, " +
                  "mic-driven jaw/vowels/laughter, session fatigue with yawning — no dedicated hardware required.",
    Author      = "VRCFaceTracking",
    Version     = "1.1.0")]
public class AdvancedEmulationModule : ITrackingModuleV2
{
    public ModuleCapabilities Capabilities => ModuleCapabilities.Eye | ModuleCapabilities.Expression;

    // ---- Feature flags (defaults match config schema defaults = all false) --
    private bool _enableBlinking       = false;
    private bool _enableSaccades       = false;
    private bool _enableMicJaw         = false;
    private bool _enableMicVowels      = false;
    private bool _enableEmotionalState = false;
    private bool _enableTiltAsymmetry  = false;
    private bool _enableLaughter       = false;
    private bool _enableBreathing      = false;
    private bool _enableSessionFatigue = false;

    // ---- Tuning -----------------------------------------------------------
    private float _micJawSensitivity    = 2.5f;
    private float _micVowelSensitivity  = 1.5f;
    private float _laughterSensitivity  = 1.0f;

    // ---- Sub-systems ------------------------------------------------------
    private IModuleContext?         _context;
    private MicAnalyser?            _mic;
    private BlinkController?        _blink;
    private EmotionalStateTracker?  _emotion;
    private TiltSimulator?          _tilt;
    private MicPatternDetector?     _patterns;
    private SessionFatigueTracker?  _fatigue;
    private YawnAnimator?           _yawn;

    // ---- Timing -----------------------------------------------------------
    private DateTime _lastTick = DateTime.UtcNow;

    // ---- Expression indices (cached) --------------------------------------
    // Eyes
    private static readonly int IdxEyeSquintR      = (int)UnifiedExpressions.EyeSquintRight;
    private static readonly int IdxEyeSquintL      = (int)UnifiedExpressions.EyeSquintLeft;
    private static readonly int IdxEyeWideR        = (int)UnifiedExpressions.EyeWideRight;
    private static readonly int IdxEyeWideL        = (int)UnifiedExpressions.EyeWideLeft;
    // Brows
    private static readonly int IdxBrowLowererR    = (int)UnifiedExpressions.BrowLowererRight;
    private static readonly int IdxBrowLowererL    = (int)UnifiedExpressions.BrowLowererLeft;
    private static readonly int IdxBrowInnerUpR    = (int)UnifiedExpressions.BrowInnerUpRight;
    private static readonly int IdxBrowInnerUpL    = (int)UnifiedExpressions.BrowInnerUpLeft;
    private static readonly int IdxBrowOuterUpR    = (int)UnifiedExpressions.BrowOuterUpRight;
    private static readonly int IdxBrowOuterUpL    = (int)UnifiedExpressions.BrowOuterUpLeft;
    private static readonly int IdxBrowPinchR      = (int)UnifiedExpressions.BrowPinchRight;
    private static readonly int IdxBrowPinchL      = (int)UnifiedExpressions.BrowPinchLeft;
    // Nose
    private static readonly int IdxNasalConstrictR = (int)UnifiedExpressions.NasalConstrictRight;
    private static readonly int IdxNasalConstrictL = (int)UnifiedExpressions.NasalConstrictLeft;
    private static readonly int IdxNasalDilationR  = (int)UnifiedExpressions.NasalDilationRight;
    private static readonly int IdxNasalDilationL  = (int)UnifiedExpressions.NasalDilationLeft;
    // Cheeks
    private static readonly int IdxCheekSquintR    = (int)UnifiedExpressions.CheekSquintRight;
    private static readonly int IdxCheekSquintL    = (int)UnifiedExpressions.CheekSquintLeft;
    private static readonly int IdxCheekPuffR      = (int)UnifiedExpressions.CheekPuffRight;
    private static readonly int IdxCheekPuffL      = (int)UnifiedExpressions.CheekPuffLeft;
    // Jaw / mouth
    private static readonly int IdxJawOpen         = (int)UnifiedExpressions.JawOpen;
    private static readonly int IdxMouthClosed     = (int)UnifiedExpressions.MouthClosed;
    private static readonly int IdxMouthCornerPullR = (int)UnifiedExpressions.MouthCornerPullRight;
    private static readonly int IdxMouthCornerPullL = (int)UnifiedExpressions.MouthCornerPullLeft;
    private static readonly int IdxMouthStretchR   = (int)UnifiedExpressions.MouthStretchRight;
    private static readonly int IdxMouthStretchL   = (int)UnifiedExpressions.MouthStretchLeft;
    // Lip shapes
    private static readonly int IdxLipFunnelUR     = (int)UnifiedExpressions.LipFunnelUpperRight;
    private static readonly int IdxLipFunnelUL     = (int)UnifiedExpressions.LipFunnelUpperLeft;
    private static readonly int IdxLipFunnelLR     = (int)UnifiedExpressions.LipFunnelLowerRight;
    private static readonly int IdxLipFunnelLL     = (int)UnifiedExpressions.LipFunnelLowerLeft;
    private static readonly int IdxLipPuckerUR     = (int)UnifiedExpressions.LipPuckerUpperRight;
    private static readonly int IdxLipPuckerUL     = (int)UnifiedExpressions.LipPuckerUpperLeft;
    private static readonly int IdxLipPuckerLR     = (int)UnifiedExpressions.LipPuckerLowerRight;
    private static readonly int IdxLipPuckerLL     = (int)UnifiedExpressions.LipPuckerLowerLeft;
    private static readonly int IdxMouthUpperUpR   = (int)UnifiedExpressions.MouthUpperUpRight;
    private static readonly int IdxMouthUpperUpL   = (int)UnifiedExpressions.MouthUpperUpLeft;
    private static readonly int IdxMouthLowerDownR = (int)UnifiedExpressions.MouthLowerDownRight;
    private static readonly int IdxMouthLowerDownL = (int)UnifiedExpressions.MouthLowerDownLeft;

    // ---- ITrackingModuleV2 lifecycle --------------------------------------

    public Task<bool> InitializeAsync(IModuleContext context)
    {
        _context = context;

        // ---- Read feature toggles -----------------------------------
        _enableBlinking       = context.Settings.GetSetting(AdvancedEmulationConfig.KeyEnableBlinking,       false);
        _enableSaccades       = context.Settings.GetSetting(AdvancedEmulationConfig.KeyEnableSaccades,       false);
        _enableMicJaw         = context.Settings.GetSetting(AdvancedEmulationConfig.KeyEnableMicJaw,         false);
        _enableMicVowels      = context.Settings.GetSetting(AdvancedEmulationConfig.KeyEnableMicVowels,      false);
        _enableEmotionalState = context.Settings.GetSetting(AdvancedEmulationConfig.KeyEnableEmotionalState, false);
        _enableTiltAsymmetry  = context.Settings.GetSetting(AdvancedEmulationConfig.KeyEnableTiltAsymmetry,  false);
        _enableLaughter       = context.Settings.GetSetting(AdvancedEmulationConfig.KeyEnableLaughter,       false);
        _enableBreathing      = context.Settings.GetSetting(AdvancedEmulationConfig.KeyEnableBreathing,      false);
        _enableSessionFatigue = context.Settings.GetSetting(AdvancedEmulationConfig.KeyEnableSessionFatigue, false);

        // ---- Read tuning ---------------------------------------------
        _micJawSensitivity   = context.Settings.GetSetting(AdvancedEmulationConfig.KeyMicJawSensitivity,   2.5f);
        _micVowelSensitivity = context.Settings.GetSetting(AdvancedEmulationConfig.KeyMicVowelSensitivity, 1.5f);
        _laughterSensitivity = context.Settings.GetSetting(AdvancedEmulationConfig.KeyLaughterSensitivity,  1.0f);

        float blinkMin   = context.Settings.GetSetting(AdvancedEmulationConfig.KeyBlinkRateMin,    3.0f);
        float blinkMax   = context.Settings.GetSetting(AdvancedEmulationConfig.KeyBlinkRateMax,    4.0f);
        float blinkDurMs = context.Settings.GetSetting(AdvancedEmulationConfig.KeyBlinkDurationMs, 80f);
        float dblChance  = context.Settings.GetSetting(AdvancedEmulationConfig.KeyDoubleBlink,     0.1f);
        float saccRad    = context.Settings.GetSetting(AdvancedEmulationConfig.KeySaccadeRadius,   0.06f);
        float saccSpd    = context.Settings.GetSetting(AdvancedEmulationConfig.KeySaccadeSpeed,    0.4f);
        float idleTimeout  = context.Settings.GetSetting(AdvancedEmulationConfig.KeyIdleTimeoutSec,  8.0f);
        float sleepTimeout = context.Settings.GetSetting(AdvancedEmulationConfig.KeySleepTimeoutSec, 30.0f);
        float tiltScale  = context.Settings.GetSetting(AdvancedEmulationConfig.KeyTiltScale,       0.15f);
        float yawnFreq   = context.Settings.GetSetting(AdvancedEmulationConfig.KeyYawnFrequency,    1.0f);

        context.RegisterConfigSchema(AdvancedEmulationConfig.BuildSchema());

        // ---- Blink ---------------------------------------------------
        _blink = new BlinkController
        {
            RateMinSec      = blinkMin,
            RateMaxSec      = blinkMax,
            HalfDurationSec = blinkDurMs / 1000f,
            DoubleBlink     = dblChance,
            SaccadeRadius   = saccRad,
            SaccadeSpeed    = saccSpd,
        };

        // ---- Emotional state -----------------------------------------
        _emotion = new EmotionalStateTracker
        {
            IdleTimeoutSec  = idleTimeout,
            SleepTimeoutSec = sleepTimeout,
        };

        // ---- Tilt simulator ------------------------------------------
        _tilt = new TiltSimulator { Scale = tiltScale };

        // ---- Mic-driven pattern detection ----------------------------
        _patterns = new MicPatternDetector();

        // ---- Session fatigue -----------------------------------------
        _fatigue = new SessionFatigueTracker { YawnFrequency = yawnFreq };

        // ---- Yawn animator -------------------------------------------
        _yawn = new YawnAnimator();

        // ---- Microphone (start only when any mic feature is active) ---
        bool anyMicFeature = _enableMicJaw || _enableMicVowels || _enableEmotionalState
                          || _enableLaughter || _enableBreathing;
        if (anyMicFeature)
        {
            int   micDevice = context.Settings.GetSetting(AdvancedEmulationConfig.KeyMicDeviceIndex, -1);
            float micGain   = context.Settings.GetSetting(AdvancedEmulationConfig.KeyMicGain,        1.0f);
            _mic = new MicAnalyser { GainMultiplier = micGain };
            if (!_mic.Start(micDevice))
            {
                context.Logger.LogWarning(
                    "Advanced Emulation: microphone capture failed — all mic-driven features will be silent");
                _mic.Dispose();
                _mic = null;
            }
        }

        context.Logger.LogInformation(
            "Advanced Emulation v1.1 initialised [blink={Blink} sacc={Sacc} jaw={J} " +
            "vowels={V} emotion={Em} tilt={Ti} laugh={La} breath={Br} fatigue={Fa}]",
            _enableBlinking, _enableSaccades, _enableMicJaw,
            _enableMicVowels, _enableEmotionalState, _enableTiltAsymmetry,
            _enableLaughter, _enableBreathing, _enableSessionFatigue);

        return Task.FromResult(true);
    }

    public Task UpdateAsync(CancellationToken ct)
    {
        if (_context == null || _blink == null) return Task.CompletedTask;

        // ---- Delta time -----------------------------------------------
        var now = DateTime.UtcNow;
        float dt = Math.Clamp((float)(now - _lastTick).TotalSeconds, 0f, 0.2f);
        _lastTick = now;

        // ---- Mic data -------------------------------------------------
        float amplitude = _mic?.Amplitude ?? 0f;
        float vowelA    = _mic?.VowelA    ?? 0f;
        float vowelO    = _mic?.VowelO    ?? 0f;
        float vowelEE   = _mic?.VowelEE   ?? 0f;

        // ---- Pattern detection ----------------------------------------
        _patterns?.Update(amplitude, dt);
        float laughter  = _patterns?.LaughterProbability ?? 0f;
        float breathing = _patterns?.BreathingLevel      ?? 0f;
        float sigh      = _patterns?.SighProgress        ?? 0f;
        bool  whisper   = _patterns?.IsWhispering        ?? false;
        bool  loud      = _patterns?.IsLoudSpeaking      ?? false;

        // Apply sensitivity to laughter
        laughter = Math.Clamp(laughter * _laughterSensitivity, 0f, 1f);

        // ---- Emotional state ------------------------------------------
        float droopFromEmotion   = 0f;
        float blinkIntervalScale = 1f;
        EmotionalStateTracker.State emotionState = EmotionalStateTracker.State.Engaged;
        if (_enableEmotionalState && _emotion != null)
        {
            _emotion.Update(amplitude, dt);
            droopFromEmotion   = _emotion.EyeDroop;
            blinkIntervalScale = _emotion.BlinkIntervalScale;
            emotionState       = _emotion.CurrentState;
        }

        // ---- Session fatigue & yawning --------------------------------
        float fatigueLevel = 0f;
        if (_enableSessionFatigue && _fatigue != null && _yawn != null)
        {
            _fatigue.Update(dt);
            fatigueLevel = _fatigue.FatigueLevel;

            if (_fatigue.YawnTriggered)
                _yawn.Trigger();
            _yawn.Tick(dt);
        }

        // ---- Blink + saccade ------------------------------------------
        // Fatigue further slows blink rate on top of emotional state
        float effectiveBlinkScale = blinkIntervalScale * (1f - fatigueLevel * 0.4f);
        if (_enableBlinking)
            _blink.Tick(dt, effectiveBlinkScale, _enableSaccades);

        float blinkOpenness = _enableBlinking ? _blink.BlinkOpenness : 1f;
        float saccadeX = _enableSaccades ? _blink.SaccadeX : 0f;
        float saccadeY = _enableSaccades ? _blink.SaccadeY : 0f;

        // ---- Tilt asymmetry -------------------------------------------
        float leftTiltDelta  = 0f;
        float rightTiltDelta = 0f;
        if (_enableTiltAsymmetry && _tilt != null)
        {
            _tilt.Tick(dt);
            leftTiltDelta  = _tilt.LeftEyeModifier;
            rightTiltDelta = _tilt.RightEyeModifier;
        }

        // ---- Excitement level (loud mic + laughter + engaged) ---------
        float excited = 0f;
        if (emotionState == EmotionalStateTracker.State.Engaged && _enableLaughter)
            excited = Math.Clamp(laughter * (loud ? 1.5f : 0.7f), 0f, 1f);

        // ---- AFK / idle compound state --------------------------------
        // Zero mic + still for a long time → extra expression flatness
        bool isAfk = emotionState == EmotionalStateTracker.State.Sleeping && amplitude < 0.002f;

        // ---- Compose eye openness -------------------------------------
        float yawnSquint = _yawn?.EyeSquint ?? 0f;
        float baseOpenL = 1f - droopFromEmotion - fatigueLevel * 0.15f + leftTiltDelta;
        float baseOpenR = 1f - droopFromEmotion - fatigueLevel * 0.15f + rightTiltDelta;
        // Yawn closes eyes
        baseOpenL -= yawnSquint * 0.8f;
        baseOpenR -= yawnSquint * 0.8f;
        baseOpenL = Math.Clamp(baseOpenL, 0f, 1f) * blinkOpenness;
        baseOpenR = Math.Clamp(baseOpenR, 0f, 1f) * blinkOpenness;
        // AFK: further droop
        if (isAfk) { baseOpenL *= 0.4f; baseOpenR *= 0.4f; }

        bool anyEyeActive = _enableBlinking || _enableEmotionalState
                         || _enableTiltAsymmetry || _enableSessionFatigue;
        if (anyEyeActive)
        {
            _context.TrackingData.SetLeftEye(saccadeX, saccadeY, baseOpenL, 4.0f);
            _context.TrackingData.SetRightEye(saccadeX, saccadeY, baseOpenR, 4.0f);
        }

        // ================================================================
        // EXPRESSION OUTPUTS
        // ================================================================

        // ---- Eye squint: from emotional droop + yawn + laughter -------
        {
            float tirednessSquint = Math.Clamp((droopFromEmotion - 0.05f) / 0.40f, 0f, 0.7f);
            float laughSquint     = _enableLaughter ? laughter * 0.5f : 0f;
            float totalSquint     = Math.Clamp(tirednessSquint + yawnSquint * 0.4f + laughSquint, 0f, 1f);
            if (totalSquint > 0.01f)
            {
                _context.TrackingData.SetExpression(IdxEyeSquintR, totalSquint);
                _context.TrackingData.SetExpression(IdxEyeSquintL, totalSquint);
            }
        }

        // ---- Eye wide: from sigh (brief startle) ----------------------
        if (sigh > 0.5f)
        {
            float wide = (sigh - 0.5f) * 0.5f;
            _context.TrackingData.SetExpression(IdxEyeWideR, wide);
            _context.TrackingData.SetExpression(IdxEyeWideL, wide);
        }

        // ---- Brow lowerer: tired/fatigued/AFK -------------------------
        {
            float browLower = fatigueLevel * 0.35f + droopFromEmotion * 0.45f;
            if (isAfk) browLower = Math.Min(1f, browLower + 0.25f);
            if (browLower > 0.01f)
            {
                _context.TrackingData.SetExpression(IdxBrowLowererR, Math.Min(1f, browLower));
                _context.TrackingData.SetExpression(IdxBrowLowererL, Math.Min(1f, browLower));
                // Brow pinch: concentration/focus when in Idle state
                float pinch = emotionState == EmotionalStateTracker.State.Idle ? browLower * 0.4f : 0f;
                _context.TrackingData.SetExpression(IdxBrowPinchR, pinch);
                _context.TrackingData.SetExpression(IdxBrowPinchL, pinch);
            }
        }

        // ---- Brow raise: excited + yawn peak --------------------------
        {
            float yawnBrowRaise = _yawn?.BrowRaiseInner ?? 0f;
            float excitedRaise  = _enableLaughter ? excited * 0.4f : 0f;
            float totalRaise    = Math.Clamp(yawnBrowRaise + excitedRaise, 0f, 1f);
            if (totalRaise > 0.01f)
            {
                _context.TrackingData.SetExpression(IdxBrowInnerUpR, totalRaise);
                _context.TrackingData.SetExpression(IdxBrowInnerUpL, totalRaise);
                _context.TrackingData.SetExpression(IdxBrowOuterUpR, totalRaise * 0.6f);
                _context.TrackingData.SetExpression(IdxBrowOuterUpL, totalRaise * 0.6f);
            }
        }

        // ---- Nose wrinkle: yawn wind-up + disgust/concentration -------
        {
            float yawnNoseWrinkle = _yawn?.NoseWrinkle ?? 0f;
            if (yawnNoseWrinkle > 0.01f)
            {
                _context.TrackingData.SetExpression(IdxNasalConstrictR, yawnNoseWrinkle * 0.5f);
                _context.TrackingData.SetExpression(IdxNasalConstrictL, yawnNoseWrinkle * 0.5f);
            }
            // Nasal dilation: loud/excited speech
            if (_enableLaughter && excited > 0.3f)
            {
                float dilation = (excited - 0.3f) * 0.5f;
                _context.TrackingData.SetExpression(IdxNasalDilationR, dilation);
                _context.TrackingData.SetExpression(IdxNasalDilationL, dilation);
            }
        }

        // ---- Cheek puff + squint: laughter ----------------------------
        if (_enableLaughter && laughter > 0.01f)
        {
            float puff   = Math.Clamp((laughter - 0.3f) * 1.5f, 0f, 0.7f);
            float squint = Math.Clamp(laughter * 0.6f, 0f, 0.8f);
            _context.TrackingData.SetExpression(IdxCheekPuffR, puff);
            _context.TrackingData.SetExpression(IdxCheekPuffL, puff);
            _context.TrackingData.SetExpression(IdxCheekSquintR, squint);
            _context.TrackingData.SetExpression(IdxCheekSquintL, squint);
        }

        // ---- Smile / lip corner pull: excited + laughter --------------
        if (_enableLaughter)
        {
            float smile = Math.Clamp(excited * 0.7f + laughter * 0.35f, 0f, 0.9f);
            if (smile > 0.01f)
            {
                _context.TrackingData.SetExpression(IdxMouthCornerPullR, smile);
                _context.TrackingData.SetExpression(IdxMouthCornerPullL, smile);
                // Yawn grimace overrides corners via LipCornerPull weight
                float yawnCorner = _yawn?.LipCornerPull ?? 0f;
                if (yawnCorner > smile)
                {
                    _context.TrackingData.SetExpression(IdxMouthStretchR, yawnCorner * 0.6f);
                    _context.TrackingData.SetExpression(IdxMouthStretchL, yawnCorner * 0.6f);
                }
            }
        }

        // ---- Jaw from mic amplitude + yawn ----------------------------
        bool yawnIsActive = _yawn?.IsYawning ?? false;
        if (yawnIsActive)
        {
            // Yawn takes full control of jaw when active
            float yawnJaw = _yawn!.JawOpenness;
            _context.TrackingData.SetExpression(IdxJawOpen,     yawnJaw);
            _context.TrackingData.SetExpression(IdxMouthClosed, Math.Max(0f, 0.2f - yawnJaw));
        }
        else if (_enableMicJaw)
        {
            float jaw = Math.Clamp(amplitude * _micJawSensitivity, 0f, 1f);
            _context.TrackingData.SetExpression(IdxJawOpen,     jaw);
            _context.TrackingData.SetExpression(IdxMouthClosed, Math.Max(0f, 1f - jaw * 1.5f));
        }
        else if (_enableBreathing && breathing > 0.1f)
        {
            // Breathing: subtle jaw oscillation using time-based sine
            float breathJaw = breathing * 0.05f * (MathF.Sin((float)DateTime.UtcNow.TimeOfDay.TotalSeconds * 0.4f) * 0.5f + 0.5f);
            _context.TrackingData.SetExpression(IdxJawOpen, breathJaw);
        }

        // ---- Vowel shapes from spectral analysis ----------------------
        if (_enableMicVowels && amplitude > 0.01f)
        {
            float s = _micVowelSensitivity;

            // "Oh/Aw" (rounded) → lip funnel
            float funnel = Math.Clamp(vowelO * s, 0f, 1f);
            _context.TrackingData.SetExpression(IdxLipFunnelUR, funnel);
            _context.TrackingData.SetExpression(IdxLipFunnelUL, funnel);
            _context.TrackingData.SetExpression(IdxLipFunnelLR, funnel * 0.8f);
            _context.TrackingData.SetExpression(IdxLipFunnelLL, funnel * 0.8f);

            // "Oo/Uu" (round + pucker) → lip pucker (use O formant reduced)
            float pucker = Math.Clamp(vowelO * s * 0.5f, 0f, 0.6f);
            _context.TrackingData.SetExpression(IdxLipPuckerUR, pucker);
            _context.TrackingData.SetExpression(IdxLipPuckerUL, pucker);
            _context.TrackingData.SetExpression(IdxLipPuckerLR, pucker);
            _context.TrackingData.SetExpression(IdxLipPuckerLL, pucker);

            // "Ee/Ih" (spread) → mouth upper up + wide + mouth stretch
            float spread = Math.Clamp(vowelEE * s, 0f, 1f);
            _context.TrackingData.SetExpression(IdxMouthUpperUpR, spread * 0.5f);
            _context.TrackingData.SetExpression(IdxMouthUpperUpL, spread * 0.5f);
            _context.TrackingData.SetExpression(IdxEyeWideR,      spread * 0.15f);
            _context.TrackingData.SetExpression(IdxEyeWideL,      spread * 0.15f);
            _context.TrackingData.SetExpression(IdxMouthStretchR,  spread * 0.4f);
            _context.TrackingData.SetExpression(IdxMouthStretchL,  spread * 0.4f);

            // "Ah" (open) → lower jaw + lower lip
            float open = Math.Clamp(vowelA * s, 0f, 1f);
            _context.TrackingData.SetExpression(IdxMouthLowerDownR, open * 0.6f);
            _context.TrackingData.SetExpression(IdxMouthLowerDownL, open * 0.6f);

            // Whisper → reduced funnel, slightly pursed
            if (whisper && !loud)
            {
                _context.TrackingData.SetExpression(IdxLipFunnelUR, funnel * 0.4f);
                _context.TrackingData.SetExpression(IdxLipFunnelUL, funnel * 0.4f);
            }
        }

        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _mic?.Stop();
        _mic?.Dispose();
        _context?.Logger.LogInformation("Advanced Emulation shut down");
        return Task.CompletedTask;
    }
}
