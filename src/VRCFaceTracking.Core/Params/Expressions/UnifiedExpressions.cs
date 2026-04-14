namespace VRCFaceTracking.Core.Params.Expressions
{
    /// <summary>
    /// Represents the type of Shape data being sent by UnifiedExpressionData, in the form of enumerated shapes.
    /// </summary>
    /// <remarks>These shapes have a strong basis on the underlying muscular foundations of the entire head including the face, eyes, tongue, and inner mouth expressions.</remarks>
    public enum UnifiedExpressions
    {
        #region Eye Expressions

        EyeSquintRight,
        EyeSquintLeft,
        EyeWideRight,
        EyeWideLeft,

        #endregion

        #region Eyebrow Expressions

        BrowPinchRight,
        BrowPinchLeft,
        BrowLowererRight,
        BrowLowererLeft,
        BrowInnerUpRight,
        BrowInnerUpLeft,
        BrowOuterUpRight,
        BrowOuterUpLeft,

        #endregion

        #region Nose Expressions

        NasalDilationRight,
        NasalDilationLeft,
        NasalConstrictRight,
        NasalConstrictLeft,

        #endregion

        #region Cheek Expressions

        CheekSquintRight,
        CheekSquintLeft,
        CheekPuffRight,
        CheekPuffLeft,
        CheekSuckRight,
        CheekSuckLeft,

        #endregion

        #region Jaw Exclusive Expressions

        JawOpen,
        JawRight,
        JawLeft,
        JawForward,
        JawBackward,
        JawClench,
        JawMandibleRaise,

        MouthClosed,

        #endregion

        #region Lip Expressions

        LipSuckUpperRight,
        LipSuckUpperLeft,
        LipSuckLowerRight,
        LipSuckLowerLeft,

        LipSuckCornerRight,
        LipSuckCornerLeft,

        LipFunnelUpperRight,
        LipFunnelUpperLeft,
        LipFunnelLowerRight,
        LipFunnelLowerLeft,

        LipPuckerUpperRight,
        LipPuckerUpperLeft,
        LipPuckerLowerRight,
        LipPuckerLowerLeft,

        MouthUpperUpRight,
        MouthUpperUpLeft,
        MouthUpperDeepenRight,
        MouthUpperDeepenLeft,
        NoseSneerRight,
        NoseSneerLeft,

        MouthLowerDownRight,
        MouthLowerDownLeft,

        MouthUpperRight,
        MouthUpperLeft,
        MouthLowerRight,
        MouthLowerLeft,

        MouthCornerPullRight,
        MouthCornerPullLeft,
        MouthCornerSlantRight,
        MouthCornerSlantLeft,

        MouthFrownRight,
        MouthFrownLeft,
        MouthStretchRight,
        MouthStretchLeft,

        MouthDimpleRight,
        MouthDimpleLeft,

        MouthRaiserUpper,
        MouthRaiserLower,
        MouthPressRight,
        MouthPressLeft,
        MouthTightenerRight,
        MouthTightenerLeft,

        #endregion

        #region Tongue Expressions

        TongueOut,

        TongueUp,
        TongueDown,
        TongueRight,
        TongueLeft,

        TongueRoll,
        TongueBendDown,
        TongueCurlUp,
        TongueSquish,
        TongueFlat,

        TongueTwistRight,
        TongueTwistLeft,

        #endregion

        #region Throat/Neck Expressions

        SoftPalateClose,
        ThroatSwallow,

        NeckFlexRight,
        NeckFlexLeft,

        #endregion

        Max
    }

    /// <summary>
    /// Represents the type of Legacy Shape data being sent by UnifiedExpressionData, in the form of enumerated SRanipal shapes.
    /// </summary>
    /// <remarks>
    /// This enum is not intended to be used directly by modules in the final iteration, and instead will be used as a compatibility layer for making the new Unified system backwards compatible with older VRCFT avatars.
    /// </remarks>
    internal enum SRanipal_LipShape_v2
    {
        JawRight = 0,
        JawLeft = 1,
        JawForward = 2,
        JawOpen = 3,
        MouthApeShape = 4,
        MouthUpperRight = 5,
        MouthUpperLeft = 6,
        MouthLowerRight = 7,
        MouthLowerLeft = 8,
        MouthUpperOverturn = 9,
        MouthLowerOverturn = 10,
        MouthPout = 11,
        MouthSmileRight = 12,
        MouthSmileLeft = 13,
        MouthSadRight = 14,
        MouthSadLeft = 15,
        CheekPuffRight = 16,
        CheekPuffLeft = 17,
        CheekSuck = 18,
        MouthUpperUpRight = 19,
        MouthUpperUpLeft = 20,
        MouthLowerDownRight = 21,
        MouthLowerDownLeft = 22,
        MouthUpperInside = 23,
        MouthLowerInside = 24,
        MouthLowerOverlay = 25,
        TongueLongStep1 = 26,
        TongueLongStep2 = 32,
        TongueDown = 30,
        TongueUp = 29,
        TongueRight = 28,
        TongueLeft = 27,
        TongueRoll = 31,
        TongueUpLeftMorph = 34,
        TongueUpRightMorph = 33,
        TongueDownLeftMorph = 36,
        TongueDownRightMorph = 35,
        Max = 37,
    }
}