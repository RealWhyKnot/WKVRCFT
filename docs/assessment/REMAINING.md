# Remaining work — what was deliberately deferred

_Snapshot: 2026-04-27, after the WKVRCFT split from upstream and the P0/P1 punch list from [STATE.md](STATE.md). All P0 and most P1 items landed; what's below is intentionally not fixed in this round, with reasoning so the next chat doesn't have to re-derive it._

## P1 — open decisions for the user

### Registry-endpoint ownership (was STATE.md P1-6)

`registry.vrcft.io` belongs to upstream. WKVRCFT now reads the URL from `AppConfig.RegistryUrl` (defaulting to upstream's URL — no behavioural change), so swapping is a settings edit, not a code change. The user still needs to pick a destination:

| Option | Pros | Cons |
|---|---|---|
| **Keep upstream's registry** | Zero infra; existing third-party modules show up automatically | Upstream curators control which modules get listed; if WKVRCFT diverges, listings could be filtered out |
| **Self-host at e.g. `registry.whyknot.dev/modules`** | Full control of curation; can list WKVRCFT-only forks of modules | New infra to run; users have to migrate; have to keep it healthy |
| **Static manifest list (JSON file in a repo)** | Lowest possible ops; just a Pages-served JSON | Manual updates per release; no real "registry" semantics |
| **Hybrid (federate)** | Pull upstream's list and append/override locally | Most code; partial trust model |

The host code is ready for any of them — `AppConfig.RegistryUrl` accepts an arbitrary URL that returns the same JSON shape upstream uses (see `TrackingModuleMetadata`). To swap at runtime, edit `%APPDATA%\VRCFaceTracking\settings\app_config.json` and set `"registryUrl": "https://your-host/modules"`.

UI for changing this from the settings page is **not built yet** — quick win for a follow-up: add a text field on `SettingsView.vue` that writes `appConfig.registryUrl` and persists.

### NuGet packaging for `VRCFaceTracking.SDKv2`

Per [EMULATED-MODULE-PLAN.md](EMULATED-MODULE-PLAN.md) phase 1: SDKv2 should ship as a NuGet package so third-party module repos can `<PackageReference>` it instead of vendoring a DLL. Decisions needed:

1. Package ID. Suggestion: `RealWhyKnot.VRCFaceTracking.SDKv2` (clear ownership; easy to swap if upstream ever publishes one).
2. Publishing identity. The user's NuGet.org account, or a new org account.
3. Version policy. Suggest mirroring the host version line so SDK and host advance together.

Until this lands, third-party modules can `<Reference HintPath="..\path\to\VRCFaceTracking.SDKv2.dll" />` against a vendored DLL.

### Cherry-pick decisions for upstream commits

WKVRCFT is currently 5+ commits behind `benaclejames/master` on the legacy SDK + mutator path. Each is small enough to cherry-pick standalone but is a user-judgment call:

| Commit | Subject | Recommendation |
|---|---|---|
| `4c58db6` | `feat(mutator): More parameter blending` | Cherry-pick — touches `Core/Params/Data/Mutation/`, no SDKv2 conflict |
| `8660166` | `feat(mutators): Add eyelid blend mutation property` | Cherry-pick — same area, completes the v1 mutator UX |
| `d1c911d` | `fix: Add parent pid option to ModuleProcess + watchdog` | Cherry-pick — pure robustness improvement |
| `e1c7429` | `fix: Mismatched head rotation parameters` | Cherry-pick — bug fix, low risk |
| `5aed3bf` | `fix: Eyebrow shapes regression in eye-only modules` | Already on master via merge `3ca4141` — verify, no action |

Open upstream PRs worth porting (titles from `gh pr list` against `benaclejames/VRCFaceTracking`):

- **#346** `fix: Exit VRCFT when SteamVR shuts down` — quality-of-life; safe.
- **#259** `Optimizations for parsing and receiving OSC messages` — perf; review for behaviour changes.
- **#263** `Mockup for linearly normalized eye gaze values` — design call, not just code; defer.

## P2 — known TODOs in code (not addressed this round)

The assessment listed these. They didn't make the cut because each requires either reaching consensus on v1 sunset timing, or doing real protocol/state-machine work. Unchanged from STATE.md:

- UDP packet chunking unfinished — [VrcftSandboxClient.cs](../../src/VRCFaceTracking.Core/Sandboxing/VrcftSandboxClient.cs):99,109,114, [UdpFullDuplex.cs](../../src/VRCFaceTracking.Core/Sandboxing/UdpFullDuplex.cs):274,299, [PartialPacket.cs](../../src/VRCFaceTracking.Core/Sandboxing/IPC/PartialPacket.cs):146. **Defer** until a v1 module produces a packet large enough to need it; today none do.
- [VrcftPacketDecoder.cs](../../src/VRCFaceTracking.Core/Sandboxing/VrcftPacketDecoder.cs):115 throws `NotImplementedException` on unknown packet types. Should swallow + log. **Quick fix; trivial.**
- [VrcftSandboxServer.cs](../../src/VRCFaceTracking.Core/Sandboxing/VrcftSandboxServer.cs):60 `// @TODO: Use packet`. Read the comment — no functional impact.
- [UnifiedTrackingMutator.cs](../../src/VRCFaceTracking.Core/Params/Data/UnifiedTrackingMutator.cs):86 — calibration/correction state isn't persisted. **Real fix:** wire to `SettingsService`. ~30 lines but needs UX for "reset calibration".

## P3 — branch hygiene

- Local `temp/angry-franklin` and `temp/great-khorana-f3f2ab` worktrees still exist alongside this one. Remove them once the corresponding chats are wrapped: `git worktree remove .claude/worktrees/<name>`.
- Folder rename for the user's benefit: `Move-Item D:\Github\VRCFaceTracking D:\Github\WKVRCFT` after closing all tools (Visual Studio, Claude Code, Photino dev runs).
- The `master` branch in the user's main worktree at `D:\Github\VRCFaceTracking` still tracks upstream. To make it `main` against the new origin: `git fetch origin && git branch -m master main && git branch --set-upstream-to=origin/main main`. (Run from the main worktree, not this one.)

## Followups exposed by this round (new since STATE.md)

### Bundled modules don't subscribe to `OnSettingChanged` yet

The new event ([commit c4744bf](#)) gives modules a way to react to live config changes. Both bundled modules still re-bind only on `InitializeAsync`. Low priority — the modules' settings are mostly slow-tuning behavioural knobs that don't *need* live updates. Mic device selection in AdvancedEmulation is the obvious candidate for live re-bind.

### Vue settings-page UI for `RegistryUrl`

Mentioned above. ~15 lines on `SettingsView.vue`.

### `JsonElement.DeepEquals` requires .NET 9+

Used in `V2ModuleSettings.ApplyFromHost`. We target .NET 10 so it's fine, but if you ever back-port to .NET 8 this needs a manual JSON-string compare.

### LICENSE language

The README's Credits section originally said "MIT" per the task brief, but the actual `LICENSE` file is **Apache 2.0**. The README and the WhyKnot copyright addition both reflect that now. If the user prefers MIT going forward, that's a license switch (whole-file change), not a clarification.
