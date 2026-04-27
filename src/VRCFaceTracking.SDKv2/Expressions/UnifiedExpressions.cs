namespace VRCFaceTracking.SDKv2.Expressions
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
}
