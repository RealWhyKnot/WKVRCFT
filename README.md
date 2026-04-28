# WKVRCFT

A friendly fork of [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking). WKVRCFT is a Windows host that bridges face- and eye-tracking hardware to VRChat's OSC interface, with a focus on **hot-pluggable third-party modules** — each module runs in its own sandboxed process, declares its own settings UI, and can be installed without restarting the host. Drives the same eye and expression parameters VRChat already understands.

## Quick start

- **Releases:** _coming soon_ — pre-built artifacts will land on the [Releases page](https://github.com/RealWhyKnot/WKVRCFT/releases) once the first tagged build is cut.
- **Build from source:** see [Build From Source](https://github.com/RealWhyKnot/WKVRCFT/wiki/Build-From-Source) on the wiki.

After install, point an avatar at the [VRCFaceTracking parameter list](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/) — the wire format hasn't changed.

## Modules

Tracking sources ship as separate DLLs that the host loads at runtime. Default install bundles **no** modules — you install the ones you want from the [module registry](https://github.com/RealWhyKnot/WKVRCFT/wiki/Registry) (or point the host at any registry you like). The microphone-driven emulation modules (audio-to-expression, behavioural eye synthesis) live in [WKVRCFT-Emulation](https://github.com/RealWhyKnot/WKVRCFT-Emulation).

Writing a module — the `IModuleContext` API, declarative config schemas, manifest fields, the live-config event — is documented on the [Module Authors](https://github.com/RealWhyKnot/WKVRCFT/wiki/Module-Authors) wiki page.

For an overview of how the pieces fit together, see [Architecture](https://github.com/RealWhyKnot/WKVRCFT/wiki/Architecture).

## Credits

This project is a fork of [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking) by [benaclejames](https://github.com/benaclejames). The original tool is Apache-2.0–licensed; the same license carries through here. We've kept the upstream remote configured so improvements there can be pulled in over time.

Many thanks to the VRCFaceTracking community and contributors whose work made this fork possible.

## License

Apache License 2.0 — see [LICENSE](LICENSE).
