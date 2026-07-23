# GitHub Automation Dependencies

## Scope

This document owns the externally versioned Actions and command-line tools executed by CI.
The event-owned Pull Request and Main workflows pin every third-party Action to
a full commit SHA and every downloaded executable archive to a version and
SHA-256 pair.
The values were reviewed on 2026-07-23 and were older than the seven-day adoption cooldown.

## GitHub Actions

| Action | Version | Commit SHA | Reviewed commit date |
| --- | --- | --- | --- |
| `actions/checkout` | 6.0.2 | `de0fac2e4500dabe0009e67214ff5f5447ce83dd` | 2026-01-09 |
| `actions/setup-dotnet` | 5.2.0 | `c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7` | 2026-02-26 |
| `actions/upload-artifact` | 7.0.1 | `043fb46d1a93c77aae656e7c1c64a875d1fc6a0a` | 2026-04-10 |
| `actions/download-artifact` | 8.0.1 | `3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c` | 2026-03-11 |
| `ncipollo/release-action` | 1.21.0 | `339a81892b84b4eeb0f6e744e4574d79d0d9b8dd` | 2026-03-14 |
| `DavidAnson/markdownlint-cli2-action` | 23.0.0 | `ce4853d43830c74c1753b39f3cf40f71c2031eb9` | 2026-03-26 |

The Markdown Action contains `markdownlint-cli2` 0.22.0 and is MIT licensed.
The official GitHub Actions are used only for checkout, SDK setup with its
locked-graph NuGet cache, and artifact transfer. The third-party
`ncipollo/release-action` owns immutable GitHub release publication.

## Runner selection

Lint and planning complete with substantial headroom below the 15-minute limit.
Release publication handles one bounded artifact through standard shell tools
and API actions. These jobs need no native compatibility contract, privileged
service, or full-VM toolchain, so they use `ubuntu-slim` with explicit timeouts.

Build and package validation intentionally follow the repository family's
moving stable `ubuntu-latest` full-VM image because packaging uses
runner-provided Bash, `jq`, `7z`, and checksum tooling. The job needs build
headroom but no Windows runtime and skips live audio. A separate job smoke-tests
the Windows-targeted test executable on the oldest supported GA Windows image
while skipping its hardware-dependent audio probe.

## Downloaded workflow tools

| Tool | Version | Linux archive SHA-256 | Published |
| --- | --- | --- | --- |
| ShellCheck | 0.11.0 | `b7af85e41cc99489dcc21d66c6d5f3685138f06d34651e6d34b42ec6d54fe6f6` | 2025-08-04 |
| actionlint | 1.7.12 | `8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8` | 2026-03-30 |
| pinact | 3.9.2 | `6adcc8a2217e4114e0841f8bca0cddf9958a9c52e3e89760c35b791cdba1a916` | 2026-04-29 |

CI downloads the tool archives into the per-run temporary directory and
verifies each SHA-256 before extraction.
ShellCheck inspects standalone shell files and inline Bash through actionlint.
Actionlint validates workflow structure; pinact rejects floating or too-recent
Action references.

## Permissions and repository policy

Both entry workflows default to `contents: read`.
Only the stable release job receives `contents: write`; pull-request code never receives a publishing credential.
GitHub's default workflow-token permission is read-only.
Repository policy requires full-length Action commit pins and immutable GitHub Releases.
