using VRCFaceTracking.V2.Configuration;

namespace VRCFaceTracking.AdvancedEmulation;

public static class AdvancedEmulationConfig
{
    // ---- Feature toggles (all off by default) ----------------------------
    public const string KeyEnableBlinking       = "enable_blinking";
    public const string KeyEnableSaccades       = "enable_saccades";
    public const string KeyEnableMicJaw         = "enable_mic_jaw";
    public const string KeyEnableMicVowels      = "enable_mic_vowels";
    public const string KeyEnableEmotionalState = "enable_emotional_state";
    public const string KeyEnableTiltAsymmetry  = "enable_tilt_asymmetry";
    public const string KeyEnableLaughter       = "enable_laughter";
    public const string KeyEnableBreathing      = "enable_breathing";
    public const string KeyEnableSessionFatigue = "enable_session_fatigue";

    // ---- Microphone -------------------------------------------------------
    public const string KeyMicDeviceIndex       = "mic_device_index";
    public const string KeyMicGain              = "mic_gain";

    // ---- Blink ------------------------------------------------------------
    public const string KeyBlinkRateMin         = "blink_rate_min";
    public const string KeyBlinkRateMax         = "blink_rate_max";
    public const string KeyBlinkDurationMs      = "blink_duration_ms";
    public const string KeyDoubleBlink          = "double_blink_chance";

    // ---- Saccades ---------------------------------------------------------
    public const string KeySaccadeRadius        = "saccade_radius";
    public const string KeySaccadeSpeed         = "saccade_speed";

    // ---- Mic jaw/vowels ---------------------------------------------------
    public const string KeyMicJawSensitivity    = "mic_jaw_sensitivity";
    public const string KeyMicVowelSensitivity  = "mic_vowel_sensitivity";

    // ---- Emotional state --------------------------------------------------
    public const string KeyIdleTimeoutSec       = "idle_timeout_sec";
    public const string KeySleepTimeoutSec      = "sleep_timeout_sec";

    // ---- Tilt asymmetry ---------------------------------------------------
    public const string KeyTiltScale            = "tilt_scale";

    // ---- Laughter ---------------------------------------------------------
    public const string KeyLaughterSensitivity  = "laughter_sensitivity";

    // ---- Session fatigue / yawning ----------------------------------------
    public const string KeyYawnFrequency        = "yawn_frequency";

