# Writing a v2 module

A WKVRCFT module is a .NET 10 DLL that implements `ITrackingModuleV2`. The host loads it into a sandboxed sub-process, hands it an `IModuleContext`, and calls `UpdateAsync` ~100 times per second. Your module produces eye/expression/head data; the host forwards it to VRChat as OSC.

You only need a reference to the **`VRCFaceTracking.SDKv2`** assembly. The host wires up the rest.

---

## Minimal module

```csharp
using VRCFaceTracking.V2;
using VRCFaceTracking.V2.Configuration;
using VRCFaceTracking.SDKv2.Expressions;

[ModuleMetadata(Name = "My Tracker", Author = "you", Version = "1.0.0",
                Description = "Drives jaw open from a sine wave.")]
public class MyTrackerModule : ITrackingModuleV2
{
    private IModuleContext _ctx = null!;
    private float _phase;

    public ModuleCapabilities Capabilities => ModuleCapabilities.Expression;

    public Task<bool> InitializeAsync(IModuleContext context)
    {
        _ctx = context;
        _ctx.Logger.LogInformation("MyTracker initialised");
        return Task.FromResult(true);
    }

    public Task UpdateAsync(CancellationToken ct)
    {
        _phase += 0.1f;
        var jaw = (MathF.Sin(_phase) + 1f) * 0.5f;
        _ctx.TrackingData.SetExpression(UnifiedExpressions.JawOpen, jaw);
        return Task.CompletedTask;
    }

    public Task ShutdownAsync() => Task.CompletedTask;
}
```

That's a complete module. Drop it next to a `manifest.json` (see below) under `%LOCALAPPDATA%\VRCFaceTracking\modules\MyTracker\` and the host picks it up.

---

## `IModuleContext` surface

Everything you can do as a module flows through the context handed to `InitializeAsync`. Hold a reference; the host owns its lifetime.

| Member | Purpose |
|---|---|
| `ILogger Logger` | Forward log entries to the host. Routed to the host log view; respects log level. |
| `IModuleSettings Settings` | Read/write per-module settings. Persisted as `settings.json` in your module directory. `GetSetting<T>(key, default)`, `SetSetting<T>`, `SaveAsync`. |
| `ITrackingDataWriter TrackingData` | Push tracking output: `SetExpression(UnifiedExpressions, float)`, `SetLeftEye / SetRightEye`, `SetHeadRotation`, `SetHeadPosition`. Buffered; flushed at the end of each `UpdateAsync` tick. |
| `void PublishEvent(string eventType, object? payload)` | Module → host event channel. Reserved for future UI integration. |
| `void RegisterConfigSchema(ConfigSchema schema)` | Declare a settings UI. See the next section. |
| `event Action<string, object?>? OnSettingChanged` | Fires when the user changes a config value while your module is running. See "Live config updates". |

Your module process exits when the host goes down or when the user disables the module. `ShutdownAsync` runs first, with a short grace window.

---

## Declarative config UI — `RegisterConfigSchema`

You don't ship UI markup; you ship a schema, and the host renders a Vue panel from it.

```csharp
public Task<bool> InitializeAsync(IModuleContext ctx)
{
    var schema = new ConfigSchema
    {
        Fields = new()
        {
            new ConfigField {
                Key = "sensitivity", Label = "Sensitivity",
                Type = ConfigFieldType.Float,
                DefaultValue = 1.0f, Min = 0.0f, Max = 4.0f
            },
            new ConfigField {
                Key = "device", Label = "Microphone",
                Type = ConfigFieldType.Enum,
                Options = new[] { "Default", "Headset", "Webcam" },
                DefaultValue = "Default"
            },
            new ConfigField {
                Key = "enableHeadDroop", Label = "Head droop on yawn",
                Description = "Tilts the head down briefly during a detected yawn.",
                Type = ConfigFieldType.Bool, DefaultValue = false
            }
        }
    };
    ctx.RegisterConfigSchema(schema);

    // Read initial values; the host will have populated settings.json with defaults.
    _sensitivity = ctx.Settings.GetSetting("sensitivity", 1.0f);
    _device      = ctx.Settings.GetSetting("device", "Default");
    return Task.FromResult(true);
}
```

Supported `ConfigFieldType` values: `Float`, `Int`, `Bool`, `String`, `Enum`, `FilePath`. Use `Min`/`Max` for numeric ranges and `Options` for `Enum`. `Description` is rendered as helper text.

End-to-end: your schema travels over the V2 named pipe (`V2MessageType.ConfigSchema`, type 13), the host stores it on the module record, and the Modules view renders a panel against it. When the user clicks Save, the values arrive back as a settings push (see next section).

---

