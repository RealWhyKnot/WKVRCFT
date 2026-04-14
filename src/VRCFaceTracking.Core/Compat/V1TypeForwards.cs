// Type-forwarding shim for V1 module backward compatibility.
//
// V1 community modules (SteamLink, VirtualDesktop, etc.) were compiled against
// VRCFaceTracking.Core 5.1.1.0, where ExtTrackingModule, ModuleMetadata, and
// ModuleState all lived in VRCFaceTracking.Core.dll.
//
// These types were later extracted to VRCFaceTracking.SDK.dll.  When the V1 module
// host redirects a VRCFaceTracking.Core 5.1.1.0 request to the current Core
// (version-agnostic redirect in ModuleLoadContext), the CLR still looks up each
// referenced type by (AssemblyName, TypeName).  Without these forwarders it would
// throw TypeLoadException because the types are no longer in Core.
//
// [TypeForwardedTo(typeof(T))] inserts a type-forwarder record in the metadata of
// THIS assembly (Core) that tells the CLR: "T has moved — look in the assembly
// where T is currently defined (SDK)".  No IL is emitted; this is pure metadata.

using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(VRCFaceTracking.ExtTrackingModule))]
[assembly: TypeForwardedTo(typeof(VRCFaceTracking.ModuleMetadata))]
[assembly: TypeForwardedTo(typeof(VRCFaceTracking.Core.Library.ModuleState))]
