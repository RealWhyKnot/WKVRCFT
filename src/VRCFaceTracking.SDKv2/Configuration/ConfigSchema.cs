namespace VRCFaceTracking.V2.Configuration;

/// <summary>
/// Declarative configuration schema that modules publish.
/// The host renders this as a settings UI.
/// </summary>
public class ConfigSchema
{
    public List<ConfigField> Fields { get; set; } = new();
}

public class ConfigField
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public ConfigFieldType Type { get; set; }
    public object? DefaultValue { get; set; }
    public float? Min { get; set; }
    public float? Max { get; set; }
    public List<string>? Options { get; set; }
}

public enum ConfigFieldType
{
    Float,
    Int,
    Bool,
    String,
    Enum,
    FilePath
}
