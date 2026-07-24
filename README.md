# Voice Output Device Changer

**This project is no longer maintained. Maintenance ended without runtime
validation so that a potentially more effective alternative approach can be
evaluated.**

Voice Output Device Changer is a client-side BepInEx 5 Mono mod for Lethal
Company on Windows. It sends voices from players other than the local player
to a separately selected Windows audio output device. Music, effects, the
local microphone sidetone, and other game audio remain on Unity's normal
output.

The implementation and build-time verification were completed.
The planned in-game two-player validation run was not performed before
maintenance ended.

## Compatibility

- Lethal Company v81 (2026-04-17 UTC, Manifest ID:
  `6423525044216269478`)
    - Package dependency
        - [BepInExPack][bepinexpack-package] v5.4.2305 (2026-03-17 UTC)
- Windows with an audio render endpoint available through Core Audio.

This mod is client-side and does not send network messages or require
installation by the host or other players.
Other game versions are not currently supported.
The exact target identifiers and integration evidence are documented under
[game voice playback](docs/domain/lethal-company-voice-playback.md).

## Install

1. Build the Release configuration as described under [Build](#build).
2. Copy
   `VoiceOutputDeviceChanger/bin/Release/netstandard2.1/VoiceOutputDeviceChanger.dll`
   to `Lethal Company/BepInEx/plugins/VoiceOutputDeviceChanger/`.
3. Start the game.

Only the mod DLL is installed.
Do not copy build-time BepInEx, Harmony, Unity, or game assemblies from the NuGet cache or game files.

## Configuration

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `General.RemoteVoiceOutputEndpointId` | `string` | Empty | Windows endpoint ID for remote player voices. Empty uses the current multimedia default. |

Open the normal settings screen from either the main menu or the in-game quick menu.
The mod adds a `REMOTE VOICE OUTPUT` option below the microphone device option.
Select it repeatedly to cycle through the current Windows multimedia default and all active render endpoints.

The selection is stored as a Windows endpoint ID in:

```text
BepInEx/config/com.aoirint.voiceoutputdevicechanger.cfg
```

An empty `RemoteVoiceOutputEndpointId` uses the current Windows multimedia default.
If a saved endpoint is missing, the saved ID is preserved while remote voices
temporarily use the current Windows multimedia default. If the alternate
renderer is unavailable or fails, remote voices remain audible through the
normal Unity output instead of being discarded.

## Build

The repository pins .NET SDK 10.0.201 and all NuGet versions.

```powershell
dotnet restore VoiceOutputDeviceChanger.slnx --locked-mode
dotnet format VoiceOutputDeviceChanger.slnx --no-restore --verify-no-changes
dotnet build VoiceOutputDeviceChanger.slnx --no-restore -c Release /p:BepInExPluginVersion=0.0.0
dotnet run --project VoiceOutputDeviceChanger.Tests --no-build -c Release -- VoiceOutputDeviceChanger/bin/Release/netstandard2.1/VoiceOutputDeviceChanger.dll 0.1.0-alpha.1 0.1.0-alpha.1
```

To create the same validated package produced by CI, follow
[release operations](docs/operations/release.md).
Every `main` build uploads a short-lived package artifact.
The current `0.1.0-alpha.1` version also creates an immutable GitHub prerelease.
BepInEx loader metadata and the Thunderstore manifest remain `0.0.0` for this
GitHub-only alpha because those consumers do not accept the SemVer prerelease
label.

Stable GitHub and Thunderstore publication remain disabled.
Maintainers must complete the runtime checks in release operations before
enabling them.

## Troubleshooting

- `UNAVAILABLE; UNITY OUTPUT ACTIVE`: Windows endpoint enumeration failed.
  Check `BepInEx/LogOutput.log`; remote voices should still use the normal game
  output.
- `MISSING DEVICE; DEFAULT`: the saved endpoint is no longer active. Click the
  option to select the system default or another device.
- No added setting: confirm the supported game version, the executable is
  `Lethal Company.exe`, and BepInEx loaded plugin GUID
  `com.aoirint.voiceoutputdevicechanger`.
- Duplicate or missing voice: remove other voice-routing mods and repeat the
  clean-profile runtime protocol before reporting a compatibility issue.

For maintainer debugging, log inspection, clean-profile isolation, and the
distinction between deterministic and hardware-dependent audio checks, see
[development operations](docs/operations/development.md#debugging).

## Documentation

Developer documentation starts at [docs/README.md](docs/README.md).
Game voice assignment, settings members, and runtime boundaries are documented
under [Lethal Company voice playback](docs/domain/lethal-company-voice-playback.md).

## License

This project is released under the [MIT License](LICENSE).

[bepinexpack-package]: https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/
