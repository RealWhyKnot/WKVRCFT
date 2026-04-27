# Plan — extract emulated tracking into its own downloadable module repo

## Goal

The two in-repo modules at `modules/VRCFaceTracking.{EmulatedTracking,AdvancedEmulation}` should ship as a **separately downloadable module** that the host installs through the same registry pathway as any third-party module. The host program then no longer bundles them.

## Suggested repo name

**`WKVRCFT-EmulatedModule`** (or `WKVRCFT-FakeTracking`). Convention matches `WKVRCProxy`. Final call up to the user.

## Current state — quick recap

Both modules already live in their own .csproj subfolders, target net10.0, implement `ITrackingModuleV2`, ship a bundled `manifest.json`, and call `RegisterConfigSchema`. They are **almost** drop-in extractable. Two coupling problems block a clean lift.

### Coupling problem #1 — `UnifiedExpressions` lives in Core

Both modules import `VRCFaceTracking.Core.Params.Expressions.UnifiedExpressions`:

- [modules/VRCFaceTracking.AdvancedEmulation/AdvancedEmulationModule.cs:4,69-111](modules/VRCFaceTracking.AdvancedEmulation/AdvancedEmulationModule.cs) — caches 30+ enum values as static int indices.
- [modules/VRCFaceTracking.EmulatedTracking/ARKitMapper.cs:1](modules/VRCFaceTracking.EmulatedTracking/ARKitMapper.cs) — same pattern.

Both .csprojs carry `<ProjectReference Include="..\..\src\VRCFaceTracking.Core\VRCFaceTracking.Core.csproj" />` ([example](modules/VRCFaceTracking.AdvancedEmulation/VRCFaceTracking.AdvancedEmulation.csproj):9). A standalone repo cannot do this; it needs `UnifiedExpressions` to be reachable via NuGet (or via the SDK assembly).

**Resolution path (must be done in the host repo first):** promote the enum.

1. Move `VRCFaceTracking.Core.Params.Expressions.UnifiedExpressions` (and any sibling constants like `UnifiedExpressionsCount`) into `src/VRCFaceTracking.SDKv2/Expressions/`. Keep the namespace `VRCFaceTracking.V2.Expressions` to avoid cross-collision, or use a `[assembly: TypeForwardedTo]` shim in Core for backward compat with v1.
2. Update both in-repo modules to use the new location.
3. Drop the Core `ProjectReference` from both module .csprojs.
4. Verify build, push to host repo, **then** start the new repo.

This is the single blocker. ~50 lines of namespace plumbing.

### Coupling problem #2 — SDK isn't on NuGet

The new repo needs to consume `VRCFaceTracking.SDKv2` as a `PackageReference`, not a `ProjectReference`. Two options:

