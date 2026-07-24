# Voice Output Device Changer

Voice Output Device Changer sends voices from players other than the local
player to a separately selected Windows audio output device. Music, effects,
and other game audio remain on the normal game output.

## Compatibility

- Lethal Company v81 (2026-04-17 UTC, Manifest ID:
  `6423525044216269478`)
    - Package dependency
        - [BepInExPack][bepinexpack-package] v5.4.2305 (2026-03-17 UTC)
- Windows

## What it does

- Adds a `REMOTE VOICE OUTPUT` option to the normal game settings.
- Sends remote player voices to the selected active Windows output device.
- Keeps music, effects, and other game audio on the normal game output.

## Who needs to install

Install the mod only on clients that want to send remote player voices to a
different output device.

## Configuration

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `General.RemoteVoiceOutputEndpointId` | `string` | Empty | Windows endpoint ID for remote player voices. Empty uses the current multimedia default. |

Open the normal settings screen from the main menu or in-game quick menu, then
use `REMOTE VOICE OUTPUT` to select the Windows multimedia default or another
active output device.

If a saved endpoint is missing, remote voices temporarily use the current
Windows multimedia default. If alternate routing fails, remote voices remain
on the normal game output.

## AI Disclosure

Some parts of this project were developed with AI tools based on large language
models, including agent-based tools. The project maintainer reviews the code.
This disclosure is made in compliance with Thunderstore and community policies.

[bepinexpack-package]: https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/
