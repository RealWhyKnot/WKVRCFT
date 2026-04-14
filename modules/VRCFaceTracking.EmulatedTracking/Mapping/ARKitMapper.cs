using VRCFaceTracking.Core.Params.Expressions;
using VRCFaceTracking.V2;

namespace VRCFaceTracking.EmulatedTracking.Mapping;

/// <summary>
/// Maps the 52 ARKit blendshape outputs from Audio2Face-3D to UnifiedExpressions indices.
/// ARKit index ordering follows Apple's standard face AR blendshape spec.
/// </summary>
public static class ARKitMapper
{
    // ARKit blendshape names → their index in the 52-element output array
    private const int EyeBlinkLeft       = 0;
    private const int EyeLookDownLeft    = 1;
    private const int EyeLookInLeft      = 2;
    private const int EyeLookOutLeft     = 3;
    private const int EyeLookUpLeft      = 4;
    private const int EyeSquintLeft      = 5;
    private const int EyeWideLeft        = 6;
    private const int EyeBlinkRight      = 7;
    private const int EyeLookDownRight   = 8;
    private const int EyeLookInRight     = 9;
    private const int EyeLookOutRight    = 10;
    private const int EyeLookUpRight     = 11;
    private const int EyeSquintRight     = 12;
    private const int EyeWideRight       = 13;
    private const int JawForward         = 14;
    private const int JawLeft            = 15;
    private const int JawRight           = 16;
    private const int JawOpen            = 17;
    private const int MouthClose         = 18;
    private const int MouthFunnel        = 19;
    private const int MouthPucker        = 20;
    private const int MouthLeft          = 21;
    private const int MouthRight         = 22;
    private const int MouthSmileLeft     = 23;
    private const int MouthSmileRight    = 24;
    private const int MouthFrownLeft     = 25;
    private const int MouthFrownRight    = 26;
    private const int MouthDimpleLeft    = 27;
    private const int MouthDimpleRight   = 28;
    private const int MouthStretchLeft   = 29;
    private const int MouthStretchRight  = 30;
    private const int MouthRollLower     = 31;
    private const int MouthRollUpper     = 32;
    private const int MouthShrugLower    = 33;
    private const int MouthShrugUpper    = 34;
    private const int MouthPressLeft     = 35;
    private const int MouthPressRight    = 36;
    private const int MouthLowerDownLeft = 37;
    private const int MouthLowerDownRight= 38;
    private const int MouthUpperUpLeft   = 39;
    private const int MouthUpperUpRight  = 40;
    private const int BrowDownLeft       = 41;
    private const int BrowDownRight      = 42;
    private const int BrowInnerUp        = 43;
    private const int BrowOuterUpLeft    = 44;
    private const int BrowOuterUpRight   = 45;
    private const int CheekPuff          = 46;
    private const int CheekSquintLeft    = 47;
    private const int CheekSquintRight   = 48;
    private const int NoseSneerLeft      = 49;
    private const int NoseSneerRight     = 50;
    private const int TongueOut          = 51;

