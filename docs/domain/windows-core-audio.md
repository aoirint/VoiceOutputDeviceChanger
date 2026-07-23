# Windows Core Audio Contract

## Endpoint identity

The configuration stores an `IMMDevice` endpoint ID, not its friendly name.
Friendly names are display-only, may collide, and may change.
An empty ID means `IMMDeviceEnumerator.GetDefaultAudioEndpoint` for render flow and the multimedia role.

Enumeration includes only endpoints in the active state.
If a configured ID is absent, the UI reports that condition and the renderer
temporarily uses the current multimedia default without overwriting the saved
missing ID.
The next user click moves to the default selection.

## Render format

The renderer requests two-channel 32-bit IEEE float samples at `AudioSettings.outputSampleRate`.
It opens the endpoint in shared, event-driven mode with Windows format conversion and source-quality conversion enabled.
This choice preserves Unity callback values without an intermediate PCM16
conversion and lets the Windows audio engine adapt to the endpoint mix format.

The format choice is a mod contract, not a claim that every physical endpoint runs natively in stereo float at Unity's rate.
Successful conversion on each target device remains a runtime check.

## COM and thread ownership

- Endpoint enumeration initializes COM for the calling thread and releases
  every device, collection, property store, and enumerator before returning.
- Cleanup retains the first reported failure while continuing to release the
  remaining COM objects and property values.
- Rendering initializes COM on one background thread and creates, starts,
  renders, stops, resets, and releases its WASAPI objects on that thread.
- A changed endpoint generation ends the current renderer before opening the replacement.
- Initialization transfers ownership only after all interfaces and the event
  handle are acquired. A failed `Start` disposes the partial renderer.
- Shutdown signals the worker and waits up to five seconds. It never uses `Thread.Abort`.

The property variant reader accepts only `VT_LPWSTR` for the friendly-name property and always calls `PropVariantClear`.

All declared MMDevice, property-store, audio-client, and render-client IIDs were
compared with the Windows SDK 10.0.26100.0 headers.
The verification harness enumerated the multimedia default and active
endpoints, opened a shared event-driven client, rendered one silent buffer,
and disposed it successfully on 2026-07-22.
This verifies the interop path on that machine; it does not replace the target-hardware matrix.

## Failure policy

Alternate routing becomes active only after `IAudioClient.Start` succeeds.
Until then, the Unity audio callback is left unchanged.
Endpoint-enumeration failure suspends the current renderer and clears queued
audio until a later successful selection resumes routing.
If rendering later fails, readiness is cleared and queued samples are
discarded. Later callbacks remain on Unity output while the worker retries
after a bounded delay.

This fail-open policy prioritizes audible communication over strict output separation.
It can duplicate or drop one callback block at a routing transition, but it
should not create sustained silence or replay older queued voice after recovery.
