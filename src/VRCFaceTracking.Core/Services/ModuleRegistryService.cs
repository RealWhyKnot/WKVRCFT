using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Models;

namespace VRCFaceTracking.Core.Services;

public class ModuleRegistryService
{
    public const string DefaultRegistryUrl = "https://registry.vrcft.io/modules";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly ILogger<ModuleRegistryService> _logger;
    private readonly SettingsService? _settings;
    private static readonly HttpClient Http = new();

    private List<TrackingModuleMetadata>? _cachedRegistry;
    private DateTime _cacheExpiry = DateTime.MinValue;

    private List<InstallableTrackingModule> _modules = new();

    private string RegistryUrl =>
        string.IsNullOrWhiteSpace(_settings?.AppConfig.RegistryUrl)
            ? DefaultRegistryUrl
            : _settings!.AppConfig.RegistryUrl;

    public ModuleRegistryService(ILoggerFactory loggerFactory, SettingsService? settings = null)
    {
        _logger = loggerFactory.CreateLogger<ModuleRegistryService>();
        _settings = settings;
    }

    /// <summary>
    /// Returns the merged list: all registry modules annotated with local install state.
    /// </summary>
    public async Task<List<InstallableTrackingModule>> GetModulesAsync(
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var registry = await FetchRegistryAsync(forceRefresh, ct);
        var localInstalls = ScanLocalInstalls();

        _modules = registry.Select(meta =>
        {
            var module = new InstallableTrackingModule { Metadata = meta };
            if (localInstalls.TryGetValue(meta.PackageId, out var localInfo))
            {
                module.InstallPath = localInfo.path;
                module.InstalledVersion = localInfo.version;
                module.InstallState = string.Compare(localInfo.version, meta.Version,
                    StringComparison.OrdinalIgnoreCase) < 0
                    ? InstallState.UpdateAvailable
                    : InstallState.Installed;
            }
            return module;
        }).ToList();

        return _modules;
    }

    /// <summary>
    /// Returns only the locally-installed modules (for the Modules page).
    /// Includes modules that aren't in the registry (community/local).
    /// </summary>
    public List<InstallableTrackingModule> GetLocalModules()
    {
        var localInstalls = ScanLocalInstalls();
        var result = new List<InstallableTrackingModule>();

        foreach (var (packageId, info) in localInstalls)
        {
            // Try to find registry metadata
            var existing = _modules.FirstOrDefault(m => m.Metadata.PackageId == packageId);
            if (existing != null)
            {
                result.Add(existing);
            }
            else
            {
                // Locally-installed module not in registry
                result.Add(new InstallableTrackingModule
                {
                    Metadata = new TrackingModuleMetadata
                    {
                        PackageId = packageId,
                        DisplayName = Path.GetFileNameWithoutExtension(info.path),
                        Version = info.version ?? "unknown"
                    },
                    InstallState = InstallState.Installed,
                    InstallPath = info.path,
                    InstalledVersion = info.version
                });
            }
        }

        return result;
    }

    private async Task<List<TrackingModuleMetadata>> FetchRegistryAsync(
        bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh && _cachedRegistry != null && DateTime.UtcNow < _cacheExpiry)
            return _cachedRegistry;

        try
        {
            _logger.LogInformation("Fetching module registry...");
            using var response = await Http.GetAsync(RegistryUrl, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug($"Registry response: {json[..Math.Min(200, json.Length)]}...");
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var modules = JsonSerializer.Deserialize<List<TrackingModuleMetadata>>(json, opts)
                          ?? new List<TrackingModuleMetadata>();

            _cachedRegistry = modules;
            _cacheExpiry = DateTime.UtcNow + CacheTtl;
            _logger.LogInformation($"Registry fetched: {modules.Count} module(s)");
            return modules;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to fetch registry: {ex.Message}");
            return _cachedRegistry ?? new List<TrackingModuleMetadata>();
        }
    }

    // Returns a map of packageId → (primaryDllPath, version)
    private static Dictionary<string, (string path, string? version)> ScanLocalInstalls()
    {
        var result = new Dictionary<string, (string, string?)>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(UnifiedLibManager.ModulesDir))
            return result;

        foreach (var moduleDir in Directory.GetDirectories(UnifiedLibManager.ModulesDir))
        {
            var packageId = Path.GetFileName(moduleDir);
            var dlls = Directory.GetFiles(moduleDir, "*.dll", SearchOption.AllDirectories);
            if (dlls.Length == 0) continue;

            // Read installed version from manifest if present
            string? version = null;
            var manifestPath = Path.Combine(moduleDir, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    if (doc.RootElement.TryGetProperty("version", out var v))
                        version = v.GetString();
                }
                catch { /* ignore bad manifest */ }
            }

            result[packageId] = (dlls[0], version);
        }

        return result;
    }

    /// <summary>
    /// Writes a manifest.json after successful install. Captures the registry metadata so the
    /// host can show the user the page URL, usage instructions, and version across restarts
    /// without having to round-trip the registry again.
    /// </summary>
    public static void WriteManifest(string packageId, TrackingModuleMetadata metadata)
    {
        var manifestPath = Path.Combine(UnifiedLibManager.ModulesDir, packageId, "manifest.json");
        try
        {
            var payload = new
            {
                sdk = 2,
                packageId,
                name = metadata.DisplayName,
                version = metadata.Version,
                description = metadata.Description,
                author = metadata.Author,
                downloadUrl = metadata.DownloadUrl,
                pageUrl = metadata.PageUrl,
                usageInstructions = metadata.UsageInstructions,
                dllFileName = metadata.DllFileName,
                md5Hash = metadata.Md5Hash,
                tags = metadata.Tags,
                usesEye = metadata.UsesEye,
                usesExpression = metadata.UsesExpression,
                iconUrl = metadata.IconUrl
            };
            var opts = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(payload, opts));
        }
        catch { /* non-fatal */ }
    }

    /// <summary>
    /// Legacy thin overload — preserved for callers that only know the version.
    /// New callers should pass the full metadata so the manifest survives restart with
    /// page URL, usage instructions, etc. intact.
    /// </summary>
    public static void WriteManifest(string packageId, string version)
        => WriteManifest(packageId, new TrackingModuleMetadata { PackageId = packageId, Version = version });
}
