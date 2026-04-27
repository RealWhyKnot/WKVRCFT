# Build from source

WKVRCFT is a Windows-only build (the host targets `net10.0-windows` and depends on Photino + Windows Forms). The Vue UI builds anywhere but only matters in the context of the desktop host.

## Prerequisites

- **.NET 10 SDK preview** — `dotnet --version` should report 10.x. Available from [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).
- **Node.js 20+** and **npm** for the Vue UI.
- **PowerShell 7** (`pwsh`) to run `build.ps1`. Windows PowerShell 5.1 also works for the build script, but `pwsh` is the tested path.
- **git**.
- Windows 10/11 64-bit. The dev loop is best on a machine with VRChat or another OSC consumer running, but you can poke the host without one.

## One-time setup

```powershell
git clone https://github.com/RealWhyKnot/WKVRCFT.git
cd WKVRCFT
dotnet restore VRCFaceTracking.slnx
cd ui
npm install
cd ..
```

## Production build

```powershell
pwsh ./build.ps1
```

Output lands in `./dist/`:
- `VRCFaceTracking.exe` — the main host (renamed from `VRCFaceTracking.App.exe` by the script).
- `wwwroot/` — the built Vue UI, served by Photino at runtime.
- `builtin-modules/` — the bundled modules (EmulatedTracking, AdvancedEmulation), copied to `%LOCALAPPDATA%\VRCFaceTracking\modules\` on first run.
- `fti_osc.dll` — the native OSC helper.

`build.ps1 -Dev` produces a debug-friendly build that retains PDBs and enables verbose logging by default.

## Run the dev loop

For tight iteration on the C# host:

```powershell
dotnet run --project src/VRCFaceTracking.App
```

The host expects `wwwroot/` next to the exe. In a `dotnet run` build that's already wired via the `Content Include="wwwroot\**"` in the App csproj — the Vue side needs to have been built at least once (`cd ui; npm run build`).

For tight iteration on the Vue UI, run the Vite dev server:

```powershell
cd ui
npm run dev
```

…and point Photino at the dev URL by editing the App's startup to load `http://localhost:5173/` instead of the bundled `wwwroot/`. (Quick hack — there's no built-in toggle for this yet.)

## Debugging

- **Visual Studio / Rider:** open `VRCFaceTracking.slnx`. Set `VRCFaceTracking.App` as the startup project. F5 attaches to the host process. Module sub-processes (`VRCFaceTracking.ModuleHostV2.exe`) launch separately — attach manually via the Debug → Attach to Process menu if you need to step into module code.
- **Logs:** `%APPDATA%\VRCFaceTracking\logs\` for host logs. Each module's stdout/stderr is also forwarded to the host log view (and the last 50 lines are surfaced as a click-to-expand block on each module card).
- **Settings:** `%APPDATA%\VRCFaceTracking\settings\` — `app_config.json`, `osc_target.json`, `modules.json`. Delete to reset.

## Common gotchas

- If the Vue UI shows blank, your `wwwroot/` is stale or missing. Re-run `npm run build` in `ui/`.
- The native `fti_osc.dll` must sit next to the exe. The build script copies it; manual `dotnet build` runs do too via the App csproj's `<Content Include="..\..\fti_osc.dll" />`.
- Module sub-processes inherit the parent's environment but launch with their own working directory. If your module reads relative paths, anchor them via `Path.GetDirectoryName(typeof(MyModule).Assembly.Location)`.