- **Publish `VRCFaceTracking.SDKv2` as a NuGet package** (preferred — same pattern as upstream's old SDK). Add `<IsPackable>true</IsPackable>` and packaging metadata to the SDK csproj; CI publishes on tag. This unblocks all third-party authors, not just emulation.
- **Vendor the SDK DLL** into the new repo's `vendor/` folder and `<Reference HintPath="vendor/VRCFaceTracking.SDKv2.dll" />`. Faster but brittle; version drift becomes the user's problem.

Recommend NuGet.

## What lifts cleanly vs. what needs rewriting

### Lifts as-is (no changes once Core enum is moved)

- All `Behaviours/` files in AdvancedEmulation: `BlinkController`, `EmotionalStateTracker`, `MicPatternDetector`, `SessionFatigueTracker`, `TiltSimulator`, `YawnAnimator`.
- All `Audio/` files: `MicAnalyser`, mic capture via NAudio.
- `AdvancedEmulationConfig.cs`, `EmulatedTrackingConfig.cs` — schema builders.
- `SignalProcessingBackend.cs`, `ProsodyHeadEstimator.cs`, `ARKitMapper.cs` (after enum move).
- The two module classes themselves.

### Needs minor rewrite

- The two `.csproj` files — drop Core ProjectReference, change SDKv2 to PackageReference.
- Both `manifest.json` files — once a final manifest schema is decided in the host repo, bring them up to spec (probably needs `packageId`, `downloadUrl`, `dllFileName`, `md5Hash` — though several are filled in by the registry, not the bundle).
- `EmulatedTracking`'s manifest description ("via NVIDIA Audio2Face-3D NIM") — fix to reflect actual Goertzel implementation.

### Will need new

- `build.ps1` — mirror the WKVRCProxy pattern: build → produce `dist/<module>.zip` containing the DLL + manifest.json + dependencies (e.g. NAudio.dll). Refer to [reference/WKVRCProxy/build.ps1](reference/WKVRCProxy/build.ps1) for shape.
- `version.txt` — `YYYY.M.D.PATCH-HASH` format, same as WKVRCProxy.
- A GitHub release workflow that uploads the zip and (optionally) PRs the registry.
- README — what it does, prerequisites (microphone), supported VRCFT host versions.

## Suggested repo structure

```
WKVRCFT-EmulatedModule/
├── src/
│   ├── EmulatedTracking/          # was modules/VRCFaceTracking.EmulatedTracking/
│   │   ├── EmulatedTrackingModule.cs
│   │   ├── EmulatedTrackingConfig.cs
│   │   ├── SignalProcessingBackend.cs
│   │   ├── ProsodyHeadEstimator.cs
│   │   ├── ARKitMapper.cs
│   │   ├── manifest.json
│   │   └── EmulatedTracking.csproj
│   └── AdvancedEmulation/         # was modules/VRCFaceTracking.AdvancedEmulation/
│       ├── AdvancedEmulationModule.cs
│       ├── AdvancedEmulationConfig.cs
│       ├── Audio/
│       │   ├── MicAnalyser.cs
│       │   └── MicPatternDetector.cs
│       ├── Behaviours/
│       │   ├── BlinkController.cs
│       │   ├── EmotionalStateTracker.cs
│       │   ├── SessionFatigueTracker.cs
│       │   ├── TiltSimulator.cs
│       │   └── YawnAnimator.cs
│       ├── manifest.json
│       └── AdvancedEmulation.csproj
├── dist/                          # gitignored — build output
├── vendor/                        # gitignored unless we vendor SDK DLL
├── build.ps1
├── version.txt
├── WKVRCFT-EmulatedModule.slnx
├── README.md
└── .gitignore
```

Decision: **two separate modules in one repo, two separate zip outputs.** They're orthogonal designs (see EmulatedTracking = audio→DSP→ARKit; AdvancedEmulation = behavioural state machine + optional mic). Users pick one. Distributing both from one repo means one CI, one version line, two zip artifacts. Alternative: separate repos. One repo is simpler.

## Migration path (don't break users)

Ship in three phases:

### Phase 1 — host-side prep (1 PR in WKVRCFT)
- Move `UnifiedExpressions` to SDKv2.
- Publish SDKv2 to NuGet (or set up vendoring).
- Both in-repo modules updated to consume the new layout. Bundled modules still ship in `modules/`. Behaviour identical for end users.

### Phase 2 — new repo lives alongside (1 PR in WKVRCFT, plus the new repo)
- Create `WKVRCFT-EmulatedModule` repo, lift the source, ship a release.
- Add the two modules to `https://registry.vrcft.io/modules` (or whichever registry you point at). They're now installable via Browse Registry in-app.
- Host repo keeps shipping `modules/VRCFaceTracking.AdvancedEmulation/` and `modules/VRCFaceTracking.EmulatedTracking/` as built-in defaults so existing users don't lose tracking on update.

### Phase 3 — deprecate built-ins (later release)
- Remove the two `modules/*` subfolders from the host repo.
- Bump host version with release notes: "Emulation modules now distributed separately — install from registry."
- Optional: first-run UI nudge that auto-installs them if neither is present.

## Blockers that need user decisions

1. **Where does the registry point?** ([STATE.md](docs/assessment/STATE.md) item P1-6.) If `registry.vrcft.io` stays upstream, the new repo's modules need to be accepted by upstream's registry curators. Otherwise stand up a new registry — perhaps `registry.whyknot.dev/modules` — and switch the host's `RegistryUrl` constant.
2. **NuGet publishing.** Who publishes `VRCFaceTracking.SDKv2`? Personal NuGet account or org? `RealWhyKnot.VRCFaceTracking.SDKv2` package ID is fine.
3. **Repo naming.** `WKVRCFT-EmulatedModule` vs `WKVRCFT-FakeTracking` vs split. Aesthetic.
4. **One repo or two?** The two modules are independent. One repo, two artifacts is cleaner; two repos (matching `WKVRCProxy`'s pattern) gives independent versioning.

## First-steps checklist (for whoever picks this up)

- [ ] In WKVRCFT host repo: move `UnifiedExpressions` to `src/VRCFaceTracking.SDKv2/Expressions/`. Update both in-repo modules. Build clean. Commit.
- [ ] Add NuGet packaging metadata to `src/VRCFaceTracking.SDKv2/VRCFaceTracking.SDKv2.csproj` (`<IsPackable>`, `<PackageId>`, `<Version>`, etc.). Decide owner.
- [ ] Resolve the manifest schema question — write down the canonical bundled-manifest fields ([STATE.md P0-1](docs/assessment/STATE.md)).
- [ ] Create `WKVRCFT-EmulatedModule` repo. Lift sources. Adjust namespaces and project refs. First green build.
- [ ] Mirror WKVRCProxy's `build.ps1` to produce per-module zips into `dist/`.
- [ ] First release; smoke-test installing it through the host's Browse Registry flow.
- [ ] Decide phase-3 timing for removing the in-repo built-ins.
