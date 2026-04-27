# Architecture

WKVRCFT is a multi-process Windows application. A single foreground host runs the UI and orchestrates a small fleet of sandboxed module processes. Tracking data flows from modules → host → OSC → VRChat.

## Projects in the solution

| Project | Purpose |
|---|---|
| **`VRCFaceTracking.App`** | The desktop entry point. Hosts a Photino webview that renders the Vue UI, owns the OSC sender, owns module lifecycle via `UnifiedLibManager`. The exe shipped to users is this. |
| **`VRCFaceTracking.Core`** | Orchestration: module discovery, lifecycle, capability arbitration, registry client, settings persistence, IPC framing. Where most of the host logic lives. References both SDK projects. |
| **`VRCFaceTracking.SDKv2`** | The public API surface for v2 modules. `ITrackingModuleV2`, `IModuleContext`, `ITrackingDataWriter`, `ConfigSchema`, the `UnifiedExpressions` enum. A leaf project — does not reference Core. Third-party module authors build against this, and only this. |
| **`VRCFaceTracking.SDK`** | The legacy v1 SDK — `ExtTrackingModule` abstract class, UDP IPC. Kept for backward compatibility with existing third-party modules. New modules should target SDKv2. |
| **`VRCFaceTracking.ModuleHost`** | The v1 sandbox process. Loads a v1 module DLL into a collectible AssemblyLoadContext and bridges its sync API to the parent over UDP. |
| **`VRCFaceTracking.ModuleHostV2`** | The v2 sandbox process. Loads a v2 module DLL, hands it an `IModuleContext`, and bridges the async lifecycle over a Named Pipe. |
| **`modules/VRCFaceTracking.EmulatedTracking`** | Bundled module. Microphone audio → on-device DSP (Goertzel band energy + ZCR vowel classification) → ARKit blendshapes → unified expressions. |
| **`modules/VRCFaceTracking.AdvancedEmulation`** | Bundled module. Behavioural eye and expression synthesis: blink rhythms, saccades, mic-driven emotional state, head-droop fatigue. All features off by default. |
| **`ui/`** | Vue 3 + TypeScript + Vite SPA. Built into `wwwroot/` and served by Photino. |

## Process topology

```
┌────────────────────────────┐
│  VRCFaceTracking.App.exe   │  ← Photino webview hosts the Vue UI
│  (host, owns Vue UI)       │     OSC sender lives here
└───────────┬────────────────┘
            │   Named Pipe per module
            │   (vrcft-module-<id>)
   ┌────────┼────────┐
   ▼        ▼        ▼
 ┌───┐    ┌───┐    ┌───┐
 │M1 │    │M2 │    │M3 │     ModuleHostV2.exe instances
 └───┘    └───┘    └───┘     (one process per loaded module)
```

Each module runs in its own OS process. A module crash takes down only that process; the host detects the disconnect, surfaces it in the UI, and restarts the module up to a retry cap.

## Data flow — one tracking frame

```
[hardware / mic / synthesised]
            │
            ▼
   module.UpdateAsync(ct)              ~100 Hz, in-process to ModuleHostV2
            │
            │  ITrackingDataWriter.SetExpression / SetEye / SetHead*
            ▼
   V2TrackingDataWriter (buffer)       Named Pipe — V2MessageType.TrackingData
            │
            ▼
   V2PipeServer (on host)              Core
            │
            ▼
   UnifiedTrackingMutator               applies calibration, eyelid blend, etc.
            │
            ▼
   ParameterSenderService               maps to OSC parameter names
            │
            ▼
   OscSendService → UDP → VRChat
```

The Vue UI subscribes to a 30 Hz broadcast of the same data (`TrackingDataBroadcaster`) for the live preview and parameter view; that path is independent of the OSC send.

## Configuration UI

The host renders module settings declaratively. A v2 module calls `IModuleContext.RegisterConfigSchema` during init; the schema crosses the pipe (`V2MessageType.ConfigSchema`), the host stores it on the module record, and `ModulesView.vue` instantiates `ModuleConfigPanel.vue` against it. On save, the new values cross back over the pipe (`V2MessageType.Settings`) and the module's `OnSettingChanged` event fires for each changed key.

See [Module Authors](Module-Authors) for the schema fields and example code.

## v1 vs v2 — where they diverge

| | v1 | v2 |
|---|---|---|
| Module entry | abstract class `ExtTrackingModule` | interface `ITrackingModuleV2` |
| Lifecycle | sync `Initialize` / `Update` / `Teardown` | async `InitializeAsync` / `UpdateAsync` / `ShutdownAsync` |
| Data path | mutates `UnifiedTracking.Data` static | typed `ITrackingDataWriter` methods |
| Host IPC | UDP loopback, custom packet framing | Named Pipe, JSON-framed messages |
| Settings | none (modules use their own files) | `IModuleSettings` + host-managed `settings.json` |
| UI | none | declarative `ConfigSchema` rendered by host |
| Live config | no | `OnSettingChanged` event |
| Recommended for new work | no | yes |

Both SDKs target `net10.0` and load via collectible `AssemblyLoadContext` so the host can unload them at runtime.

## Where to look in code

| What | Where |
|---|---|
| Host entry point | `src/VRCFaceTracking.App/Program.cs` |
| Module lifecycle / discovery | `src/VRCFaceTracking.Core/Library/UnifiedLibManager.cs` |
| v2 module interface | `src/VRCFaceTracking.SDKv2/ITrackingModuleV2.cs` |
| v2 host context (module side) | `src/VRCFaceTracking.ModuleHostV2/V2ModuleContext.cs` |
| v2 IPC framing | `src/VRCFaceTracking.Core/Sandboxing/V2/V2Message.cs` |
| Registry client | `src/VRCFaceTracking.Core/Services/ModuleRegistryService.cs` |
| Vue config panel | `ui/src/components/ModuleConfigPanel.vue` |
| Modules page | `ui/src/views/ModulesView.vue` |
