# State of the rebuild — punch list

_Snapshot: 2026-04-27. Branch `temp/great-khorana-f3f2ab`. Build: ✅ clean (0 warnings, 0 errors, .NET 10)._

## Project shape, in one paragraph

VRCFaceTracking bridges face/eye-tracking hardware to VRChat over OSC. The host loads "modules" — DLLs implementing one of two SDKs — that produce expression/eye/head data. The rebuild replaces the previous WinUI3/XAML app with a **Photino + Vue 3** host (`src/VRCFaceTracking.App` shells `ui/` as a webview), keeps a backwards-compatible v1 SDK ([src/VRCFaceTracking.SDK/ExtTrackingModule.cs](src/VRCFaceTracking.SDK/ExtTrackingModule.cs) — abstract class, UDP IPC to [src/VRCFaceTracking.ModuleHost](src/VRCFaceTracking.ModuleHost)), and adds a new v2 SDK ([src/VRCFaceTracking.SDKv2/ITrackingModuleV2.cs](src/VRCFaceTracking.SDKv2/ITrackingModuleV2.cs) — async interface, Named-Pipe + JSON IPC to [src/VRCFaceTracking.ModuleHostV2](src/VRCFaceTracking.ModuleHostV2), with declarative UI config). Modules are sandboxed per-process. Two in-repo modules ship: [modules/VRCFaceTracking.EmulatedTracking](modules/VRCFaceTracking.EmulatedTracking) (mic-driven Goertzel DSP → ARKit blendshapes) and [modules/VRCFaceTracking.AdvancedEmulation](modules/VRCFaceTracking.AdvancedEmulation) (behavioural eye/expression synthesis). A registry client at [src/VRCFaceTracking.Core/Services/ModuleRegistryService.cs](src/VRCFaceTracking.Core/Services/ModuleRegistryService.cs) fetches third-party modules from `https://registry.vrcft.io/modules`.

## Repo ownership — Part 1 status

**Skipped.** Origin is `benaclejames/VRCFaceTracking`; authenticated user `RealWhyKnot` has only `pull` permission (`gh api repos/benaclejames/VRCFaceTracking` → `permissions.admin: false`). `gh repo rename WKVRCFT` cannot run. To proceed:
1. Fork upstream to `RealWhyKnot/VRCFaceTracking` (or push current state to a brand-new `RealWhyKnot/WKVRCFT`).
2. Run `git remote set-url origin <new>`; `git push -u origin master`.
3. Then `gh repo rename WKVRCFT` if the fork name needs changing.
4. Local folder rename: close all tools, then `Move-Item D:\Github\VRCFaceTracking D:\Github\WKVRCFT`.

Folder rename is independent of the GitHub move and entirely safe to defer.

## Punch list — what's left to wrap up

Prioritised. Each item is concrete with file:line.

### P0 — blocks shipping

1. **Module manifest is missing fields the registry expects.** Bundled `modules/*/manifest.json` only has `sdk`, `version`, `name`, `description`, `author`. The registry-side `TrackingModuleMetadata` ([src/VRCFaceTracking.Core/Models](src/VRCFaceTracking.Core/Models) — referenced by [ModuleInstaller.cs:31,91,124](src/VRCFaceTracking.Core/Services/ModuleInstaller.cs)) needs `PackageId`, `DisplayName`, `Version`, `DownloadUrl`, `DllFileName`, `Md5Hash`, plus the v1 fields `ModuleId`, `ModulePageUrl`, `UsageInstructions` that real third-party modules ship with (see [reference/VirtualDesktop.VRCFaceTracking/module.json](reference/VirtualDesktop.VRCFaceTracking/module.json)). Pick one schema for both bundled-and-registry use, document it, and bring the two in-repo modules up to spec. Without this, any registry-installed module that previously relied on `UsageInstructions` (e.g. "install VD Streamer 1.30+") loses its setup hint.

2. **`EmulatedTracking` description lies.** [modules/VRCFaceTracking.EmulatedTracking/manifest.json:5](modules/VRCFaceTracking.EmulatedTracking/manifest.json) says "via NVIDIA Audio2Face-3D NIM". Actual implementation is on-device Goertzel DSP ([modules/VRCFaceTracking.EmulatedTracking/SignalProcessingBackend.cs](modules/VRCFaceTracking.EmulatedTracking/SignalProcessingBackend.cs)). The Audio2Face plan in the 2026-04-02 design doc was abandoned. Fix the description or the implementation.

3. **`UnifiedExpressions` enum lives in Core but every v2 module needs it.** [modules/VRCFaceTracking.AdvancedEmulation/AdvancedEmulationModule.cs:4,69-111](modules/VRCFaceTracking.AdvancedEmulation/AdvancedEmulationModule.cs) and [modules/VRCFaceTracking.EmulatedTracking/ARKitMapper.cs:1](modules/VRCFaceTracking.EmulatedTracking/ARKitMapper.cs) both import `VRCFaceTracking.Core.Params.Expressions.UnifiedExpressions`. The .csprojs at [modules/VRCFaceTracking.AdvancedEmulation/VRCFaceTracking.AdvancedEmulation.csproj:9](modules/VRCFaceTracking.AdvancedEmulation/VRCFaceTracking.AdvancedEmulation.csproj) carry a `ProjectReference` to Core. Third-party modules cannot do this. **Move `UnifiedExpressions` (and any sibling Param-name constants modules need) into `VRCFaceTracking.SDKv2`** — or split a `VRCFaceTracking.SDKv2.Common` package. Until this is done, no third-party v2 module can be authored without bundling Core.dll.

