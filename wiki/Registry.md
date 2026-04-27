# Module registry

The module registry is a hosted JSON list of available modules. WKVRCFT's "Browse" view fetches it, lets the user install entries, and tracks which versions are installed locally.

## Default endpoint

WKVRCFT defaults to `https://registry.vrcft.io/modules`. That endpoint is not operated by the WKVRCFT maintainers — ownership of the canonical registry endpoint for this fork is an open decision tracked in the [issue tracker](https://github.com/RealWhyKnot/WKVRCFT/issues). Until it's decided, the upstream endpoint is used by default so existing third-party modules show up.

## Pointing the host at a different registry

The URL is read from `AppConfig.RegistryUrl`. Edit `%APPDATA%\VRCFaceTracking\settings\app_config.json`:

```json
{
  "debugMode": false,
  "theme": "dark",
  "registryUrl": "https://your-host.example.com/modules"
}
```

Restart the host. The Browse view now hits your endpoint instead. Empty/missing/whitespace falls back to the default.

A settings-page UI for this is not yet wired; for now it's a config-file edit.

## Payload shape

The endpoint should return a JSON array of objects matching `TrackingModuleMetadata`:

```json
[
  {
    "ModuleId": "com.example.MyTracker",
    "ModuleName": "My Tracker",
    "AuthorName": "you",
    "ModuleDescription": "Drives jaw motion from a sine wave.",
    "Version": "1.0.0",
    "DownloadUrl": "https://github.com/you/MyTracker/releases/download/v1.0.0/MyTracker.zip",
    "FileHash": "0123456789abcdef0123456789abcdef",
    "DllFileName": "MyTracker.dll",
    "ModulePageUrl": "https://github.com/you/MyTracker",
    "UsageInstructions": "No setup required.",
    "UsesEye": false,
    "UsesExpression": true,
    "Tags": ["demo"],
    "IconUrl": ""
  }
]
```

Field-name capitalisation is the registry's convention (PascalCase, prefixed); the host maps these to the local manifest's camelCase fields after install. See [Module Authors](Module-Authors) for the local-manifest spec.

## Install flow

1. User clicks **Install** in Browse.
2. Host downloads `DownloadUrl` (a zip).
3. If `FileHash` is set, the host verifies MD5 before unpacking. Mismatch aborts the install.
4. Zip extracts into `%LOCALAPPDATA%\VRCFaceTracking\modules\<ModuleId>\`.
5. Host writes a normalised `manifest.json` into that directory carrying `version`, `pageUrl`, `usageInstructions`, etc., so the local view survives a registry outage.
6. Host fires its module-list-changed event; the UI refreshes.

The module loads on the next host launch (or when the user explicitly reloads modules). Concurrent installs are guarded — clicking Install twice on the same module is a no-op.

## Cache and offline behaviour

The registry response is cached in memory for 10 minutes. If a fetch fails, the last successful response is reused so an offline user can still see what's installed and what's available. A truly cold start with no network shows an empty Browse view but doesn't break the host.

## Hosting your own

The minimum is a static JSON file served over HTTPS. GitHub Pages, S3, or a tiny Cloudflare Worker all work. Set `Content-Type: application/json`. Update the file when you ship a new version of any listed module.

For richer registries — e.g. dynamic listings, per-user filters, hash registries — you can serve the same shape from any HTTP endpoint. The host doesn't care how it's generated, only that it returns the array shape above.
