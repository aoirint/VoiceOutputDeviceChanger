# Source Layout

This document defines the ownership boundary expressed by the repository tree.
It assumes the external constraints recorded in the
[dependency baseline](../domain/dependencies.md),
[Lethal Company voice playback](../domain/lethal-company-voice-playback.md),
and [Windows Core Audio notes](../domain/windows-core-audio.md).

## Project map

```text
VoiceOutputDeviceChanger/
  Core/
  Interop/
    Game/
    Settings/
    WindowsAudio/
  Plugin.cs
  VoiceOutputDeviceChanger.csproj
VoiceOutputDeviceChanger.Tests/
VoiceOutputDeviceChanger.slnx
assets/
global.json
.github/
docs/
```

`VoiceOutputDeviceChanger/` is the only runtime project and produces the one
DLL installed into BepInEx. `Plugin.cs` is its composition root: it owns loader
lifecycle, configuration wiring, and bounded Harmony registration.

`Core/` contains the framework-independent audio queue, stream, mixing, and
activation-identity policy. Code in this directory does not depend on BepInEx,
Harmony, Unity, reflection, COM, or Windows endpoint APIs.

`Interop/` owns concrete adapters and framework callbacks:

- `Game/` owns Harmony callbacks, Unity components, reflection targets, and
  settings-screen injection;
- `Settings/` owns the BepInEx configuration adapter and endpoint-selection
  coordination; and
- `WindowsAudio/` owns Core Audio discovery, COM declarations, WASAPI
  rendering, and the render-thread adapter.

Dependencies point from the composition root and Interop toward Core. Core
does not reach back into Interop or access game singletons.

`VoiceOutputDeviceChanger.Tests/` is a Windows console test harness. It links
the framework-independent and Windows-audio production sources needed for
deterministic and hardware-dependent checks. It also owns the host-neutral ZIP
contract, managed-assembly inspection, and archive mutation fixtures. Test code
is never copied into the player package.

Each project file declares its target framework, language version, analyzer
policy, and locked-restore behavior. `global.json` selects the SDK policy used
by local checks and CI. The compact solution file is the canonical restore,
format, and build entry point.

## Automation ownership

`.github/` owns source validation, release classification, package staging,
ZIP creation, checksum generation, artifact handoff, and stable GitHub and
Thunderstore publication. Packaging is intentionally not a third .NET project.
The test project validates both mutation fixtures and the completed CI-created
archive.

`assets/` owns the package manifest, user-facing README and changelog, editable
icon source, and rendered icon. CI stages these files with the built DLL and
root license.

`docs/` owns domain knowledge, architecture, and maintainer procedures. Build
outputs and CI artifacts are generated state and remain outside the committed
source layout.
