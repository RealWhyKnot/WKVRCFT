using System.Text.Json;
using VRCFaceTracking.SDKv2.Expressions;
using Photino.NET;

namespace VRCFaceTracking.App;

public class TrackingDataBroadcaster : IDisposable
{
    private readonly PhotinoWindow _window;
    private readonly System.Threading.Timer _timer;
    private readonly float[] _lastShapes;
    private bool _windowReady;
    private const float ChangeThreshold = 0.001f;
    private const int BroadcastIntervalMs = 33; // ~30fps

    public TrackingDataBroadcaster(PhotinoWindow window)
    {
        _window = window;
        _lastShapes = new float[(int)UnifiedExpressions.Max + 1];
        _timer = new System.Threading.Timer(Broadcast, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        _windowReady = true;
        _timer.Change(0, BroadcastIntervalMs);
    }

    public void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void Broadcast(object? state)
    {
        if (!_windowReady) return;

        try
        {
            var data = UnifiedTracking.Data;

            // Change detection - skip if nothing significant changed
            bool changed = false;
            for (int i = 0; i < data.Shapes.Length && i < _lastShapes.Length; i++)
            {
                if (Math.Abs(data.Shapes[i].Weight - _lastShapes[i]) > ChangeThreshold)
                {
                    changed = true;
                    _lastShapes[i] = data.Shapes[i].Weight;
                }
            }

            // Always send if eye/head data might have changed (we don't track those separately)
            changed = true; // For now, always send - optimize later

            if (!changed) return;

            var shapes = new float[data.Shapes.Length];
            for (int i = 0; i < shapes.Length; i++)
                shapes[i] = MathF.Round(data.Shapes[i].Weight, 3);

            var trackingPayload = new
            {
                eye = new
                {
                    left = new
                    {
                        gazeX = MathF.Round(data.Eye.Left.Gaze.x, 3),
                        gazeY = MathF.Round(data.Eye.Left.Gaze.y, 3),
                        openness = MathF.Round(data.Eye.Left.Openness, 3),
                        pupil = MathF.Round(data.Eye.Left.PupilDiameter_MM, 3)
                    },
                    right = new
                    {
                        gazeX = MathF.Round(data.Eye.Right.Gaze.x, 3),
                        gazeY = MathF.Round(data.Eye.Right.Gaze.y, 3),
                        openness = MathF.Round(data.Eye.Right.Openness, 3),
                        pupil = MathF.Round(data.Eye.Right.PupilDiameter_MM, 3)
                    }
                },
                shapes,
                head = new
                {
                    yaw = MathF.Round(data.Head.HeadYaw, 3),
                    pitch = MathF.Round(data.Head.HeadPitch, 3),
                    roll = MathF.Round(data.Head.HeadRoll, 3),
                    posX = MathF.Round(data.Head.HeadPosX, 3),
                    posY = MathF.Round(data.Head.HeadPosY, 3),
                    posZ = MathF.Round(data.Head.HeadPosZ, 3)
                }
            };

            var json = JsonSerializer.Serialize(new { type = "TRACKING_DATA", data = trackingPayload });

            _window.Invoke(() =>
            {
                try { _window.SendWebMessage(json); }
                catch { /* window may be closing */ }
            });
        }
        catch
        {
            // Don't let broadcast errors crash the app
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
