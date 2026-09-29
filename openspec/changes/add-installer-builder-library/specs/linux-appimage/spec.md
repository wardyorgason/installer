# Spec Delta

## Purpose

Produces a single-file Linux AppImage from the payload, built inside a container with pinned tooling so any host with a Docker engine, including the Mac agent, can produce it reproducibly.

## ADDED Requirements

### Requirement: AppImage output
A Linux target SHALL produce `<name>-<displayVersion>-<target-name>.AppImage`, an executable file for the target's architecture (x86-64 for `x64`, AArch64 for `arm64`) that starts the main executable, passing its command-line arguments through.

#### Scenario: AppImage runs the app
- **WHEN** the produced AppImage is run on a Linux system of the target architecture with arguments `--help`
- **THEN** the payload's main executable starts and receives `--help`

#### Scenario: arm64 target built on an x86-64 host
- **WHEN** a Linux arm64 target is built on an x86-64 host
- **THEN** the produced AppImage is an AArch64 AppImage

### Requirement: Desktop integration metadata
The AppImage SHALL contain a desktop entry named after the app's `id`, with the app's `name`, `description` as comment, the icon, `linux.categories` (default `Utility`) and `displayVersion` as the AppImage version, plus the app icon as a PNG.

#### Scenario: Desktop entry contents
- **WHEN** the AppImage for app id `dev.screenrec.app` with categories `["AudioVideo"]` is extracted
- **THEN** it contains `dev.screenrec.app.desktop` with `Name=ScreenRec`, `Categories=AudioVideo;` and the display version, and a PNG icon

### Requirement: Containerized build
The AppImage SHALL always be built inside a container, using a Docker-compatible engine on the build host. The builder SHALL use the container definition shipped with the builder, and SHALL build its image automatically when that image is not present locally. The image's identity SHALL change whenever the container definition changes, so an outdated image is never reused. The build SHALL NOT depend on the engine being able to mount host directories.

#### Scenario: First build on a host
- **WHEN** the image for the current container definition is not present
- **THEN** the builder builds it and then builds the AppImage

#### Scenario: Later builds reuse the image
- **WHEN** the image for the current container definition is already present
- **THEN** the builder uses it without rebuilding

#### Scenario: Docker not available
- **WHEN** no Docker engine is reachable
- **THEN** the Linux target fails preflight with an error that a running Docker engine is required, and other targets are still built

### Requirement: Pinned tooling
The container definition MUST pin the AppImage tooling and the AppImage runtime for each supported architecture to fixed versions, and MUST verify each download against a checksum while the image is built. Building an AppImage SHALL NOT download anything.

#### Scenario: Checksum mismatch
- **WHEN** a pinned download's checksum does not match while the image is built
- **THEN** the image build fails and the Linux target fails with the container build's error output

#### Scenario: Offline AppImage build
- **WHEN** the image is already present and the host has no network access
- **THEN** the AppImage is still built