    public static ConfigSchema BuildSchema() => new()
    {
        Fields = new List<ConfigField>
        {
            // --- Feature toggles ---
            new() { Key = KeyEnableBlinking,       Label = "Enable Natural Blinking",
                    Description = "Synthesise natural blink rhythms (15–20/min) with randomised timing and occasional double-blinks.",
                    Type = ConfigFieldType.Bool, DefaultValue = false },

            new() { Key = KeyEnableSaccades,       Label = "Enable Saccades",
                    Description = "Add small random micro-gaze movements during idle to break the stare.",
                    Type = ConfigFieldType.Bool, DefaultValue = false },

            new() { Key = KeyEnableMicJaw,         Label = "Enable Mic → Jaw",
                    Description = "Drive jaw open/close from microphone amplitude.",
                    Type = ConfigFieldType.Bool, DefaultValue = false },

            new() { Key = KeyEnableMicVowels,      Label = "Enable Mic → Vowels",
                    Description = "Drive lip shapes from basic spectral analysis of mic audio (A/E/I/O/U vowels).",
                    Type = ConfigFieldType.Bool, DefaultValue = false },

            new() { Key = KeyEnableEmotionalState, Label = "Enable Emotional State",
                    Description = "Infer engaged/idle/tired/sleeping state from mic activity and modulate eye openness, blink rate, and droop accordingly.",
                    Type = ConfigFieldType.Bool, DefaultValue = false },

            new() { Key = KeyEnableTiltAsymmetry,  Label = "Enable Tilt Eye Asymmetry",
                    Description = "Simulate head-tilt asymmetry: when the head tilts the lower eye droops slightly relative to the upper one.",
                    Type = ConfigFieldType.Bool, DefaultValue = false },

            new() { Key = KeyEnableLaughter,       Label = "Enable Laughter Detection",
                    Description = "Detect laughter from rapid mic amplitude bursts and drive cheek puff, smile, and eye squint.",
                    Type = ConfigFieldType.Bool, DefaultValue = false },

            new() { Key = KeyEnableBreathing,      Label = "Enable Breathing Detection",
                    Description = "Detect quiet breathing rhythm and drive subtle jaw/mouth movement during silence.",
                    Type = ConfigFieldType.Bool, DefaultValue = false },

            new() { Key = KeyEnableSessionFatigue, Label = "Enable Session Fatigue",
                    Description = "Gradually increase fatigue (brow droop, slower blinks, yawning) the longer the session runs. Starts after 15 minutes.",
                    Type = ConfigFieldType.Bool, DefaultValue = false },

            // --- Microphone ---
            new() { Key = KeyMicDeviceIndex,       Label = "Microphone Device",
                    Description = "Device index (-1 = system default).",
                    Type = ConfigFieldType.Int, DefaultValue = -1, Min = -1, Max = 32 },

            new() { Key = KeyMicGain,              Label = "Microphone Gain",
                    Description = "Input gain multiplier applied before analysis.",
                    Type = ConfigFieldType.Float, DefaultValue = 1.0f, Min = 0.1f, Max = 10.0f },

            // --- Blink ---
            new() { Key = KeyBlinkRateMin,         Label = "Min Blink Interval (s)",
                    Description = "Minimum time between blinks at rest.",
                    Type = ConfigFieldType.Float, DefaultValue = 3.0f, Min = 0.5f, Max = 10.0f },

            new() { Key = KeyBlinkRateMax,         Label = "Max Blink Interval (s)",
                    Description = "Maximum time between blinks at rest.",
                    Type = ConfigFieldType.Float, DefaultValue = 4.0f, Min = 1.0f, Max = 15.0f },

            new() { Key = KeyBlinkDurationMs,      Label = "Blink Duration (ms)",
                    Description = "Approximate half-cycle duration of each blink.",
                    Type = ConfigFieldType.Float, DefaultValue = 80f, Min = 30f, Max = 300f },

            new() { Key = KeyDoubleBlink,          Label = "Double-Blink Chance",
                    Description = "Probability [0–1] that a blink is followed immediately by a second one.",
                    Type = ConfigFieldType.Float, DefaultValue = 0.1f, Min = 0.0f, Max = 1.0f },

            // --- Saccades ---
            new() { Key = KeySaccadeRadius,        Label = "Saccade Radius",
                    Description = "Maximum gaze offset (0–1) during idle saccades.",
                    Type = ConfigFieldType.Float, DefaultValue = 0.06f, Min = 0.01f, Max = 0.3f },

            new() { Key = KeySaccadeSpeed,         Label = "Saccade Speed",
                    Description = "How quickly the gaze drifts toward a new saccade target (0–1 per second).",
                    Type = ConfigFieldType.Float, DefaultValue = 0.4f, Min = 0.05f, Max = 2.0f },

            // --- Mic jaw/vowels ---
            new() { Key = KeyMicJawSensitivity,    Label = "Jaw Sensitivity",
                    Description = "How much RMS amplitude opens the jaw (higher = opens wider).",
                    Type = ConfigFieldType.Float, DefaultValue = 2.5f, Min = 0.5f, Max = 10.0f },

            new() { Key = KeyMicVowelSensitivity,  Label = "Vowel Sensitivity",
                    Description = "Scale factor for spectral vowel shapes.",
                    Type = ConfigFieldType.Float, DefaultValue = 1.5f, Min = 0.2f, Max = 5.0f },

            // --- Emotional state ---
            new() { Key = KeyIdleTimeoutSec,       Label = "Idle Timeout (s)",
                    Description = "Seconds of mic silence before transitioning from Engaged → Idle.",
                    Type = ConfigFieldType.Float, DefaultValue = 8.0f, Min = 2.0f, Max = 60.0f },

            new() { Key = KeySleepTimeoutSec,      Label = "Sleep Timeout (s)",
                    Description = "Seconds of silence before transitioning from Idle → Sleeping.",
                    Type = ConfigFieldType.Float, DefaultValue = 30.0f, Min = 5.0f, Max = 120.0f },

            // --- Tilt ---
            new() { Key = KeyTiltScale,            Label = "Tilt Asymmetry Scale",
                    Description = "How much simulated head tilt affects per-eye openness difference.",
                    Type = ConfigFieldType.Float, DefaultValue = 0.15f, Min = 0.0f, Max = 0.5f },

            // --- Laughter ---
            new() { Key = KeyLaughterSensitivity,  Label = "Laughter Sensitivity",
                    Description = "How readily the laughter detector fires (higher = triggers on quieter/less-intense amplitude bursts).",
                    Type = ConfigFieldType.Float, DefaultValue = 1.0f, Min = 0.2f, Max = 3.0f },

            // --- Yawn ---
            new() { Key = KeyYawnFrequency,        Label = "Yawn Frequency",
                    Description = "Multiplier on yawn rate (0 = disabled, 1 = natural, 2 = twice as often). Only takes effect when Session Fatigue is enabled.",
                    Type = ConfigFieldType.Float, DefaultValue = 1.0f, Min = 0.0f, Max = 3.0f },
        }
    };
}
