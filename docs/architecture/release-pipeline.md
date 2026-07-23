# Release Pipeline

This design depends on [GitHub automation dependencies](../domain/github-automation.md)
and the [release operations contract](../operations/release.md).

## Version model

`VoiceOutputDeviceChanger/VoiceOutputDeviceChanger.csproj` is the source of the three-part numeric plugin version.
The generated BepInEx metadata and assembly version are derived from it.

- `0.0.0` is always an edge build. Its artifact version adds `edge`, the UTC
  build timestamp, and the first seven commit-SHA characters, but the
  loader-facing version remains `0.0.0`.
- A nonzero numeric version is stable only when the developer and package
  changelogs contain that exact version and the maintainer has completed the
  documented pre-release checks.
- Prerelease strings are rejected because BepInEx 5 parses loader metadata as `System.Version`.

The classifier fails before packaging when the version syntax or stable changelog entry is invalid.

## Artifact flow

The `Pull Request` workflow owns `pull_request` and `merge_group` events.
It runs the repository-owned `lint-source` gate on Linux and the `test-source`
gate on Windows without any publishing permission.

The `Main` workflow owns only pushes to `main`.
It re-runs those same `lint` and `test` jobs on the exact integrated commit.
A read-only `plan` job resolves one release identity through the production
classifier. The `build` job depends directly on `lint`, `test`, and `plan`, so a
failed or skipped gate cannot be bypassed through cross-workflow polling.

The Windows build job restores the locked graph and builds the release DLL.
That output then follows this path:

1. The package mutation suite exercises the production validator in the test project.
2. The workflow stages the DLL, manifest, icon, package README, package
   changelog, and license twice and requires byte-identical ZIPs.
3. The workflow writes an adjacent SHA-256 file.
4. The validator inspects archive paths, entry types, content, and managed custom attributes.
5. GitHub artifact storage carries those exact two files to the release job.
6. The release job requires exactly one ZIP and checksum, then verifies the digest before publishing.
7. The published GitHub release record is checked for its tag, target commit,
   immutable stable state, exact assets, and ZIP digest.
8. The same verified ZIP is submitted to Thunderstore for stable versions.

The release job never rebuilds the DLL.
An incomplete or mismatched existing tag or release causes failure, and asset
upload does not use replacement mode. An exact immutable release from the same
commit may be reused only to recover the remaining Thunderstore publication.

## Failure boundaries

Lint, build, test, classification, packaging, or checksum failure prevents artifact upload or publication.
Edge builds skip the release job.
Every integrated edge build still uploads a versioned artifact and records the
source commit, download URL, and GitHub artifact digest in the workflow summary.
Stable GitHub publication creates a draft, attaches both assets, and only then
publishes it. Thunderstore receives the already-verified ZIP afterward. If an
upload or publish call fails, the GitHub release or draft can remain for
maintainer inspection; recovery is documented in
[release operations](../operations/release.md).