## Live config updates — `OnSettingChanged`

Before this event existed, modules read settings once during `InitializeAsync` and required a restart to pick up changes. With `OnSettingChanged`, you can rebind state on the fly.

```csharp
public Task<bool> InitializeAsync(IModuleContext ctx)
{
    _ctx = ctx;
    _device = ctx.Settings.GetSetting("device", "Default");

    ctx.OnSettingChanged += (key, _) =>
    {
        // By the time this fires, ctx.Settings already reflects the new value.
        switch (key)
        {
            case "device":
                _device = ctx.Settings.GetSetting("device", "Default");
                ReopenMicrophone(_device);
                break;
            case "sensitivity":
                _sensitivity = ctx.Settings.GetSetting("sensitivity", 1.0f);
                break;
        }
    };

    return Task.FromResult(true);
}
```

Notes:
- The handler runs on a background pipe-reader thread. Keep it quick and thread-safe.
- The boxed `newValue` argument is the JSON-deserialised value (a `JsonElement`-backed `object?`). For typed reads, prefer calling `Settings.GetSetting<T>` inside the handler — it returns the new value directly.
- Only **changed** keys fire the event; resaving an unchanged setting is a no-op.
- Exceptions thrown from your handler are swallowed by the host so a buggy handler can't take down the module.

---

## `manifest.json` — required next to your DLL

The host parses this loosely (`JsonDocument`), so unknown fields are tolerated. Keep what you need.

| Field | Type | Required? | Meaning |
|---|---|---|---|
| `sdk` | int | yes | `2` for v2 modules. `1` is the legacy SDK. |
| `packageId` | string | yes | Stable identifier; matches the install directory name under `%LOCALAPPDATA%\VRCFaceTracking\modules\<packageId>\`. Must be unique. |
| `name` | string | yes | Display name shown in the Modules view. |
| `version` | string | yes | Semver-ish; parsed via `System.Version`. |
| `description` | string | yes | One-paragraph summary. Shown in the registry browser and the local Modules view. |
| `author` | string | yes | Maintainer or team name. |
| `dllFileName` | string | optional | Explicit DLL name. Falls back to `<packageId>.dll`, then to the first `.dll` in the directory. Recommended. |
| `pageUrl` | string | optional | Link to your homepage / GitHub / docs. Rendered as a "More info" link in the registry browser. |
| `usageInstructions` | string | optional | Setup hint shown to users (e.g. "Install Driver X 2.0+ before enabling"). Plain text. |
| `usesEye` | bool | optional | UI hint: this module produces eye data. |
| `usesExpression` | bool | optional | UI hint: this module produces expression data. |
| `tags` | string[] | optional | Free-form labels for filtering. |
| `iconUrl` | string | optional | Reserved for the registry view. |
| `md5Hash` | string | optional | MD5 of the downloadable zip. Verified by the registry installer. Not relevant for locally-installed modules. |
| `downloadUrl` | string | optional | URL the registry installer fetches. Set by the registry, not by you. |

### Complete example

```json
{
  "sdk": 2,
  "packageId": "com.example.MyTracker",
  "name": "My Tracker",
  "version": "1.0.0",
  "description": "Drives jaw motion from a sine wave. For demos only.",
  "author": "you",
  "dllFileName": "MyTracker.dll",
  "pageUrl": "https://github.com/you/MyTracker",
  "usageInstructions": "No setup required — enable the module and it runs.",
  "usesExpression": true,
  "tags": ["demo", "no-hardware"]
}
```

---

## Installing locally

For development:

1. Build your project to a folder (any location — call it `MyTracker.dll` plus its dependencies).
2. Create `%LOCALAPPDATA%\VRCFaceTracking\modules\<packageId>\` (the directory name must equal `packageId` from your manifest).
3. Drop the DLL, the manifest, and any runtime dependencies in that directory.
4. Launch (or restart) WKVRCFT. The Modules view will list it and you can enable it.

For end-user install via the registry: see [Registry](Registry).

---

## Capabilities and arbitration

Set `Capabilities` to the union of what your module produces:

```csharp
public ModuleCapabilities Capabilities =>
    ModuleCapabilities.Eye | ModuleCapabilities.Expression;
```

The host arbitrates between modules — only one module at a time owns each capability. If two modules both claim Eye, the user picks. Don't write to capabilities you don't claim — those writes are dropped.

---

## What v2 modules **cannot** do

- Ship arbitrary HTML / Avalonia / XAML UI. Declarative config is the supported surface.
- Receive arbitrary host events. The host → module channel currently carries settings only.
- Reach into Core types directly. Stay on the SDKv2 surface — the API there is the contract; everything else is implementation detail and may change.
