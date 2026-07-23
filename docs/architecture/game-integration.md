# Game Integration Architecture

This design assumes the base-game behavior documented under
[Lethal Company voice playback](../domain/lethal-company-voice-playback.md).

## Compatibility boundary

The mod compiles without `Assembly-CSharp.dll`.
At startup, `GameReflection` resolves the documented integration targets by name.
Missing members fail plugin initialization as one guarded transaction instead of producing partially active patches.
Changing the supported game version requires reconfirming that member set
before updating the compatibility claim.

## Player and network role policy

The patch is a postfix on the game's playback refresh.
It makes no RPC, network-variable, ownership, host, or server-state change.
The operation fails closed until `StartOfRound.localPlayerController` exists,
which identifies an initialized local client context.

For each player, the integration mirrors the game's eligibility rule: controlled or dead.
It excludes the exact local Unity object and registers the assigned `AudioSource` for every remaining eligible player.
After building that allowlist, it deactivates any mod capture component on a
pooled source no longer assigned to an eligible remote player.
This cleanup prevents a source reused for the local player from retaining remote routing.

## Settings UI

Postfixes on the two menu `Start` methods search below that menu root for the existing `ChooseDevice` microphone option.
The launch-only `MenuManager` instance is skipped through `isInitScene`.

For each settings surface, the injector:

1. checks for an existing mod clone to remain idempotent;
2. clones the microphone option under the same parent;
3. assigns the clone a unique name and `SettingsOptionType.DontTell`;
4. moves its anchored position down by 52 units;
5. adds `VoiceOutputDeviceOption` and binds the cloned text element;
6. restores the original active state.

The cloned button's serialized listener still calls `SettingsOption.SetSettingsOptionInt`.
A prefix recognizes only objects carrying `VoiceOutputDeviceOption`, cycles
the endpoint, updates the label, and skips the game-owned method.
All unmarked settings options execute normally.

## Callback containment

Harmony postfixes and the clone prefix enter `IntegrationContext.RunGuarded`.
The callback body and diagnostic sink have separate exception boundaries, so a
logging failure cannot rethrow into a game method.
The Unity audio callback has its own allocation-free boolean path and performs no logging.

This containment does not claim compatibility with arbitrary third-party transpilers or source replacements.
Patch interaction remains part of the stable release runtime checks.
