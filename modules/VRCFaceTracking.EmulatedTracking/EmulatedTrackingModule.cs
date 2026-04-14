using Microsoft.Extensions.Logging;
using VRCFaceTracking.EmulatedTracking.Audio;
using VRCFaceTracking.EmulatedTracking.HeadMovement;
using VRCFaceTracking.EmulatedTracking.Inference;
using VRCFaceTracking.EmulatedTracking.Mapping;
using VRCFaceTracking.V2;

namespace VRCFaceTracking.EmulatedTracking;

/// <summary>
/// Emulated Face Tracking — drives facial expressions from microphone audio using
/// on-device spectral signal processing.  No external service, Docker container,
/// or GPU required; everything runs in-process on the CPU.
///
/// Architecture:
///   Microphone → AudioCaptureService (16 kHz PCM)
///             → SignalProcessingBackend (Goertzel spectral analysis → 52 ARKit blendshapes)
///             → ARKitMapper (ARKit → UnifiedExpressions)
///             → ProsodyHeadEstimator (RMS/ZCR → head pitch/yaw)
/// </summary>
[ModuleMetadata(
    Name = "Emulated Face Tracking",
    Description = "Drives facial expressions from microphone audio using on-device spectral analysis",
    Author = "VRCFaceTracking",
    Version = "1.0.0")]
public class EmulatedTrackingModule : ITrackingModuleV2
{
    public ModuleCapabilities Capabilities => ModuleCapabilities.Expression | ModuleCapabilities.Head;

    private IModuleContext? _context;
    private AudioCaptureService? _capture;
    private SignalProcessingBackend? _backend;
    private ProsodyHeadEstimator? _headEstimator;

    private float[]? _latestBlendshapes;
    private byte[]?  _latestAudioChunk;
    private readonly object _dataLock = new();

    private float _expressionIntensity = 1f;
    private float _headIntensity = 1f;
    private bool  _enableHead = true;

    public Task<bool> InitializeAsync(IModuleContext context)
    {
        _context = context;

        int   micDevice = context.Settings.GetSetting(EmulatedTrackingConfig.KeyMicDevice, -1);
        float micGain   = context.Settings.GetSetting(EmulatedTrackingConfig.KeyMicGain, 1.0f);
        _expressionIntensity = context.Settings.GetSetting(EmulatedTrackingConfig.KeyExpressionIntensity, 1.0f);
        _headIntensity       = context.Settings.GetSetting(EmulatedTrackingConfig.KeyHeadIntensity, 1.0f);
        _enableHead          = context.Settings.GetSetting(EmulatedTrackingConfig.KeyEnableHead, true);

        context.RegisterConfigSchema(EmulatedTrackingConfig.BuildSchema());

        _backend = new SignalProcessingBackend();

        _capture = new AudioCaptureService { GainMultiplier = micGain };
        _capture.OnAudioChunk += chunk =>
        {
            lock (_dataLock)
            {
                _latestBlendshapes = _backend.Process(chunk);
                _latestAudioChunk  = chunk;
            }
        };

        if (!_capture.Start(micDevice))
        {
            context.Logger.LogError("Emulated Face Tracking: failed to start microphone capture");
            return Task.FromResult(false);
        }

        _headEstimator = new ProsodyHeadEstimator();
        context.Logger.LogInformation("Emulated Face Tracking initialized (signal processing mode)");
        return Task.FromResult(true);
    }

    public Task UpdateAsync(CancellationToken ct)
    {
        if (_context == null) return Task.CompletedTask;

        float[]? blendshapes;
        byte[]?  audioChunk;

        lock (_dataLock)
        {
            blendshapes       = _latestBlendshapes;
            audioChunk        = _latestAudioChunk;
            _latestAudioChunk = null;
        }

        if (blendshapes != null && blendshapes.Length == 52)
            ARKitMapper.Apply(blendshapes, _context.TrackingData, _expressionIntensity);

        if (_enableHead && _headEstimator != null && audioChunk != null)
        {
            _headEstimator.Process(audioChunk);
            _context.TrackingData.SetHeadRotation(
                _headEstimator.HeadYaw   * _headIntensity,
                _headEstimator.HeadPitch * _headIntensity,
                0f);
        }

        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _capture?.Stop();
        _capture?.Dispose();
        _backend?.Reset();
        _headEstimator?.Reset();
        _context?.Logger.LogInformation("Emulated Face Tracking shut down");
        return Task.CompletedTask;
    }
}
