# Changelog

All notable developer-facing changes to this project are documented in this
file. Package-facing release notes are maintained in `assets/CHANGELOG.md`.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- A client-side BepInEx 5 plugin for the supported Lethal Company build on Windows.
- A settings option in both the main menu and in-game quick menu that cycles active Windows render endpoints.
- Persistent selection by Windows endpoint ID, including system-default and missing-device behavior.
- Automatic reopening when the Windows multimedia default output changes while
  the system-default option is selected.
- Per-player stereo capture queues, multi-player mixing, clipping protection, and event-driven shared-mode WASAPI output.
- Guarded Harmony integration that attaches only to controlled or dead remote
  player voice sources after the game's Dissonance refresh.
- Deterministic core tests for channel conversion, ring wraparound, overflow, concurrent mixing, clamping, and reset behavior.
- Deterministic package generation, semantic BepInEx metadata validation,
  path-safety checks, and branch-reaching mutation fixtures.
- Event-owned GitHub Actions workflows that share source and package gates
  across pull requests, merge queues, and integrated commits.
- Thunderstore-compatible package assets and stable publication using the same
  archive released through GitHub.
- APM-managed repository Skills, contributor governance, ownership, and
  canonical repository policies shared with related mods.
- Domain, architecture, development, and release documentation for the supported game build and platform contracts.
- A pull-request confirmation tied to the repository Contribution License Agreement.

### Security

- Pinned all direct dependencies and the resolved transitive graph with NuGet content hashes.
- Sanitized Windows device names before displaying them through TextMeshPro.
- Kept configured endpoint IDs out of shell, filesystem, network, and diagnostic output paths.
- Bounded package entry size, total expansion, and compression ratios before
  reading archive payloads.

### Changed

- Made project build policy explicit in each project file and retained
  `global.json` as the shared SDK selection policy for local checks and CI.
- Aligned game-domain and related documentation around one compatibility target
  section, product-focused headings, and canonical cross-links.
- Reorganized the solution into plugin and test project directories, with Core
  and concrete Interop responsibilities mirrored by paths and namespaces.
- Moved release classification, deterministic ZIP staging, and checksum
  creation into GitHub Actions. Archive validation and mutation fixtures live
  in the test project instead of a standalone package executable.
- Reused the repository-family version classifier and limited superseded-run
  cancellation to pull-request events so merge-queue checks can complete.
- Made stable release retries reuse an existing immutable GitHub release only
  when its commit and asset digests match the rebuilt validation artifact.
- Replaced the bespoke runtime-approval file and evidence matrix with a focused pre-release runtime checklist.
- Routes all remote sources through a single mixed renderer with deterministic shutdown.
- Made settings-control cloning transactional so failed initialization leaves
  no partial selector that blocks a later retry.
- Made queue clearing win over a concurrently publishing read so reconfiguration
  cannot restore pre-clear audio.
- Bound Unity-buffer clearing to the exact active capture registration and
  suspended alternate routing when endpoint enumeration fails.
- Serialized capture commit with registration retirement so teardown cannot
  invalidate a successful identity check before Unity-buffer clearing.
- Serialized readiness retirement with in-flight submissions and rejected
  stale renderer generations before publishing readiness.
- Continued endpoint COM cleanup after an earlier cleanup operation fails.
- Made WASAPI shutdown release every COM object and wait handle even when stop
  or reset reports a failure.
- Preserved Unity voice output whenever the alternate endpoint is not ready,
  preventing device failure from muting remote players.
- Licensed the project under the MIT License.
- Reserved project version `0.0.0` for non-release edge artifacts while
  retaining the maintainer-owned pre-release checklist for stable publication.
- Made the host-neutral package layout and rejection policy explicit as archive contract version 1.
- Removed the private-repository publication restriction from the agent
  guidance in preparation for public source distribution.
