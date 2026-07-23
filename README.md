# Voice Output Device Changer

Voice Output Device Changer is a client-side BepInEx 5 Mono mod for Lethal
Company on Windows. It sends voices from players other than the local player
to a separately selected Windows audio output device. Music, effects, the
local microphone sidetone, and other game audio remain on Unity's normal
output.

The implementation and build-time verification are complete.
An in-game two-player validation run is still required before publishing a stable release.

## Requirements

- The supported Lethal Company build for Windows, documented under
  [game voice playback](docs/domain/lethal-company-voice-playback.md).
- BepInEx 5.x Mono installed in the game directory.
- A Windows audio render endpoint available through Core Audio.

This mod is client-side and does not send network messages or require
installation by the host or other players.
Other game versions are not currently supported.

## Install

1. Build the Release configuration as described under [Build](#build).
2. Copy
   `VoiceOutputDeviceChanger/bin/Release/netstandard2.1/VoiceOutputDeviceChanger.dll`
   to `Lethal Company/BepInEx/plugins/VoiceOutputDeviceChanger/`.
3. Start the game.

Only the mod DLL is installed.
Do not copy build-time BepInEx, Harmony, Unity, or game assemblies from the NuGet cache or game files.

## Configure

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
dotnet build VoiceOutputDeviceChanger.slnx --no-restore -c Release
dotnet run --project VoiceOutputDeviceChanger.Tests --no-build -c Release -- VoiceOutputDeviceChanger/bin/Release/netstandard2.1/VoiceOutputDeviceChanger.dll 0.0.0 0.0.0
```

To create the same validated edge package produced by CI, follow [release operations](docs/operations/release.md).
Every `main` build uploads a short-lived package artifact.
Project version `0.0.0` is always classified as an edge build and never creates a GitHub Release.

A nonzero three-part project version with matching developer and package
changelog sections is eligible for stable GitHub and Thunderstore releases.
Maintainers must complete the runtime checks in
[release operations](docs/operations/release.md) before raising the project
version from `0.0.0`.

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
