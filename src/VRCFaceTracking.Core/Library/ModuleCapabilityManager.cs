using Microsoft.Extensions.Logging;

namespace VRCFaceTracking.Core.Library;

public enum TrackingCapability
{
    Eye,
    Expression,
    Head
}

public class ModuleCapabilityManager
{
    private readonly Dictionary<TrackingCapability, string?> _owners = new()
    {
        { TrackingCapability.Eye, null },
        { TrackingCapability.Expression, null },
        { TrackingCapability.Head, null }
    };

    private readonly ILogger<ModuleCapabilityManager> _logger;

    public event Action? OnCapabilityChanged;

    public ModuleCapabilityManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ModuleCapabilityManager>();
    }

    public string? GetOwner(TrackingCapability capability)
    {
        return _owners.GetValueOrDefault(capability);
    }

    public bool TryClaim(string moduleId, TrackingCapability capability)
    {
        if (_owners[capability] == null)
        {
            _owners[capability] = moduleId;
            _logger.LogInformation("Module " + moduleId + " claimed " + capability);
            OnCapabilityChanged?.Invoke();
            return true;
        }
        return false;
    }

    public void Assign(string moduleId, TrackingCapability capability)
    {
        var previous = _owners[capability];
        _owners[capability] = moduleId;
        _logger.LogInformation("Capability " + capability + " reassigned from " + (previous ?? "none") + " to " + moduleId);
        OnCapabilityChanged?.Invoke();
    }

    public void Release(string moduleId)
    {
        foreach (var cap in _owners.Keys.ToList())
        {
            if (_owners[cap] == moduleId)
            {
                _owners[cap] = null;
                _logger.LogInformation("Module " + moduleId + " released " + cap);
            }
        }
        OnCapabilityChanged?.Invoke();
    }

    public void ReleaseCapability(TrackingCapability capability)
    {
        var previous = _owners[capability];
        _owners[capability] = null;
        if (previous != null)
            _logger.LogInformation("Capability " + capability + " released from " + previous);
        OnCapabilityChanged?.Invoke();
    }

    public bool IsOwner(string moduleId, TrackingCapability capability)
    {
        return _owners[capability] == moduleId;
    }

    public Dictionary<TrackingCapability, string?> GetAllAssignments()
    {
        return new Dictionary<TrackingCapability, string?>(_owners);
    }
}
