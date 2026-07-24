# Development Operations

## Prerequisites

- Windows PowerShell.
- The SDK selected by `global.json` (`10.0.201`, compatible feature-band roll-forward).
- Network access to the mapped sources in `nuget.config` for the first restore.

Game code and assets are research inputs, not build inputs.
The solution must restore and compile without a local Lethal Company installation.

## Reproduce the build

Run from the repository root:

```powershell
dotnet restore VoiceOutputDeviceChanger.slnx --locked-mode
dotnet format VoiceOutputDeviceChanger.slnx --no-restore --verify-no-changes
dotnet build VoiceOutputDeviceChanger.slnx --no-restore -c Debug /p:BepInExPluginVersion=0.0.0
dotnet run --project VoiceOutputDeviceChanger.Tests --no-build -c Debug -- VoiceOutputDeviceChanger/bin/Debug/netstandard2.1/VoiceOutputDeviceChanger.dll 0.1.0-alpha.1 0.1.0-alpha.1
dotnet build VoiceOutputDeviceChanger.slnx --no-restore -c Release /p:BepInExPluginVersion=0.0.0
dotnet run --project VoiceOutputDeviceChanger.Tests --no-build -c Release -- VoiceOutputDeviceChanger/bin/Release/netstandard2.1/VoiceOutputDeviceChanger.dll 0.1.0-alpha.1 0.1.0-alpha.1
```

The test project is a console harness rather than a test-framework package.
Core queue and mixer cases are deterministic; by default on Windows, the same
harness also exercises live Core Audio endpoint enumeration and WASAPI
rendering. It validates the built plugin metadata and every archive-contract
mutation fixture in the same run.
Success prints both audio and package-contract completion messages and returns
exit code zero.

GitHub's hosted Windows runner has no active render endpoint.
The shared `test-source` gate therefore runs the same harness with
`--skip-live-audio` for deterministic cases in both entry workflows and records
the hardware-dependent probe as skipped.
Local release validation must run without that argument on a Windows system with an active endpoint.

## Dependency checks

Review the lockfile whenever a package declaration changes, then query the configured feed:

```powershell
dotnet list VoiceOutputDeviceChanger.slnx package --vulnerable --include-transitive --source https://nuget.bepinex.dev/v3/index.json --source https://api.nuget.org/v3/index.json
dotnet list VoiceOutputDeviceChanger.slnx package --deprecated --include-transitive --source https://nuget.bepinex.dev/v3/index.json --source https://api.nuget.org/v3/index.json
```

New versions require source and publisher review, a publication date at least
seven days old, exact pinning, a regenerated lockfile, runtime-behavior review,
and the full verification matrix.

## Restore Agent Skills

Use APM CLI 0.26.0 from the repository root:

```powershell
apm install --frozen
apm audit --ci
```

The manifest and lockfile select public Skill dependencies by full commit SHA.
Do not edit `.agents/skills/` directly.

## Inspect plugin metadata

The built DLL must contain exactly one plugin entry point with:

- GUID `com.aoirint.voiceoutputdevicechanger`;
- name `Voice Output Device Changer`;
- version matching the project version for stable builds, or `0.0.0` for
  prerelease builds whose SemVer identity cannot be parsed by BepInEx 5;
- process filter `Lethal Company.exe`.

Use Mono.Cecil from the reviewed locked NuGet cache to inspect custom attributes without loading game dependencies.
The final implementation verification records this inspection. Do not use
`Assembly.LoadFrom` as a substitute because it executes loader resolution
behavior.

## Debugging

Start with a clean BepInEx profile containing only BepInEx and the built mod
DLL. Reproduce the issue once, exit the game, and inspect
`BepInEx/LogOutput.log` for the plugin GUID
`com.aoirint.voiceoutputdevicechanger`.

Before sharing logs, remove personal paths, Windows endpoint IDs, account or
lobby identifiers, and unrelated plugin output. Keep the first initialization
failure or callback error and enough surrounding lines to establish ordering.
Do not enable broad reflection, COM, or per-audio-buffer logging: those paths
can expose machine-specific identifiers or disrupt the audio callback.

Use the deterministic test harness first for queue, channel conversion, mixer,
and lifecycle failures. Use the hardware-dependent run without
`--skip-live-audio` only for endpoint enumeration, WASAPI startup, or device
loss behavior. Gameplay hook, UI, and source-reuse failures require the
clean-profile checks in
[release operations](release.md#stable-release-runtime-checks).

## Documentation checks

CI runs `markdownlint-cli2` through a full-SHA-pinned action against all
committed Markdown using `.markdownlint-cli2.yaml`. With pnpm 11 or newer,
reproduce the same rules using the reviewed package version and fail-closed
cooldown settings:

```powershell
pnpm --config.minimumReleaseAge=10080 --config.minimumReleaseAgeStrict=true --config.minimumReleaseAgeIgnoreMissingTime=false --config.minimumReleaseAgeExclude= dlx markdownlint-cli2@0.22.0 --config .markdownlint-cli2.yaml "**/*.md"
```

Review repository-relative links and user-facing prose separately from
developer architecture and compatibility claims.

## Workflow checks

Workflow and Composite Action changes require ShellCheck 0.11.0, actionlint
1.7.12, and pinact 3.9.2 or later with the repository's pinned-action policy.
CI itself uses the reviewed 3.9.2 archive:

```powershell
actionlint -color -pyflakes=
pinact run --check --min-age 7
```

Run ShellCheck first for any standalone shell files.
`actionlint` then invokes the installed ShellCheck for inline Bash.

## Install for local runtime testing

Copy only:

```text
VoiceOutputDeviceChanger/bin/Release/netstandard2.1/VoiceOutputDeviceChanger.dll
```

to:

```text
Lethal Company/BepInEx/plugins/VoiceOutputDeviceChanger/VoiceOutputDeviceChanger.dll
```

Use a separate clean BepInEx profile for the first run.
Do not copy `BepInEx.dll`, Harmony, Unity reference assemblies, `.deps.json`, `.pdb`, or any game-derived file.
