# `manifest.json` schema

Every module — bundled or registry-installed — ships a `manifest.json` next to its main DLL. The host reads this file at startup to enumerate modules, and the registry-install pipeline writes it after a successful download so version + setup metadata survive restarts.

The schema is loose JSON: the host parses with `JsonDocument` and reads only the fields it cares about, so unknown fields are tolerated. New fields can be added without breaking older hosts.

## Fields

| Field | Type | Required | Used by | Notes |
|---|---|---|---|---|
| `sdk` | int | yes | host loader | `2` = SDKv2 (Named Pipe, JSON IPC). `1` = legacy SDK. |
| `packageId` | string | yes | host + registry | Stable identifier; matches the install directory name under `%LOCALAPPDATA%\VRCFaceTracking\modules\<packageId>\`. Must be unique. |
| `name` | string | yes | UI | Human-readable display name shown in the Modules view. |
| `version` | string | yes | host + UI + registry | Semver-ish (`X.Y.Z`). Compared by `System.Version.Parse` — any 4-part numeric is fine. |
| `description` | string | yes | UI | One-paragraph summary. Should accurately describe what the module does. |
| `author` | string | yes | UI | Display name of the maintainer/team. |
| `dllFileName` | string | optional | host loader | If present, the host loads exactly this DLL. Otherwise it falls back to `<packageId>.dll`, then to the first `.dll` in the directory. Recommended for clarity. |
| `pageUrl` | string | optional | UI | Link to the module's homepage / GitHub / docs. Renders as a "More info" link in the registry browser. |
| `usageInstructions` | string | optional | UI | Setup hint shown to users in the registry view (e.g. "Install Driver X 2.0+ before enabling"). Plain-text, multi-paragraph OK. |
| `md5Hash` | string | optional | registry verifier | MD5 of the downloadable zip; the installer verifies post-download. Not relevant for bundled modules. |
| `downloadUrl` | string | optional | registry | URL the installer fetches. Not relevant for bundled modules. |
| `usesEye` | bool | optional | UI | Hint for the Modules view. Currently advisory only. |
| `usesExpression` | bool | optional | UI | As above. |
| `tags` | string[] | optional | UI | Free-form labels for filtering. |
| `iconUrl` | string | optional | UI | Reserved; not yet rendered. |
| `builtIn` | bool | optional | host | Set to `true` by the App at deploy time when copying built-in modules into the per-user modules dir. Don't set this manually. |

## Example — a bundled module

```json
{
  "sdk": 2,
  "packageId": "VRCFaceTracking.EmulatedTracking",
  "name": "Emulated Face Tracking",
  "version": "1.0.0",
  "description": "Drives facial expressions from microphone audio using on-device DSP …",
  "author": "VRCFaceTracking",
  "dllFileName": "VRCFaceTracking.EmulatedTracking.dll",
  "pageUrl": "https://github.com/RealWhyKnot/WKVRCFT/tree/main/modules/VRCFaceTracking.EmulatedTracking",
  "usageInstructions": "Pick a microphone in module settings, then enable the module.",
  "usesExpression": true,
  "tags": ["audio", "emulation", "no-hardware"]
}
```

## Example — a third-party module distributed via the registry

The registry payload uses different JSON property names (capitalised, prefixed) but maps to the same fields after deserialisation; see `src/VRCFaceTracking.Core/Models/TrackingModuleMetadata.cs` for the JSON-property aliases. After install, the host writes a normalised `manifest.json` (the shape above) into the install directory.
