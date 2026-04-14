namespace VRCFaceTracking.V2;

/// <summary>
/// Controlled write access to tracking data.
/// Modules use this to set expression weights, eye data, and head pose.
/// </summary>
public interface ITrackingDataWriter
{
    /// <summary>
    /// Set an expression shape weight by index.
    /// </summary>
    void SetExpression(int expressionIndex, float weight);

    /// <summary>
    /// Set left eye data.
    /// </summary>
    void SetLeftEye(float gazeX, float gazeY, float openness, float pupilDiameterMM);

    /// <summary>
    /// Set right eye data.
    /// </summary>
    void SetRightEye(float gazeX, float gazeY, float openness, float pupilDiameterMM);

    /// <summary>
    /// Set head rotation (normalized -1 to 1, representing -90 to 90 degrees).
    /// </summary>
    void SetHeadRotation(float yaw, float pitch, float roll);

    /// <summary>
    /// Set head position (normalized -1 to 1, representing ~0.5m movement).
    /// </summary>
    void SetHeadPosition(float x, float y, float z);
}
