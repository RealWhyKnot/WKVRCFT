namespace VRCFaceTracking.EmulatedTracking.HeadMovement;

/// <summary>
/// Estimates subtle head movement from audio prosody:
///  - RMS energy → head pitch (nodding on speech beats)
///  - Spectral centroid variance → head yaw (turning during emphasis)
/// All outputs are low-pass filtered to avoid jitter.
/// </summary>
public class ProsodyHeadEstimator
{
    private const float SampleRate = 16000f;
    private const float Alpha = 0.08f;       // Low-pass smoothing factor
    private const float HeadIntensity = 0.15f; // Max head rotation magnitude (0–1 scale)

    private float _smoothedRms = 0f;
    private float _smoothedCentroid = 0f;
    private float _baselineRms = 0f;
    private float _headPitch = 0f;
    private float _headYaw = 0f;
    private int _framesProcessed = 0;

    public float HeadPitch => _headPitch;
    public float HeadYaw => _headYaw;

    public void Process(byte[] pcm16le)
    {
        if (pcm16le.Length < 2) return;

        int sampleCount = pcm16le.Length / 2;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BitConverter.ToInt16(pcm16le, i * 2) / 32768f;
        }

        // Compute RMS energy
        float rms = 0f;
        for (int i = 0; i < sampleCount; i++) rms += samples[i] * samples[i];
        rms = MathF.Sqrt(rms / sampleCount);

        // Compute spectral centroid (rough frequency content measure)
        float centroid = ComputeSpectralCentroid(samples);

        // Baseline calibration during first 2 seconds
        if (_framesProcessed < 20)
        {
            _baselineRms = (_baselineRms * _framesProcessed + rms) / (_framesProcessed + 1);
            _framesProcessed++;
        }

        // Low-pass filter
        _smoothedRms = _smoothedRms + Alpha * (rms - _smoothedRms);
        _smoothedCentroid = _smoothedCentroid + Alpha * (centroid - _smoothedCentroid);

        // Map to head movement
        float relativeRms = Math.Clamp(_smoothedRms - _baselineRms * 0.5f, 0f, 0.5f) * 2f;

        // Pitch: nod slightly on speech energy peaks
        _headPitch = -relativeRms * HeadIntensity;

        // Yaw: subtle drift based on frequency content variation
        float yawTarget = (_smoothedCentroid - 0.5f) * HeadIntensity * 0.5f;
        _headYaw = _headYaw + 0.02f * (yawTarget - _headYaw);
    }

    public void Reset()
    {
        _smoothedRms = 0f;
        _smoothedCentroid = 0f;
        _baselineRms = 0f;
        _headPitch = 0f;
        _headYaw = 0f;
        _framesProcessed = 0;
    }

    private static float ComputeSpectralCentroid(float[] samples)
    {
        // Simplified: use zero-crossing rate as a proxy for frequency content
        int zeroCrossings = 0;
        for (int i = 1; i < samples.Length; i++)
        {
            if (samples[i] * samples[i - 1] < 0)
                zeroCrossings++;
        }
        // Normalize to [0, 1]
        float zcr = (float)zeroCrossings / samples.Length;
        return Math.Clamp(zcr * 5f, 0f, 1f);
    }
}