### P1 — visible UX gaps

4. **No equivalent of v1's `OnActiveChange` callback.** Old `ModuleMetadata` ([reference/VRCFaceTracking/VRCFaceTracking.SDK/ModuleState.cs](reference/VRCFaceTracking/VRCFaceTracking.SDK/ModuleState.cs)) let modules notify the host when activation state flipped. v2 [src/VRCFaceTracking.SDKv2/IModuleContext.cs:30](src/VRCFaceTracking.SDKv2/IModuleContext.cs) has `PublishEvent` (module→host) but no host→module event channel. If you want hot-toggle of features inside a running module without a full reload, you need an `IModuleContext.OnSettingsChanged` or similar.

5. **No UI for module crashes / state.** The Vue store models `mod.crashCount` and `mod.status` ∈ `Active|Idle|InitFailed` ([ui/src/views/ModulesView.vue:46-54,126-130](ui/src/views/ModulesView.vue)) but there's no surface explaining *why* `InitFailed` happened. `mod.lastMessage` is rendered but it's the only diagnostic. A click-to-expand crash log would close the loop.

6. **Registry endpoint is hardcoded.** [src/VRCFaceTracking.Core/Services/ModuleRegistryService.cs:11](src/VRCFaceTracking.Core/Services/ModuleRegistryService.cs) — `https://registry.vrcft.io/modules`. If that domain is owned by upstream and this becomes a hard fork (`WKVRCFT`), you either need to keep using it (and accept upstream gating registry membership), stand up your own registry, or make the URL settings-configurable. Decision needed.

7. **Built-in modules show in the registry view only via `ScanLocalInstalls`.** [src/VRCFaceTracking.Core/Services/ModuleRegistryService.cs:124-155](src/VRCFaceTracking.Core/Services/ModuleRegistryService.cs) reads installed modules from `%LOCALAPPDATA%\VRCFaceTracking\modules` only — but the in-repo built-ins live alongside the App on a dev build. Confirm there's a separate "built-in" enumeration path; otherwise built-in modules won't appear after a fresh clone.

### P2 — known TODOs in code

8. UDP packet chunking unfinished — [src/VRCFaceTracking.Core/Sandboxing/VrcftSandboxClient.cs:99,109,114](src/VRCFaceTracking.Core/Sandboxing/VrcftSandboxClient.cs), [UdpFullDuplex.cs:274,299](src/VRCFaceTracking.Core/Sandboxing/UdpFullDuplex.cs), [PartialPacket.cs:146](src/VRCFaceTracking.Core/Sandboxing/IPC/PartialPacket.cs). Only matters for v1 modules that produce large packets; deprioritise if v1 is sunsetting.
9. [src/VRCFaceTracking.Core/Sandboxing/VrcftPacketDecoder.cs:115](src/VRCFaceTracking.Core/Sandboxing/VrcftPacketDecoder.cs) — `NotImplementedException` on unknown packet types. Should swallow + log, not throw.
10. [src/VRCFaceTracking.Core/Sandboxing/VrcftSandboxServer.cs:60](src/VRCFaceTracking.Core/Sandboxing/VrcftSandboxServer.cs) — `// @TODO: Use packet`.
11. [src/VRCFaceTracking.Core/Params/Data/UnifiedTrackingMutator.cs:86](src/VRCFaceTracking.Core/Params/Data/UnifiedTrackingMutator.cs) — calibration/correction state isn't persisted to settings.

### P3 — branch hygiene

12. Local `temp/angry-franklin` worktree exists at `D:\Github\VRCFaceTracking\.claude\worktrees\angry-franklin` — separate parallel chat in flight, do not touch.
13. Branch is **2 ahead, 5 behind** `origin/master`. Upstream commits not yet merged into the rebuild include `feat/eyelid-blend` mutator (`8660166`), `fix(core): cross-platform/async fixes` (`9ab5ef3`), parent-PID watchdog (`d1c911d`), mismatched head-rotation params (`e1c7429`). Decide cherry-pick vs leave-them-on-upstream for each.
14. 5 open PRs upstream (`gh pr list`); none of them are yours to merge, but #346 (Exit on SteamVR shutdown), #259 (OSC parsing optimizations) might be worth porting.

## What's working (don't break)

- Build is clean across all 8 projects (.NET 10, 0 warnings).
- ModuleHostV2 IPC: handshake → init → tracking-data stream → settings → config schema → shutdown — message types fully defined ([src/VRCFaceTracking.Core/Sandboxing/V2/V2Message.cs:9-41](src/VRCFaceTracking.Core/Sandboxing/V2/V2Message.cs)).
- End-to-end module-config UI: module → `RegisterConfigSchema` → IPC → host → store → [ui/src/components/ModuleConfigPanel.vue](ui/src/components/ModuleConfigPanel.vue). Verified wired in [ModulesView.vue:143,252](ui/src/views/ModulesView.vue).
- Registry fetch + install + MD5 verify + concurrent-install guard at [ModuleInstaller.cs:39,91](src/VRCFaceTracking.Core/Services/ModuleInstaller.cs).
- Both in-repo v2 modules call `RegisterConfigSchema` on init and read settings via `IModuleContext.Settings` — pattern is proven.
