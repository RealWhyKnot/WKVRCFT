namespace VRCFaceTracking.Core.Params.Data.Mutation;

[AttributeUsage(AttributeTargets.Field)]
public class MutationPropertyAttribute : Attribute
{
    public string Name { get; }
    public bool Persist { get; }
    public float Min { get; }
    public float Max { get; }

    public MutationPropertyAttribute(string name, bool persist = false, float min = 0f, float max = 1f)
    {
        Name = name;
        Persist = persist;
        Min = min;
        Max = max;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class MutationButtonAttribute : Attribute
{
    public string Name { get; }

    public MutationButtonAttribute(string name)
    {
        Name = name;
    }
}
