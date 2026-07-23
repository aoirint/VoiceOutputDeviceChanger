# Release Pipeline

This design depends on [GitHub automation dependencies](../domain/github-automation.md)
and the [release operations contract](../operations/release.md).

## Version model

`VoiceOutputDeviceChanger/VoiceOutputDeviceChanger.csproj` is the source of the
SemVer release identity.
The generated assembly version, BepInEx metadata, artifact name, and Git tag are
derived from it.

- `0.0.0` is always an edge build. Its artifact version adds `edge`, the UTC
  build timestamp, and the first seven commit-SHA characters, but the
  loader-facing version remains `0.0.0`.
- A SemVer prerelease such as `0.1.0-alpha.1` creates a GitHub prerelease.
  Its assembly version uses the numeric core, while BepInEx metadata and the
  Thunderstore manifest remain `0.0.0` because neither consumer accepts this
  prerelease identity.
- A nonzero numeric version is stable only when the developer and package
  changelogs contain that exact version and the maintainer has completed the
  documented stable release checks.

The classifier fails before packaging when the version syntax or required
changelog entry is invalid.

## Artifact flow

The `Pull Request` workflow owns `pull_request` and `merge_group` events.
It runs the repository-owned `lint-source` gate on Linux and the `test-source`
gate on Windows without any publishing permission.

The `Main` workflow owns only pushes to `main`.
It re-runs those same `lint` and `test` jobs on the exact integrated commit.
A read-only `plan` job resolves one release identity through the production
classifier. The `build` job depends directly on `lint`, `test`, and `plan`, so a
failed or skipped gate cannot be bypassed through cross-workflow polling.

The Linux build job restores the locked graph and builds the release DLL.
The separate Windows job smoke-tests the Windows-targeted test executable while
skipping its hardware-dependent endpoint and WASAPI probe.
That output then follows this path:

1. The package mutation suite exercises the production validator in the test project.
2. The workflow stages the DLL, manifest, icon, package README, package
   changelog, and license, then creates the ZIP with the runner-provided `7z`.
3. The workflow writes `SHA256SUMS` for the ZIP.
4. The validator inspects archive paths, entry types, content, and managed custom attributes.
5. The build job verifies the ZIP against `SHA256SUMS`.
6. GitHub artifact storage carries those exact two files to the release job.
7. The release job requires exactly one ZIP and checksum, then verifies the
   internal handoff before publishing only the ZIP.
8. For a prerelease, the pinned release action creates an immutable GitHub
   prerelease for the integrated commit and attaches only the ZIP.
9. Stable GitHub and Thunderstore publication remain disabled until the
   maintainer separately authorizes them after runtime validation.

The release job never rebuilds the DLL.
An existing release for the same tag causes publication to fail instead of
replacing its assets.

## Failure boundaries

Lint, build, test, classification, packaging, or checksum failure prevents artifact upload or publication.
Edge and stable builds currently skip the release job.
Every integrated edge build still uploads a versioned artifact and records the
source commit, download URL, and GitHub artifact digest in the workflow summary.
GitHub prerelease publication creates a draft, attaches the ZIP, and only then
publishes it. The checksum remains inside the workflow artifact.
If an upload or publish call fails, the GitHub prerelease or draft can remain
for maintainer inspection; recovery is documented in
[release operations](../operations/release.md).
