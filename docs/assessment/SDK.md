# SDK assessment — module API surface and UI capability

## Two SDKs in parallel

| | v1 (legacy / compat) | v2 (current) |
|---|---|---|
| Entry point | [src/VRCFaceTracking.SDK/ExtTrackingModule.cs](src/VRCFaceTracking.SDK/ExtTrackingModule.cs) — abstract class | [src/VRCFaceTracking.SDKv2/ITrackingModuleV2.cs](src/VRCFaceTracking.SDKv2/ITrackingModuleV2.cs) — interface |
| Lifecycle | sync `Initialize()` / `Update()` / `Teardown()` | async `InitializeAsync(IModuleContext)` / `UpdateAsync(ct)` / `ShutdownAsync()` |
| Data write | direct mutation of `UnifiedTracking.Data` static | typed `IModuleContext.TrackingData` writer methods |
| Capabilities | `(SupportsEye, SupportsExpression)` tuple | `[Flags] ModuleCapabilities { Eye, Expression, Head }` |
| Host IPC | UDP loopback, custom packet framing ([src/VRCFaceTracking.Core/Sandboxing/IPC/IpcPacket.cs](src/VRCFaceTracking.Core/Sandboxing/IPC/IpcPacket.cs)) | Named Pipe, JSON-framed messages ([src/VRCFaceTracking.Core/Sandboxing/V2/V2Message.cs](src/VRCFaceTracking.Core/Sandboxing/V2/V2Message.cs)) |
| Module-provided UI | **None** | **Yes — declarative** (see below) |

Both SDKs target net10.0. Both modules are loaded into per-module sub-processes (`ModuleHost.exe` / `ModuleHostV2.exe`) by the App. Both use a collectible `AssemblyLoadContext` so modules can be unloaded.

## v2 contract verbatim

```csharp
// src/VRCFaceTracking.SDKv2/ITrackingModuleV2.cs
public interface ITrackingModuleV2 {
    ModuleCapabilities Capabilities { get; }
    Task<bool> InitializeAsync(IModuleContext context);
    Task UpdateAsync(CancellationToken ct);     // ~100Hz
    Task ShutdownAsync();
}

// src/VRCFaceTracking.SDKv2/IModuleContext.cs
public interface IModuleContext {
    ILogger Logger { get; }
    IModuleSettings Settings { get; }                  // GetSetting<T>(key, default), SaveSetting
    ITrackingDataWriter TrackingData { get; }          // SetExpression / SetLeft|RightEye / SetHead*
    void PublishEvent(string eventType, object? payload = null);
    void RegisterConfigSchema(ConfigSchema schema);    // ← UI hook
}
```

A module is also expected to carry `[ModuleMetadata(Name=..., Description=..., Author=..., Version=...)]` (see [modules/VRCFaceTracking.AdvancedEmulation/AdvancedEmulationModule.cs:28-33](modules/VRCFaceTracking.AdvancedEmulation/AdvancedEmulationModule.cs)).

## Module discovery and loading

