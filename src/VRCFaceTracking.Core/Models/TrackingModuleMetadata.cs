using System.Text.Json.Serialization;

namespace VRCFaceTracking.Core.Models;

/// <summary>
/// Maps to the registry.vrcft.io/modules API response.
/// Field names use [JsonPropertyName] to bridge the API's naming to our internal names.
/// </summary>
public class TrackingModuleMetadata
{
    // API field: "ModuleId" (UUID string)
    [JsonPropertyName("ModuleId")]
    public string PackageId { get; set; } = "";

    // API field: "ModuleName"
    [JsonPropertyName("ModuleName")]
    public string DisplayName { get; set; } = "";

    // API field: "AuthorName"
    [JsonPropertyName("AuthorName")]
    public string Author { get; set; } = "";

    // API field: "ModuleDescription"
    [JsonPropertyName("ModuleDescription")]
    public string Description { get; set; } = "";

    [JsonPropertyName("Version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("DownloadUrl")]
    public string DownloadUrl { get; set; } = "";

    // API field: "FileHash" (optional MD5)
    [JsonPropertyName("FileHash")]
    public string Md5Hash { get; set; } = "";

    // API field: "DllFileName"
    [JsonPropertyName("DllFileName")]
    public string DllFileName { get; set; } = "";

    // API field: "ModulePageUrl"
    [JsonPropertyName("ModulePageUrl")]
    public string PageUrl { get; set; } = "";

    // API field: "UsageInstructions" — setup hint shown to users (e.g. "Install Driver X 2.0+").
    // Optional. Carried through from registry payload to the locally-written manifest.json.
    [JsonPropertyName("UsageInstructions")]
    public string UsageInstructions { get; set; } = "";

    // Not returned by API — kept for local manifest compat
    [JsonPropertyName("Tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("UsesEye")]
    public bool UsesEye { get; set; }

    [JsonPropertyName("UsesExpression")]
    public bool UsesExpression { get; set; }

    [JsonPropertyName("IconUrl")]
    public string IconUrl { get; set; } = "";
}
