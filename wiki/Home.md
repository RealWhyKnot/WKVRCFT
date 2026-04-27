# WKVRCFT wiki

WKVRCFT is a Windows host that bridges face- and eye-tracking hardware to VRChat over OSC. Its job is to load **modules** — DLLs that produce eye, expression, and head data — and forward that data to the avatar via VRChat's OSC interface. Modules run in sandboxed sub-processes, declare their own configuration UI, and can be installed at runtime from a registry.

This wiki covers the things that aren't in the README: how to write a module, how to build the host from source, and how the pieces fit together.

## Pages

- **[Module Authors](Module-Authors)** — write a v2 module. The `IModuleContext` API, the declarative config schema, the live-settings event, the manifest format, and how to ship.
- **[Build From Source](Build-From-Source)** — prerequisites, `dotnet` + `npm` flow, where the dist lands, debugging.
- **[Architecture](Architecture)** — projects in the solution, the data flow from a tracking source out to OSC, where v1 and v2 SDKs diverge.
- **[Registry](Registry)** — what the module registry is, how to point the host at a different one, payload shape.

## Install

See the README — the wiki assumes you already have the host built or installed.
