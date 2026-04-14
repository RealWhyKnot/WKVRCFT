namespace VRCFaceTracking.V2;

/// <summary>
/// Attribute to declare module metadata. Applied to the module class.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ModuleMetadataAttribute : Attribute
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
}
