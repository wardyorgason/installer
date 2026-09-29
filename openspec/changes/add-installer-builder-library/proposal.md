# Proposal

## Why

Each of my projects needs its own scripts to turn build output into something a user can install. screen-rec, for example, has about 170 lines of PowerShell just to build, sign and notarize `ScreenRec.app`, and copies of scripts like that drift apart between projects. A single reusable packaging application would let every project produce installable artifacts for Windows, macOS and Linux the same way, from one manifest, all on the Jenkins Mac agent I already have. It has to work for any kind of codebase, while giving my .NET 10 apps, like screen-rec, direct support with minimal configuration.

## What Changes

- A new packaging application: a C# (`net10.0`) console app that **builds** installer artifacts at build/release time from a **JSON manifest**. It never runs on end-user machines. It is distributed as a framework-dependent build that the build host runs directly (not a `dotnet tool` or NuGet package), so any language or build system (Jenkins shell steps, Make, npm scripts, and so on) can call it. All reusable logic lives in C#, not in shell or PowerShell scripts.
- **One manifest, many targets.** The manifest states the app's shared metadata once (app id, display name, version, publisher, description, icon as PNG, runtime profile) plus a list of **targets**. Values that change per build (such as the version or payload paths) can come from environment variables referenced in the manifest, so it never needs editing per build. Each target names its OS and architecture, its **payload** (the app's built files from any toolchain, as a `.zip` or a folder) and format-specific options. The target OS determines the output format. One run builds every target; each target succeeds or fails on its own, and the result reports each one.
- **Validation before any work.** The app checks the manifest, the host OS, the external tools and signing identity each target needs, and that the version can be expressed in every target platform's version format, and fails with clear errors.
- **Payload preparation.** Zip contents may sit at the zip root or inside a single top-level folder. The app restores the executable permission on every native executable in the payload, which zips made on Windows or with `Compress-Archive` lose.
- **Runtime profiles** add stack-specific inference and defaults without the core assuming any stack. v1 ships two:
  - `generic`: any prebuilt app. The caller names the main executable.
  - `dotnet`: direct support for .NET 10 `dotnet publish` output, framework-dependent or self-contained. It finds the main executable, detects the deployment mode and runtime identifier and fails when they don't match the target, warns when the user's machine will need a .NET runtime, and supplies the macOS entitlements the .NET runtime needs. Single-file and NativeAOT output are not detected in v1; they can use `generic`.
