# Release Operations

## Scope

The event-owned Pull Request and Main workflows, stable GitHub Releases, and
stable Thunderstore publication are enabled. APM manages repository-local
Agent Skills and is not a mod-package host.

The project version is currently `0.0.0`. The Main workflow therefore creates
a commit-specific edge artifact and skips the stable release job. A nonzero
three-part version selects the stable path after matching developer and package
changelog entries are present.

Release classification, package staging, deterministic ZIP creation, and
checksum generation belong to `.github/workflows/main.yml`. Archive-contract
and assembly-identity validation belongs to the test project. There is no
standalone package executable or separate runtime-approval data format.

## Release inputs

| Fact | Status | Basis or effect |
| --- | --- | --- |
| Game build and platform | Confirmed | [Supported game build](../domain/lethal-company-voice-playback.md) on Windows |
| Loader baseline | Partly blocked | BepInEx.Core 5.4.21 compile contract is locked; the exact installed BepInEx 5 runtime remains unverified |
| Plugin identity | Confirmed | Project assembly and generated GUID/name/version; built custom attributes are package-validated |
| Game hooks and timing | Confirmed statically | The game-domain and architecture documents cover the selected callbacks; multiplayer behavior remains unverified |
| Package hosts | Confirmed | GitHub Releases and Thunderstore for stable versions |
| Release classification | Confirmed | `0.0.0` is edge; a nonzero three-part version with a matching changelog entry is stable |
| Automation use | Confirmed | GitHub Actions, Releases, Thunderstore, and APM-managed Agent Skills |
| Repository settings | Required outside source | Verify the read-only default token, full-SHA Action policy, protected required checks, immutable releases, and private vulnerability reporting on the active repository |

The current runtime gaps do not block edge artifact verification. They must be
closed before a maintainer changes the project to a stable version.

## Archive contract

This repository uses host-neutral archive contract version 1. The ZIP root
contains exactly:

```text
VoiceOutputDeviceChanger.dll
manifest.json
icon.png
README.md
CHANGELOG.md
LICENSE
```

All entries must be regular root files with safe names. The validator rejects
absolute paths, traversal, backslashes, unsafe link types, duplicates,
unexpected files, additional DLLs, excessive individual or aggregate expansion,
extreme compression ratios, an empty license, and incomplete package-facing
documentation.

The DLL must be a valid managed assembly named `VoiceOutputDeviceChanger` with
exactly one matching `BepInPlugin` and `BepInProcess` attribute. The GUID,
name, project version, assembly version, and `Lethal Company.exe` process
restriction must agree. Mutation fixtures invoke this same validator for every
documented rejection branch.

## Edge artifacts

Run the local build and test commands in
[development operations](development.md). A push to `main` then:

1. re-runs the shared lint and test gates on the integrated commit;
2. classifies `0.0.0` as `0.0.0-edge.<UTC timestamp>.<commit>`;
3. stages only the six allowed files and creates the ZIP twice;
4. requires both ZIPs to have the same SHA-256;
5. validates the completed ZIP with the test project; and
6. uploads the ZIP and adjacent checksum as a short-lived GitHub artifact.

Packaging is CI-owned, so no repository-local command is presented as a
second production packager. To inspect an edge artifact, download it from the
successful Main workflow and verify its adjacent checksum before extraction.

## Pre-release runtime checks

Before changing the project version from `0.0.0`, use a clean Windows BepInEx
5 profile with no other mods and confirm:

- the plugin loads in the target game build without reflection or patch errors;
- both settings screens show one usable remote-output selector;
- a remote player's processed voice moves to the selected endpoint while game
  audio and the local player remain on Unity output;
- missing, disconnected, and restored endpoints fall back and recover without
  muting remote voice or crashing; and
- shutdown completes without a render-thread timeout.

Repeat the voice-separation check once as host and once as a non-host client.
Record failures with enough context to reproduce them, but do not establish a
repository-specific result schema merely to approve a release.

## Prepare a stable release

1. Complete the pre-release runtime checks for the exact commit.
2. Add a versioned changelog section with the release version and UTC date.
3. Add the same version to `assets/CHANGELOG.md`.
4. Set `Version` in `VoiceOutputDeviceChanger/VoiceOutputDeviceChanger.csproj`
   to the same nonzero three-part version.
5. Confirm the `THUNDERSTORE_TOKEN` repository secret and package namespace,
   community, and categories.
6. Run every development, workflow, package-contract, and documentation check.
7. Push the reviewed commit to `main`.

CI refuses an invalid version, a missing versioned changelog section, a
nondeterministic or invalid archive, an incomplete release identity, and a
published release that does not exactly match the integrated commit, ZIP,
checksum, and immutable-release contract. Only the validated build artifact
and checksum are attached to GitHub; the same ZIP is submitted to Thunderstore.
The release job never rebuilds it.

## Recovery

Validation failures create no release. If GitHub fails after draft creation,
inspect the draft and workflow logs. Delete an incomplete draft and its
workflow-created tag only after verifying that it was never published and
contains no reusable release asset, then rerun from the unchanged commit.
Never replace an existing published tag, release, ZIP, or checksum.

If GitHub publication succeeds but Thunderstore submission fails, first confirm
whether the package version is already present on Thunderstore. When it is not,
rerun the failed Main workflow for the unchanged commit. The release job accepts
an existing GitHub release only after its tag target, immutable state, asset
names, and asset digests exactly match the validated artifact; it then retries
the same reviewed ZIP without replacing or rebuilding the GitHub assets.

Update this document whenever version classification, archive content,
workflow permissions, release calls, runtime expectations, or package-host
scope changes.