- Install location: `%LOCALAPPDATA%\VRCFaceTracking\modules\<package-id>\` ([UnifiedLibManager.cs:35](src/VRCFaceTracking.Core/Library/UnifiedLibManager.cs)).
- Each subdirectory contains a `manifest.json` and one or more DLLs. The manifest declares `sdk: 1` or `sdk: 2`; the host picks the right host process accordingly ([UnifiedLibManager.cs:97-162,212-251](src/VRCFaceTracking.Core/Library/UnifiedLibManager.cs)).
- Loader uses reflection: v1 finds first `class : ExtTrackingModule` ([ModuleAssembly.cs:99-104](src/VRCFaceTracking.ModuleHost/ModuleAssembly.cs)); v2 finds first `class : ITrackingModuleV2` ([ModuleAssemblyV2.cs:71-78](src/VRCFaceTracking.ModuleHostV2/ModuleAssemblyV2.cs)).
- Shared assemblies (`VRCFaceTracking.*`, `Microsoft.Extensions.*`) are resolved from the host's default ALC so type identity holds across the boundary.

## **UI capability — yes, v2 supports module-provided UI** ✅

Mechanism is **declarative configuration, not direct UI rendering**. The module ships a `ConfigSchema` (POCO list of `ConfigField` with `Key, Label, Type ∈ {Float, Int, Bool, String, Enum, FilePath}, DefaultValue, Min, Max, Options`). The host renders it into a Vue panel.

End-to-end flow:

1. **Module declares** during `InitializeAsync`:
   ```csharp
   // modules/VRCFaceTracking.AdvancedEmulation/AdvancedEmulationModule.cs:146
   context.RegisterConfigSchema(AdvancedEmulationConfig.BuildSchema());
   ```
   Schema definition: [modules/VRCFaceTracking.AdvancedEmulation/AdvancedEmulationConfig.cs:49-158](modules/VRCFaceTracking.AdvancedEmulation/AdvancedEmulationConfig.cs) — 24 fields covering toggles, sensitivity sliders, mic device selection, etc.

2. **Host context serializes & sends** as `V2MessageType.ConfigSchema` (= 13) over the named pipe — [src/VRCFaceTracking.ModuleHostV2/V2ModuleContext.cs:41-45](src/VRCFaceTracking.ModuleHostV2/V2ModuleContext.cs).

3. **App receives, stores per-module** at [src/VRCFaceTracking.Core/Library/UnifiedLibManager.cs:471-476](src/VRCFaceTracking.Core/Library/UnifiedLibManager.cs) — `module.ConfigSchemaJson = msg.Payload`. Triggers `OnModuleListChanged` so the UI re-fetches.

4. **Vue store + panel** at [ui/src/views/ModulesView.vue:143-151,252-260](ui/src/views/ModulesView.vue) → `<ModuleConfigPanel :config :save />` ([ui/src/components/ModuleConfigPanel.vue](ui/src/components/ModuleConfigPanel.vue)). Save → `store.saveModuleConfig(moduleId, values)` → IPC `SaveSettings` (V2MessageType = 9) → module's `IModuleSettings`.

This means **today**: a module can ship form-driven config (sliders, dropdowns, toggles, file pickers) without any UI framework binding. It cannot ship arbitrary HTML/Avalonia/XAML — but it gets the 90% case for free.

## What v2 does NOT support

- **Live runtime config updates inside a module's `UpdateAsync`.** A module reads settings once during `InitializeAsync` (see [AdvancedEmulationModule.cs:120-144](modules/VRCFaceTracking.AdvancedEmulation/AdvancedEmulationModule.cs)). The Vue UI saying "Disabled — toggle to enable on next startup cycle" ([ModulesView.vue:286](ui/src/views/ModulesView.vue)) reflects this. To fix: add an `IModuleContext.OnSettingsChanged` event or include the new values in the `SaveSettings` ack so the module can rebind.
- **Custom UI components.** No `IModuleUiProvider`-style return-a-UserControl mechanism. Modules cannot draw their own waveform displays, calibration assistants, etc. Adding this would require breaking the process boundary — either pipe an IPC message that includes pre-rendered HTML/SVG payloads, or have the host load a sibling JS bundle from the module folder. Unlikely worth it; declarative config covers the 90%.
- **Host events to module.** `PublishEvent` is module → host; the inverse channel exists conceptually (`V2MessageType.Event = 12` is reserved per [V2Message.cs:36](src/VRCFaceTracking.Core/Sandboxing/V2/V2Message.cs)) but isn't wired into a module-side API. v1's `ModuleMetadata.OnActiveChange` is gone with no replacement.
- **A public Module ID / GUID.** v1 manifests carried `ModuleId` (GUID) for stable identity ([reference/VirtualDesktop.VRCFaceTracking/module.json](reference/VirtualDesktop.VRCFaceTracking/module.json)). v2 manifests use the directory name as `PackageId`. Probably fine, but worth ratifying.

## Recommended SDK additions (small, scoped)

If you only do one thing: **promote `UnifiedExpressions` (and the `UnifiedExpressionsLength` constant) from `VRCFaceTracking.Core.Params.Expressions` into `VRCFaceTracking.SDKv2`.** Today every module needs it and every module has to project-reference Core to get it. This is the single biggest blocker for third-party v2 modules. (Details in [docs/assessment/EMULATED-MODULE-PLAN.md](docs/assessment/EMULATED-MODULE-PLAN.md).)

Two more, in priority order:

1. **Live-config plumbing.** Add to `IModuleContext`:
   ```csharp
   event Action<string, object?>? OnSettingChanged;     // (key, newValue)
   ```
   Wire `SaveSettings` IPC → `V2ModuleContext` → fire event. ~20 lines on each side. Eliminates the "restart to apply" friction in module config UI.

2. **Optional manifest fields for registry parity.** Add `moduleId` (GUID), `pageUrl`, `usageInstructions` to the bundled `manifest.json` schema. Make `WriteManifest` ([ModuleRegistryService.cs:160](src/VRCFaceTracking.Core/Services/ModuleRegistryService.cs)) preserve them. Lets registry-listed modules carry setup hints into the local install.

## File:line cheat-sheet for downstream chats

| What | Where |
|---|---|
| v2 module interface | [src/VRCFaceTracking.SDKv2/ITrackingModuleV2.cs](src/VRCFaceTracking.SDKv2/ITrackingModuleV2.cs) |
| v2 host context | [src/VRCFaceTracking.SDKv2/IModuleContext.cs](src/VRCFaceTracking.SDKv2/IModuleContext.cs) |
| v2 config schema | [src/VRCFaceTracking.SDKv2/Configuration/ConfigSchema.cs](src/VRCFaceTracking.SDKv2/Configuration/ConfigSchema.cs) |
| v2 IPC framing | [src/VRCFaceTracking.Core/Sandboxing/V2/V2Message.cs](src/VRCFaceTracking.Core/Sandboxing/V2/V2Message.cs) |
| v2 module loader | [src/VRCFaceTracking.ModuleHostV2/ModuleAssemblyV2.cs](src/VRCFaceTracking.ModuleHostV2/ModuleAssemblyV2.cs) |
| v2 host context impl | [src/VRCFaceTracking.ModuleHostV2/V2ModuleContext.cs](src/VRCFaceTracking.ModuleHostV2/V2ModuleContext.cs) |
| Manager that orchestrates lifecycle | [src/VRCFaceTracking.Core/Library/UnifiedLibManager.cs](src/VRCFaceTracking.Core/Library/UnifiedLibManager.cs) |
| Vue config panel | [ui/src/components/ModuleConfigPanel.vue](ui/src/components/ModuleConfigPanel.vue) |
| Modules page | [ui/src/views/ModulesView.vue](ui/src/views/ModulesView.vue) |
