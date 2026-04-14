using VRCFaceTracking.Core.Params.Expressions;
using VRCFaceTracking.Core.Sandboxing.V2;
using VRCFaceTracking.V2;

namespace VRCFaceTracking.ModuleHostV2;

/// <summary>
/// Implements ITrackingDataWriter. Accumulates writes to a local buffer;
/// FlushAsync() serializes the buffer and sends it to the host via the pipe.
/// </summary>
public class V2TrackingDataWriter : ITrackingDataWriter
{
    private readonly V2PipeClient _pipe;
    private readonly int _shapeCount = (int)UnifiedExpressions.Max + 1;

    private float[]? _shapes;
    private V2EyeDataPayload? _eyeLeft;
    private V2EyeDataPayload? _eyeRight;
    private V2HeadRotPayload? _headRot;
    private V2HeadPosPayload? _headPos;

    public V2TrackingDataWriter(V2PipeClient pipe)
    {
        _pipe = pipe;
    }

    public void SetExpression(int expressionIndex, float weight)
    {
        if (expressionIndex < 0 || expressionIndex >= _shapeCount) return;
        _shapes ??= new float[_shapeCount];
        _shapes[expressionIndex] = weight;
    }

    public void SetLeftEye(float gazeX, float gazeY, float openness, float pupilDiameterMM)
    {
        _eyeLeft = new V2EyeDataPayload(gazeX, gazeY, openness, pupilDiameterMM);
    }

    public void SetRightEye(float gazeX, float gazeY, float openness, float pupilDiameterMM)
    {
        _eyeRight = new V2EyeDataPayload(gazeX, gazeY, openness, pupilDiameterMM);
    }

    public void SetHeadRotation(float yaw, float pitch, float roll)
    {
        _headRot = new V2HeadRotPayload(yaw, pitch, roll);
    }

    public void SetHeadPosition(float x, float y, float z)
    {
        _headPos = new V2HeadPosPayload(x, y, z);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        if (_shapes == null && _eyeLeft == null && _eyeRight == null
            && _headRot == null && _headPos == null)
            return; // Nothing to send

        var payload = new V2TrackingDataPayload(_eyeLeft, _eyeRight, _headRot, _headPos, _shapes);
        await _pipe.SendTrackingDataAsync(payload, ct);

        // Reset buffers — only fields that were set will be non-null in next cycle
        _shapes = null;
        _eyeLeft = null;
        _eyeRight = null;
        _headRot = null;
        _headPos = null;
    }
}
