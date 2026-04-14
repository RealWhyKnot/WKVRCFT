using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace VRCFaceTracking.ModuleHost;

public class ModuleAssembly
{
    private readonly string _modulePath;
    private readonly ILogger _logger;
    private AssemblyLoadContext? _loadContext;

    public Assembly? Assembly { get; private set; }
    public ExtTrackingModule? TrackingModule { get; private set; }

    /// <summary>
    /// Collectible ALC that probes the module's own directory for dependencies.
    /// Third-party modules ship their NuGet dependencies next to the DLL;
    /// without this resolver the host exe's directory would be probed instead.
    /// </summary>
    private sealed class ModuleLoadContext : AssemblyLoadContext
    {
        private readonly string _moduleDir;
        private readonly AssemblyDependencyResolver _resolver;

        public ModuleLoadContext(string modulePath)
            : base(Path.GetFileNameWithoutExtension(modulePath), isCollectible: true)
        {
            _moduleDir = Path.GetDirectoryName(Path.GetFullPath(modulePath))!;
            _resolver  = new AssemblyDependencyResolver(Path.GetFullPath(modulePath));
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // All VRCFaceTracking.* assemblies MUST resolve to the host's already-loaded copy.
            //
            // Two reasons:
            //  1. Type identity — ExtTrackingModule (VRCFaceTracking.SDK) must be the same type
            //     object in both host and module, or IsAssignableFrom / casting fails.
            //  2. Shared state — UnifiedTracking.Data (VRCFaceTracking.Core) is a static field.
            //     Both the host's Program.cs and the V1 module write/read that same buffer.
            //     If the module loaded a second copy of Core, it would get a different static
            //     instance and tracking data would never reach the host.
            //
            // V1 modules were compiled against VRCFaceTracking.Core 5.1.1.0.  The Default ALC
            // has a newer version.  Returning null here lets the Default ALC's strict version
            // check reject it with FileNotFoundException.  Instead we do an explicit
            // version-agnostic lookup — the runtime binding redirect equivalent for plugins.
            var name = assemblyName.Name;
            if (name != null && name.StartsWith("VRCFaceTracking."))
            {
                var existing = AssemblyLoadContext.Default.Assemblies
                    .FirstOrDefault(a => a.GetName().Name == name);
                if (existing != null)
                    return existing;

                // Not yet in the default context — let the default probe handle it normally.
                return null;
            }

            string? resolved = _resolver.ResolveAssemblyToPath(assemblyName);
            if (resolved != null)
                return LoadFromAssemblyPath(resolved);

            string candidate = Path.Combine(_moduleDir, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
                return LoadFromAssemblyPath(candidate);

            return null;
        }
    }

    public ModuleAssembly(string modulePath, ILogger logger)
    {
        _modulePath = modulePath;
        _logger = logger;
    }

    public bool TryLoadAssembly()
    {
        if (!File.Exists(_modulePath))
        {
            _logger.LogError("Module file not found: " + _modulePath);
            return false;
        }

        if (!_modulePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Module file is not a DLL: " + _modulePath);
            return false;
        }

        try
        {
            _loadContext = new ModuleLoadContext(_modulePath);
            Assembly = _loadContext.LoadFromAssemblyPath(Path.GetFullPath(_modulePath));

            // Find the ExtTrackingModule subclass
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.BaseType == typeof(ExtTrackingModule))
                {
                    _logger.LogInformation("Found tracking module type: " + type.FullName);
                    TrackingModule = Activator.CreateInstance(type) as ExtTrackingModule;

                    if (TrackingModule != null)
                    {
                        TrackingModule.Logger = _logger;
                        return true;
                    }
                }
            }

            _logger.LogError("No ExtTrackingModule subclass found in assembly");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load module assembly: " + ex);
            return false;
        }
    }
}
