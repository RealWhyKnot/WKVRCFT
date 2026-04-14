using System.Reflection;
using System.Runtime.Loader;
using VRCFaceTracking.V2;

namespace VRCFaceTracking.ModuleHostV2;

/// <summary>
/// Loads a V2 module DLL in a collectible AssemblyLoadContext and
/// finds the ITrackingModuleV2 implementor.
/// </summary>
public class ModuleAssemblyV2 : IDisposable
{
    private AssemblyLoadContext? _loadContext;
    private Assembly? _assembly;

    public ITrackingModuleV2? Module { get; private set; }
    public ModuleMetadataAttribute? Metadata { get; private set; }

    /// <summary>
    /// Collectible ALC that probes the module's own directory for dependencies.
    /// Without this, assemblies like Google.Protobuf that live next to the module
    /// DLL would not be found (the host exe directory is a different location).
    /// </summary>
    private sealed class ModuleLoadContext : AssemblyLoadContext
    {
        private readonly string _moduleDir;
        private readonly AssemblyDependencyResolver _resolver;

        public ModuleLoadContext(string modulePath)
            : base(Path.GetFileNameWithoutExtension(modulePath), isCollectible: true)
        {
            _moduleDir = Path.GetDirectoryName(modulePath)!;
            _resolver  = new AssemblyDependencyResolver(modulePath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // These assemblies are shared interface contracts between the host and module.
            // They MUST come from the host's default context so type identity is preserved:
            //  - VRCFaceTracking.*  → ITrackingModuleV2, IModuleContext, etc.
            //  - Microsoft.Extensions.* → ILogger (used in IModuleContext.Logger)
            // Loading separate copies in this ALC causes MissingMethodException when the
            // host passes objects (e.g. V2PipeLogger) to module code that expects the same type.
            var name = assemblyName.Name;
            if (name != null &&
                (name.StartsWith("VRCFaceTracking.") || name.StartsWith("Microsoft.Extensions.")))
                return null;

            // deps.json knows the exact paths for every NuGet dependency
            string? resolved = _resolver.ResolveAssemblyToPath(assemblyName);
            if (resolved != null)
                return LoadFromAssemblyPath(resolved);

            // Fallback: probe sibling DLLs in the module's directory
            string candidate = Path.Combine(_moduleDir, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
                return LoadFromAssemblyPath(candidate);

            return null; // let the default context try
        }
    }

    public bool TryLoad(string dllPath)
    {
        try
        {
            _loadContext = new ModuleLoadContext(dllPath);

            _assembly = _loadContext.LoadFromAssemblyPath(dllPath);

            foreach (var type in _assembly.GetExportedTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(ITrackingModuleV2).IsAssignableFrom(type)) continue;

                Module = (ITrackingModuleV2?)Activator.CreateInstance(type);
                Metadata = type.GetCustomAttribute<ModuleMetadataAttribute>();
                return Module != null;
            }

            Console.Error.WriteLine($"No ITrackingModuleV2 implementor found in {Path.GetFileName(dllPath)}");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load {Path.GetFileName(dllPath)}: {ex}");
            return false;
        }
    }

    public void Dispose()
    {
        _loadContext?.Unload();
    }
}