    // Mapping: (arkitIndex, unifiedExpression, multiplier)
    // multiplier = -1 means invert the value (e.g. blink → openness)
    private static readonly (int arkitIndex, UnifiedExpressions expr, float mult)[] Map =
    {
        // ── Jaw ──────────────────────────────────────────────────────────────
        (JawOpen,           UnifiedExpressions.JawOpen,               1f),
        (JawLeft,           UnifiedExpressions.JawLeft,               1f),
        (JawRight,          UnifiedExpressions.JawRight,              1f),
        (JawForward,        UnifiedExpressions.JawForward,            1f),

        // ── Mouth ────────────────────────────────────────────────────────────
        (MouthClose,        UnifiedExpressions.MouthClosed,           1f),
        (MouthFunnel,       UnifiedExpressions.LipFunnelUpperLeft,    1f),
        (MouthFunnel,       UnifiedExpressions.LipFunnelUpperRight,   1f),
        (MouthFunnel,       UnifiedExpressions.LipFunnelLowerLeft,    1f),
        (MouthFunnel,       UnifiedExpressions.LipFunnelLowerRight,   1f),
        (MouthPucker,       UnifiedExpressions.LipPuckerUpperLeft,    1f),
        (MouthPucker,       UnifiedExpressions.LipPuckerUpperRight,   1f),
        (MouthPucker,       UnifiedExpressions.LipPuckerLowerLeft,    1f),
        (MouthPucker,       UnifiedExpressions.LipPuckerLowerRight,   1f),
        (MouthSmileLeft,    UnifiedExpressions.MouthCornerPullLeft,   1f),
        (MouthSmileRight,   UnifiedExpressions.MouthCornerPullRight,  1f),
        (MouthSmileLeft,    UnifiedExpressions.MouthCornerSlantLeft,  0.5f),
        (MouthSmileRight,   UnifiedExpressions.MouthCornerSlantRight, 0.5f),
        (MouthFrownLeft,    UnifiedExpressions.MouthFrownLeft,        1f),
        (MouthFrownRight,   UnifiedExpressions.MouthFrownRight,       1f),
        (MouthDimpleLeft,   UnifiedExpressions.MouthDimpleLeft,       1f),
        (MouthDimpleRight,  UnifiedExpressions.MouthDimpleRight,      1f),
        (MouthStretchLeft,  UnifiedExpressions.MouthStretchLeft,      1f),
        (MouthStretchRight, UnifiedExpressions.MouthStretchRight,     1f),
        (MouthRollLower,    UnifiedExpressions.LipSuckLowerLeft,      1f),
        (MouthRollLower,    UnifiedExpressions.LipSuckLowerRight,     1f),
        (MouthRollUpper,    UnifiedExpressions.LipSuckUpperLeft,      1f),
        (MouthRollUpper,    UnifiedExpressions.LipSuckUpperRight,     1f),
        (MouthShrugLower,   UnifiedExpressions.MouthRaiserLower,      1f),
        (MouthShrugUpper,   UnifiedExpressions.MouthRaiserUpper,      1f),
        (MouthPressLeft,    UnifiedExpressions.LipSuckLowerLeft,      0.5f),
        (MouthPressRight,   UnifiedExpressions.LipSuckLowerRight,     0.5f),
        (MouthLowerDownLeft, UnifiedExpressions.MouthLowerDownLeft,   1f),
        (MouthLowerDownRight, UnifiedExpressions.MouthLowerDownRight, 1f),
        (MouthUpperUpLeft,  UnifiedExpressions.MouthUpperUpLeft,      1f),
        (MouthUpperUpRight, UnifiedExpressions.MouthUpperUpRight,     1f),
        (MouthLeft,         UnifiedExpressions.MouthUpperLeft,        1f),
        (MouthLeft,         UnifiedExpressions.MouthLowerLeft,        1f),
        (MouthRight,        UnifiedExpressions.MouthUpperRight,       1f),
        (MouthRight,        UnifiedExpressions.MouthLowerRight,       1f),

        // ── Cheeks ───────────────────────────────────────────────────────────
        (CheekPuff,         UnifiedExpressions.CheekPuffLeft,         1f),
        (CheekPuff,         UnifiedExpressions.CheekPuffRight,        1f),
        (CheekSquintLeft,   UnifiedExpressions.CheekSquintLeft,       1f),
        (CheekSquintRight,  UnifiedExpressions.CheekSquintRight,      1f),

        // ── Brows ────────────────────────────────────────────────────────────
        (BrowDownLeft,      UnifiedExpressions.BrowLowererLeft,       1f),
        (BrowDownRight,     UnifiedExpressions.BrowLowererRight,      1f),
        (BrowInnerUp,       UnifiedExpressions.BrowInnerUpLeft,       1f),
        (BrowInnerUp,       UnifiedExpressions.BrowInnerUpRight,      1f),
        (BrowOuterUpLeft,   UnifiedExpressions.BrowOuterUpLeft,       1f),
        (BrowOuterUpRight,  UnifiedExpressions.BrowOuterUpRight,      1f),

        // ── Nose ─────────────────────────────────────────────────────────────
        (NoseSneerLeft,     UnifiedExpressions.NoseSneerLeft,         1f),
        (NoseSneerRight,    UnifiedExpressions.NoseSneerRight,        1f),

        // ── Tongue ───────────────────────────────────────────────────────────
        (TongueOut,         UnifiedExpressions.TongueOut,             1f),
    };

    /// <summary>
    /// Converts 52 ARKit blendshape weights into ITrackingDataWriter calls.
    /// </summary>
    public static void Apply(float[] arkit, ITrackingDataWriter writer, float intensity = 1f)
    {
        // Accumulate per-expression (multiple ARKit values can map to same expression)
        var accumulated = new float[(int)UnifiedExpressions.Max + 1];

        foreach (var (idx, expr, mult) in Map)
        {
            if (idx >= arkit.Length) continue;
            float value = Math.Clamp(arkit[idx] * mult * intensity, 0f, 1f);
            int ei = (int)expr;
            accumulated[ei] = Math.Min(1f, accumulated[ei] + value);
        }

        // Write non-zero expressions
        for (int i = 0; i < accumulated.Length; i++)
        {
            if (accumulated[i] > 0f)
                writer.SetExpression(i, accumulated[i]);
        }
    }
}
