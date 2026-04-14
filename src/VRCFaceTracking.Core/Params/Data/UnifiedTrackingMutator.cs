using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Params.Data.Mutation;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Core.Params.Data;

public class UnifiedTrackingMutator
{
    public bool Enabled { get; set; }

    private readonly ILogger _logger;
    private readonly object _mutationsLock = new();
    private UnifiedTrackingData _inputBuffer;
    public List<TrackingMutation> Mutations { get; } = new();

    public UnifiedTrackingMutator(ILoggerFactory loggerFactory)
    {
        UnifiedTracking.Mutator = this;
        _logger = loggerFactory.CreateLogger<UnifiedTrackingMutator>();
        Enabled = false;
        _inputBuffer = new UnifiedTrackingData();
    }

    public UnifiedTrackingData MutateData(UnifiedTrackingData input)
    {
        if (!Enabled)
            return input;

        _inputBuffer.CopyPropertiesOf(input);

        lock (_mutationsLock)
        {
            foreach (var mutator in Mutations.Where(m => m.IsActive))
            {
                try
                {
                    mutator.MutateData(ref _inputBuffer);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Mutation " + mutator.Name + " failed: " + ex.Message);
                }
            }
        }

        return _inputBuffer;
    }

    public void Initialize()
    {
        _logger.LogDebug("Initializing mutations...");
        lock (_mutationsLock)
        {
            foreach (var mutation in Mutations)
            {
                _logger.LogInformation("Initializing " + mutation.Name);
                mutation.Initialize(UnifiedTracking.Data);
            }
        }
        _logger.LogDebug("Mutations initialized.");
    }

    public void Load()
    {
        _logger.LogDebug("Loading mutations...");
        var mutations = TrackingMutation.GetImplementingMutations(true);

        lock (_mutationsLock)
        {
            foreach (var mutation in mutations)
            {
                mutation.Logger = _logger;
                Mutations.Add(mutation);
                _logger.LogInformation("Loaded mutation: " + mutation.Name);
            }
        }

        Initialize();
        _logger.LogDebug("Mutations loaded.");
    }

    public async Task Save()
    {
        // Save mutation state to settings
        _logger.LogDebug("Saving mutation configuration...");
        // TODO: Serialize mutation state to SettingsService
        await Task.CompletedTask;
    }
}
