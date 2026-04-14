using VRCFaceTracking.V2.Configuration;

namespace VRCFaceTracking.EmulatedTracking;

public static class EmulatedTrackingConfig
{
    public const string KeyMicDevice = "mic_device_index";
    public const string KeyMicGain = "mic_gain";
    public const string KeyExpressionIntensity = "expression_intensity";
    public const string KeyHeadIntensity = "head_intensity";
    public const string KeyEnableHead = "enable_head";

    public static ConfigSchema BuildSchema()
    {
        return new ConfigSchema
        {
            Fields = new List<ConfigField>
            {
                new ConfigField
                {
                    Key = KeyMicDevice,
                    Label = "Microphone Device Index",
                    Description = "Device index for audio capture (-1 = default)",
                    Type = ConfigFieldType.Int,
                    DefaultValue = -1,
                    Min = -1,
                    Max = 32
                },
                new ConfigField
                {
                    Key = KeyMicGain,
                    Label = "Microphone Gain",
                    Description = "Input gain multiplier (1.0 = no change)",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 1.0f,
                    Min = 0.1f,
                    Max = 10.0f
                },
                new ConfigField
                {
                    Key = KeyExpressionIntensity,
                    Label = "Expression Intensity",
                    Description = "Scale all expression outputs (0.0–2.0)",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 1.0f,
                    Min = 0.0f,
                    Max = 2.0f
                },
                new ConfigField
                {
                    Key = KeyHeadIntensity,
                    Label = "Head Movement Intensity",
                    Description = "Scale prosody-driven head movement",
                    Type = ConfigFieldType.Float,
                    DefaultValue = 1.0f,
                    Min = 0.0f,
                    Max = 2.0f
                },
                new ConfigField
                {
                    Key = KeyEnableHead,
                    Label = "Enable Head Movement",
                    Description = "Drive head rotation from speech prosody",
                    Type = ConfigFieldType.Bool,
                    DefaultValue = true
                }
            }
        };
    }
}
