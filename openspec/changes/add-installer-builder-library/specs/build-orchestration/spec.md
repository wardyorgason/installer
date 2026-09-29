# Spec Delta

## Purpose

Defines the shared build pipeline behind every format: target selection, preflight checks, payload preparation, the main executable check, working and output directories, per-target failure handling, and the build result.

## ADDED Requirements

### Requirement: Format selection by target OS
Each target's output format SHALL be determined by its `os`: `windows` produces a setup executable, `macos` produces a macOS package, and `linux` produces an AppImage.

#### Scenario: Linux target
- **WHEN** a target has `"os": "linux"`
- **THEN** the builder produces an AppImage for it

### Requirement: Preflight before building
Before building any target, the builder SHALL run preflight checks for every selected target: the host OS the format requires, each external tool the format needs (including its minimum version), a reachable Docker engine for Linux targets, and signing identities and credentials the target's options require. A target that fails preflight MUST be marked failed without being built. Other targets SHALL still be built.

#### Scenario: Missing tool fails only its target
- **WHEN** a manifest has a Windows and a macOS target and `makensis` is not installed
- **THEN** the Windows target fails preflight with an error naming `makensis`, and the macOS target is built

#### Scenario: Preflight errors appear before long work
- **WHEN** the last target in the manifest requires a signing identity that is missing
- **THEN** that error is reported before the first target starts building

#### Scenario: macOS target on a Linux host
- **WHEN** a macOS target is built on a Linux host
- **THEN** the target fails preflight with an error that macOS packages require a macOS host

### Requirement: Tool discovery
The builder SHALL find external tools on `PATH` and additionally in `/opt/homebrew/bin` and `/usr/local/bin`. A missing tool's error SHALL name the tool and how it is usually installed.

#### Scenario: Homebrew tool without Homebrew on PATH
- **WHEN** `makensis` is installed at `/opt/homebrew/bin/makensis` and `PATH` does not include `/opt/homebrew/bin`
- **THEN** preflight finds `makensis`

### Requirement: Payload preparation
The builder SHALL prepare each target's payload in a working copy, never modifying the caller's payload. For a `.zip` payload, when every entry sits under a single top-level directory and there are no files at the zip root, that directory SHALL become the payload root; otherwise the zip root SHALL be the payload root. The builder MUST reject zip entries whose paths would escape the extraction directory. For macOS and Linux targets, the builder SHALL set the executable permission on every native executable (Mach-O or ELF) and every script starting with `#!` in the payload.

#### Scenario: Zip with a single top-level folder
- **WHEN** a zip contains only `ScreenRec/…` entries
- **THEN** the contents of `ScreenRec/` are the payload root

#### Scenario: Zip with files at the root
- **WHEN** a zip contains `App.dll` and `wwwroot/…` at its root
- **THEN** the zip root is the payload root

#### Scenario: Executable permission restored
- **WHEN** a zip made on Windows contains a Mach-O main executable and a Mach-O `createdump` without Unix permissions
- **THEN** both files are executable in the packaged macOS app

#### Scenario: Directory payload left untouched
- **WHEN** the payload is a directory
- **THEN** after the build, the directory's files, permissions and attributes are unchanged

#### Scenario: Path escape
- **WHEN** a zip contains an entry `../outside.txt`
- **THEN** the target fails with an error naming the entry

### Requirement: Main executable platform check
For every profile, the builder SHALL read the main executable's binary header and MUST fail the target when its OS family (Windows PE, macOS Mach-O, Linux ELF) or CPU architecture does not match the target. A macOS universal binary SHALL match when it contains the target's architecture.

#### Scenario: Windows payload given to a macOS target
- **WHEN** a macOS arm64 target's main executable is a Windows PE file
- **THEN** the target fails with an error that the executable is a Windows x64 binary but the target is macOS arm64

#### Scenario: Wrong architecture
- **WHEN** a Linux arm64 target's main executable is an x86-64 ELF file
- **THEN** the target fails with an error naming both architectures

### Requirement: Output and working directories
Artifacts SHALL be written to the output directory, which defaults to `dist` next to the manifest and is created when missing. Artifact file names SHALL be `<name>-<displayVersion>-<target-name>` followed by the format's suffix. Each target SHALL use its own temporary working directory, deleted when the target succeeds and kept when it fails, with its path reported in the target's result.

#### Scenario: Artifact naming
- **WHEN** app `ScreenRec`, display version `1.0.0-b12` and target `macos-arm64` produce a disk image
- **THEN** the artifact is `dist/ScreenRec-1.0.0-b12-macos-arm64.dmg`

#### Scenario: Failed target keeps its working directory
- **WHEN** a target fails during signing
- **THEN** its working directory still exists and its path is in the target's result

#### Scenario: Existing artifact replaced
- **WHEN** an artifact with the same name already exists in the output directory
- **THEN** it is replaced by the new artifact

### Requirement: Independent targets
Targets SHALL be built one at a time in manifest order. A failure in one target MUST NOT stop later targets from being built, and MUST NOT delete artifacts other targets produced.

#### Scenario: Middle target fails
- **WHEN** three targets are built and the second fails
- **THEN** the first and third targets' artifacts are produced and the result reports the second as failed

### Requirement: Build result
The builder SHALL produce a structured result containing a result schema version, an overall success flag, any manifest-level errors, and for each target: its name, OS, architecture, status (`succeeded`, `failed` or `not-selected`), produced artifacts (kind, absolute path, size in bytes and SHA-256), warnings and errors. Each warning and error SHALL carry a stable machine-readable code and a human-readable message.

#### Scenario: Successful target
- **WHEN** a macOS target produces a `.dmg` and a `.zip`
- **THEN** its result entry has status `succeeded` and lists both artifacts with kind, path, size and SHA-256

#### Scenario: Error codes
- **WHEN** a target fails because a tool is missing
- **THEN** its error has a stable code that stays the same across releases, independent of the message text

### Requirement: Signing hook contract
Each format SHALL sign through a signing step that takes one file and signs it in place. The macOS format SHALL always sign. The Windows format SHALL sign only when a signing command is configured. Signing failures MUST fail the target with the signing tool's error output.

#### Scenario: Signing failure
- **WHEN** a signing command exits with a non-zero code
- **THEN** the target fails with an error containing the command's error output
