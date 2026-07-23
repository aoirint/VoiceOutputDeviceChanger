# Agent Instructions

## Agent Skills

Repository-local Agent Skills are deployed to `.agents/skills/` by
[APM](https://github.com/microsoft/apm). Do not edit that generated directory
directly.

## APM-managed Skills

- `apm.yml` pins the selected public
  [aoirint/skills](https://github.com/aoirint/skills); `apm.lock.yaml` records
  their resolved commits and content hashes.
- Keep this unpublished APM project at `version: 0.0.0` until its distribution
  and versioning design is explicitly decided.
- Use APM CLI 0.25.0 for lock operations. It is the newest reviewed release
  that currently satisfies the normal seven-day cooldown; using it is not an
  exception.
- A maintainer may explicitly waive the normal seven-day wait for a directly
  selected current `aoirint/skills` commit. Record the waiver and exact full
  commit SHA in the pull request.
- That waiver applies only to the direct `aoirint/skills` commit selection. It
  does not cover dependencies of `aoirint/skills`; review those dependencies
  and enforce their cooldown independently.
- To restore the committed Skill set, run `apm install --frozen` from the
  repository root, then run `apm audit --ci`.
- Make all Skill changes in the public
  [aoirint/skills](https://github.com/aoirint/skills) repository. This
  repository only selects, pins, and deploys those Skills.
- To update a Skill dependency, review its source, commit pin, license, and
  cooldown first. Update `apm.yml`, remove only the validated project lock,
  regenerate it with APM 0.25.0, then run `apm install --frozen` and
  `apm audit --ci`. Commit the manifest, lockfile, notices, and generated
  `.agents/skills/` changes together.

## Markdown Checks

Use pnpm 11 or newer. Keep the exact package pin and all fail-closed cooldown
settings when reproducing the Markdown check locally:

```shell
pnpm \
  --config.minimumReleaseAge=10080 \
  --config.minimumReleaseAgeStrict=true \
  --config.minimumReleaseAgeIgnoreMissingTime=false \
  --config.minimumReleaseAgeExclude= \
  dlx markdownlint-cli2@0.22.0 \
  --config .markdownlint-cli2.yaml \
  '**/*.md'
```

Add `--fix` after the package version to apply supported automatic fixes, then
run the normal command again. Some rules, including prose line length, still
require a meaning-preserving manual edit.

## Pull Request Merges

- Merge pull requests with squash merge.
- Before confirming the merge, set the squash commit title to
  `<pull request title> (#<number>)`, including the pull request number as in
  GitHub's default squash-merge title.

## Project Directory Structure

- `VoiceOutputDeviceChanger/Plugin.cs` is the BepInEx entry point. Keep startup
  limited to logger setup and integration lifecycle wiring.
- `VoiceOutputDeviceChanger/Core/` owns framework-independent buffering,
  mixing, and capture behavior.
- `VoiceOutputDeviceChanger/Interop/Game/` owns reflection-based game and
  settings integration. Keep base-game assemblies out of build dependencies.
- `VoiceOutputDeviceChanger/Interop/WindowsAudio/` owns Core Audio and WASAPI
  boundaries. Keep endpoint identifiers and COM resources within this layer.
- `VoiceOutputDeviceChanger.Tests/` is the deterministic console test and
  package-contract harness. Hardware-dependent audio checks run only on a
  Windows machine with an active render endpoint.
- `VoiceOutputDeviceChanger.slnx` is the solution entry point.
- `docs/` contains developer documentation.
    - `domain/` contains base-game, platform, dependency, and automation facts
      without product-design decisions.
    - `architecture/` contains product models, workflows, responsibilities,
      and design decisions; it links to the domain knowledge it uses.
    - `operations/` contains reproducible development and release procedures.
- `assets/` contains the Thunderstore-compatible package manifest, README,
  changelog, editable icon source, and rendered icon. Stable builds publish the
  same validated archive to GitHub Releases and Thunderstore.

## Local Prerelease Builds

When building a prerelease DLL for local installation or runtime validation,
pass a BepInEx-compatible plugin metadata version:

```powershell
dotnet build VoiceOutputDeviceChanger.slnx -c Release /p:BepInExPluginVersion=0.0.0
```

BepInEx 5 validates plugin metadata as `System.Version` and rejects SemVer
prerelease suffixes. Keep the project `Version` as the release identity; do not
add a persistent `BepInExPluginVersion` override to the project file.

## Documentation Skill

Use `.agents/skills/software-documentation-maintenance/` when creating,
restructuring, maintaining, or reviewing developer documentation. Use
`.agents/skills/prose-quality-check/` when refining explanatory wording after
the document owner and technical evidence are established.

## Documentation Boundaries

Add base-game or reusable technical knowledge to `docs/domain/`. Add a new
domain document when an architecture document needs knowledge not already
documented there. Add product-specific models, logic, workflows, and design
decisions to `docs/architecture/`; do not duplicate domain knowledge there.