- **Windows (v1):** a per-user setup `.exe` that installs without an admin prompt, adds a Start Menu shortcut, registers in *Installed apps* and includes an uninstaller. Installing a newer version over an older one upgrades in place. It is built with NSIS, which runs on the Mac agent, so no Windows build machine is needed. Authenticode signing is an optional hook; no Windows signer is built in for v1.
- **macOS (v1):** requires a macOS host, and generalizes how screen-rec builds its app today:
  - Builds a `.app` bundle from the payload and manifest, with caller-supplied extra `Info.plist` keys. Payload subfolders are laid out so `codesign` accepts them while the app still finds them at their original relative paths.
  - Always signs with a named keychain identity (e.g., a stable self-signed identity or a `Developer ID Application` identity), with the hardened runtime and entitlements (the profile's defaults plus any the caller adds), then verifies the signature.
  - Optionally notarizes and staples, which requires a Developer ID identity.
  - Outputs a drag-to-install `.dmg`, and optionally a `.zip` of the `.app`.
- **Linux (v1):** an `.AppImage`, a single self-contained file with no install step. It is always built in a Docker container that the app defines and builds itself, with pinned, checksum-verified AppImage tooling for each target architecture, so any host with Docker (including the Mac agent) can build it and nothing is downloaded while building an AppImage.
- **Internally extensible.** Formats, signers and runtime profiles sit behind internal contracts, so new ones (e.g., MSI, `.pkg`, deb, Node or JVM profiles) can be added to the app later without changing existing manifests. Adding one means changing and releasing the app.
- **Build result.** The app writes a JSON result (artifacts and warnings per target) to stdout, logs to stderr, and exits non-zero if any target fails.

**Non-goals (v1):** system-wide (all-users/elevated) installs, auto-update, MSI/MSIX/`.pkg`/deb/rpm/Flatpak/Snap output, universal (fat) macOS binaries, building macOS packages on non-Mac hosts, running the app's own build (`dotnet publish`, `go build`, etc.), detecting single-file or NativeAOT .NET output, installing or checking for a .NET runtime on the user's machine, a NuGet library or `dotnet tool` distribution, loading plugins from outside assemblies, repackaging payloads that are already `.app` bundles, building AppImages without Docker, automatic downloading of external tools (other than what the container image fetches when it is built), publishing the container image to a registry, and creating signing certificates or keychain setup (that stays a one-time machine setup, as in screen-rec).

## Capabilities

### New Capabilities
- `package-definition`: The JSON manifest: shared app metadata, the targets list (OS, architecture, payload as zip or directory, format-specific options), schema versioning, and validation rules, including version-format compatibility.
- `runtime-profiles`: Named presets that add stack-specific inference and defaults without the core assuming any stack: `generic`, and `dotnet` with detection of framework-dependent and self-contained .NET 10 publish output and its macOS entitlements.
- `build-orchestration`: The build pipeline: per-target preflight (host, tools, identity), payload preparation, the internal format/signer/profile contracts, working and output directories, per-target error reporting, warnings, and the build result.
- `command-line-interface`: The console app's command line: arguments, JSON result on stdout, logging on stderr, and exit codes.
- `windows-installer`: Producing a per-user Windows setup executable, including install layout, shortcuts, uninstall registration, upgrades, and the uninstaller.
- `macos-package`: Producing a macOS `.app` bundle, signing it (identity, hardened runtime, entitlements), optionally notarizing it, and packaging it as `.dmg` (and optionally `.zip`).
- `linux-appimage`: Producing a Linux AppImage from the payload in a Docker container with pinned tooling.

### Modified Capabilities
<!-- None: no existing specs in this project. -->

## Impact

- **New code (all C#)**: a layered solution under `solution/`: `Installer.Cli` (entry point), `Installer.Services` (business logic), `Installer.Dao` (file system, processes and external tools) and `Installer.Dtos`, all wired with dependency injection. Each project except Dtos has its own NUnit test project. The repository root also holds `jenkins/`, `scripts/` and `docs/`, as in screen-rec. The solution includes sample payloads for end-to-end tests: a .NET 10 app (framework-dependent and self-contained) and one non-.NET executable, to prove the app doesn't depend on .NET. Generated build inputs (NSIS scripts, `Info.plist`, `.desktop` files, icons) are produced from C#. All packaging logic is C#. The only non-C# packaging file is the AppImage container definition (a Dockerfile), shipped inside the app; `scripts/` and `jenkins/` only build, test and publish the builder itself. There is no existing code to change.
- **Library dependencies**: SkiaSharp (MIT) for icon resizing, which adds native assets to the app's build.
- **Public contracts**: the manifest schema and the command line (arguments, result JSON, exit codes) become compatibility commitments once released. The manifest carries a schema version from day one.
- **External tool dependencies** (needed on the build host, not bundled):
  - Windows format: NSIS `makensis` 3.08 or later, on any host (`brew install makensis` on the Mac).
  - macOS format: `codesign`, `security`, `hdiutil`, `ditto` and `xcrun notarytool`/`stapler`, all included with macOS / Command Line Tools. Icons for every format are generated in C#, so no image tools are needed.
  - Linux format: a Docker-compatible engine. On the Mac agent it must run headless for the Jenkins user (e.g., Colima rather than Docker Desktop).
  - The app itself needs the .NET 10 runtime on the build host, even for non-.NET projects.
- **Host constraints**: macOS packages must be built on a Mac. The Jenkins Mac agent can then build all three platforms in one run.
- **Machine setup**: each Mac that builds needs the signing identity in the build user's unlocked login keychain, and a `notarytool` keychain profile if notarizing, which is the same one-time setup screen-rec's `docs/design/ci-jenkins.md` describes, plus a running Docker engine for Jenkins's user.
- **Consumers**: my other projects, in any stack. screen-rec is first: its `New-MacApp.ps1`, `ScreenRec.entitlements` and the packaging part of `Publish.ps1` can be replaced by a manifest using the `dotnet` profile and one command.
