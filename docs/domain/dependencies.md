# Dependency Baseline

## Sources and pinning

`nuget.config` clears inherited sources and permits the official BepInEx feed
and nuget.org service index with non-overlapping package-source mapping.
`BepInEx.*` and `UnityEngine.*` resolve from the BepInEx feed.
HarmonyX, Mono.Cecil, MonoMod, `NETStandard.Library.Ref`, Microsoft, and System packages resolve from nuget.org.
Every direct version is an exact range. `packages.lock.json` records the
complete resolved graph with NuGet content hashes.
Release builds use `--locked-mode` and do not update the graph implicitly.

| Direct package | Version | Role | Runtime distribution |
| --- | --- | --- | --- |
| `BepInEx.Core` | 5.4.21 | BepInEx 5 API and transitive HarmonyX compile surface | Installed BepInEx runtime |
| `BepInEx.PluginInfoProps` | 2.1.0 | Generates canonical plugin identity constants | Build-only |
| `BepInEx.Analyzers` | 1.0.8 | Plugin and API diagnostics | Build-only |
| `UnityEngine.Modules` | 2022.3.62 | Unity 2022.3.62 reference assemblies | Game runtime |
| `Mono.Cecil` | 0.11.4 | Package-tool inspection and mutation of managed metadata | Build and validation only |

No game DLL or exported asset is a build dependency or distributable output.
The Release directory contains only `VoiceOutputDeviceChanger.dll` and its project-generated symbols and metadata files.

## Adoption review

The official BepInEx registration feed and nuget.org metadata reported these publication dates during the 2026-07-22 review:

- `BepInEx.Analyzers` 1.0.8: 2022-01-25;
- `BepInEx.Core` 5.4.21: 2022-07-20;
- `BepInEx.PluginInfoProps` 2.1.0: 2022-08-18;
- `UnityEngine.Modules` 2022.3.62: 2025-05-07;
- `Mono.Cecil` 0.11.4: 2021-07-02, MIT license.

All direct packages exceed the seven-day adoption cooldown.
Mono.Cecil was already present in the reviewed locked graph before the test
project made it an exact direct dependency for managed-assembly validation; the
resolved version and content hash did not change.
Their transitive versions are immutable in the lockfile, including HarmonyX 2.7.0 and MonoMod RuntimeDetour 21.12.13.1.
nuget.org records HarmonyX 2.7.0 as published on 2021-12-21 and MonoMod RuntimeDetour 21.12.13.1 on 2021-12-15.
The Linux .NET SDK resolves Microsoft's framework-only
`NETStandard.Library.Ref` 2.1.0 package from nuget.org; the official catalog
records publication on 2019-09-23, no dependencies, a SHA-512 package hash, and
the .NET Core license URL.
The same feed reported no vulnerable or deprecated package for the locked
direct and transitive graph when queried with the .NET SDK.

Registry vulnerability metadata is not a substitute for runtime review.
Runtime behavior is limited to BepInEx loading, Harmony method patching,
MonoMod detours resolved by the installed loader, Unity reference compilation,
and build-time diagnostics or metadata generation.
