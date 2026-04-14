using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace VRCFaceTracking.Core.Params.Data.Mutation;

public enum MutationPriority
{
    Preprocessor,
    None,
    Postprocessor
}

public abstract class TrackingMutation
{
    public abstract string Name { get; }

    [JsonIgnore]
    public abstract string Description { get; }

    public abstract MutationPriority Step { get; }

    public virtual bool IsSaved { get; } = false;

    public virtual bool IsActive { get; set; }

    [JsonIgnore]
    public ILogger? Logger { get; set; }

    public virtual void Initialize(UnifiedTrackingData data) { }

    public abstract void MutateData(ref UnifiedTrackingData data);

    public static TrackingMutation[] GetImplementingMutations(bool ordered = true)
    {
        var types = Assembly.GetExecutingAssembly()
                            .GetTypes()
                            .Where(type => type.IsSubclassOf(typeof(TrackingMutation)) && !type.IsAbstract);

        var mutations = new List<TrackingMutation>();
        foreach (var t in types)
        {
            try
            {
                var mutation = (TrackingMutation?)Activator.CreateInstance(t);
                if (mutation != null) mutations.Add(mutation);
            }
            catch { /* skip types that can't be instantiated */ }
        }

        if (ordered)
            mutations.Sort((a, b) => a.Step.CompareTo(b.Step));

        return mutations.ToArray();
    }
}
