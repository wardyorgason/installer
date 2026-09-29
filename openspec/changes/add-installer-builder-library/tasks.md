# Tasks

## 1. Repository and solution scaffolding

- [x] 1.1 Create the root folders `docs/`, `jenkins/`, `scripts/` and `solution/`. In `solution/`, add `Installer.sln`, `global.json` (SDK 10.0.100, `latestFeature`), `nuget.config` (nuget.org only), `Directory.Build.props` (net10.0, nullable, warnings as errors, central package management, and the `UnitTests.*` block adding NUnit, NUnit3TestAdapter, Microsoft.NET.Test.Sdk, Moq, NetArchTest.Rules, JunitXml.TestLogger and TimeProvider.Testing) and `Directory.Packages.props`. Verify: `dotnet restore solution/Installer.sln` succeeds
- [x] 1.2 Create `Installer.Cli` (console app), `Installer.Services`, `Installer.Dao`, `Installer.Dtos` and `UnitTests.Installer.Cli` / `.Services` / `.Dao`, with the project references from design D1's layer table, and `InternalsVisibleTo` for each test project and `DynamicProxyGenAssembly2`. Verify: `dotnet build solution/Installer.sln` succeeds with no warnings
- [x] 1.3 Add empty `AddInstallerDao()`, `AddInstallerServices()` and `AddInstallerCli()`, plus `ServiceRegistration.AddInstaller()` and a `Program` with the `System.CommandLine` root and `--version`. Verify: `ServiceRegistrationTests` (`ValidateOnBuild`) and a `LayerRulesTests` fixture in each test project pass, and `dotnet run --project solution/Installer.Cli -- --version` prints the version
- [x] 1.4 Add `Samples.DotnetApp` (a .NET 10 console app that prints its arguments and version) to the solution, and the Go sample (source plus committed `windows-amd64`, `darwin-arm64` and `linux-amd64` binaries) under `UnitTests.Installer.Cli/TestData/generic/`. Verify: `dotnet publish` of the sample succeeds for `win-x64`, `osx-arm64` and `linux-x64` (framework-dependent and self-contained), and the Go binary for the host runs
- [x] 1.5 Add `scripts/Common.ps1`, `Build.ps1`, `Test.ps1` (excludes the `Integration` and `EndToEnd` categories by default, with switches to include them, and writes JUnit XML) and `Publish.ps1` (portable framework-dependent zip of `Installer.Cli` into `dist/`). Verify: each script runs locally, and `Publish.ps1` produces a zip whose `Installer.Cli.dll --version` works
- [ ] 1.6 Add `jenkins/test.Jenkinsfile` and `jenkins/build.Jenkinsfile` for the `dotnet10 && macos` agent (PATH with Homebrew and dotnet, JUnit results, zip archived), and `docs/ci-jenkins.md` covering agent setup: .NET 10, PowerShell, makensis, Colima for the Jenkins user, and the test signing identity. Verify: both jobs run green on the Mac agent

## 2. macOS bundle layout spike (design risk)

- [ ] 2.1 On the Mac, hand-build a bundle from screen-rec's real `osx-arm64` publish output with the D7 layout (folders moved to `Contents/Resources`, relative symlinks in `Contents/MacOS`), sign it inside out with `ScreenRec Dev` using the hardened runtime and screen-rec's entitlements, and run `codesign --verify --strict --deep`. Verify: the signature verifies, the app launches, the UI loads from `wwwroot`, and the existing Screen Recording and Microphone grants still apply
- [ ] 2.2 If a Developer ID identity is available, package the spike bundle as a signed `.dmg`, notarize and staple it, then copy the app out of the quarantined `.dmg` and launch it. Verify: notarization is `Accepted` and Gatekeeper opens the app without a warning (otherwise record that it's pending)
- [ ] 2.3 Record the spike's commands and results in `docs/spikes/macos-bundle-layout.md`. If the symlink layout failed, switch design D7 to the move-only fallback with `/opsx:update` before starting group 8. Verify: the doc exists and D7 matches the outcome

## 3. DTOs and the Dao foundation

- [x] 3.1 Add the Dtos: manifest records and platform options, `TargetOs` / `TargetArch`, `Problem` / `Severity` / `ErrorCodes`, `BuildFailedException`, `BuildRequest` / `BuildResult` / `TargetResult` / `BuiltArtifact`, `ToolCommand` / `ToolResult`, `HostInfo`, `PreparedPayload`, `ProfileAnalysis`, `TargetContext` and `SigningOptions`. Verify: the solution builds, and Dtos has no project or package dependencies (layer rule test)
- [x] 3.2 Add the wrappers `IProcessWrapper`, `IFileSystemWrapper`, `IZipFileWrapper`, `IEnvironmentWrapper` and `IEmbeddedResourceWrapper` with their implementations and registrations. Verify: `[Category("Integration")]` tests pass for process output capture, stdin, exit code and cancellation killing the process tree, plus symlink creation and Unix mode setting
- [x] 3.3 Implement `IToolDao`: locate tools on PATH plus `/opt/homebrew/bin` and `/usr/local/bin`, run a `ToolCommand`, log the command line, and turn a non-zero exit into a `BuildFailedException` carrying stderr. Verify: unit tests with mocked wrappers cover lookup order, a missing tool, a non-zero exit and verbose logging
- [x] 3.4 Implement `IEnvironmentDao`, `IManifestDao`, `IWorkspaceDao` (work directories, writes, moves, symlinks, artifact copy with size and SHA-256) and `IPayloadDao` (zip entry listing, extraction with prefix strip, directory copy, file listing, header bytes, Unix modes). Verify: unit tests with mocked wrappers for each method, plus one integration test that extracts a real zip

## 4. Manifest (package-definition)

- [x] 4.1 Implement `IEnvironmentExpansionService` for `${env:NAME}` in every string value. Verify: unit tests for the spec's "Environment variable expansion" scenarios (whole value, inside a longer string, unset variable), and that no other syntax is interpreted
- [x] 4.2 Implement `IVersionService`: parse 3- or 4-part numeric versions (0–65535), default `displayVersion`, and map to the short version string, bundle version and padded 4-part version. Verify: unit tests for the spec's "Version format" scenarios and the design D3 mapping table
- [x] 4.3 Implement parsing in `IManifestService`: a source-generated strict JSON context, errors that name the JSON path, top-level/target platform-section merging (objects merged, arrays and scalars replaced), default target names, and paths resolved relative to the manifest. Verify: unit tests for "Strict parsing", "Platform option sections" and "Path resolution"
- [x] 4.4 Implement `IManifestValidationService` with every rule in the package-definition spec (schema version, id, icon PNG size read from the header, known profile, targets, names, payloads, wrong-OS sections, `signCommand` placeholder, macOS identity, outputs and notarize rules, reserved `Info.plist` keys), reporting all problems. Verify: one unit test per spec scenario
- [x] 4.5 Write `docs/manifest-reference.md` (every field, platform sections, env expansion, version mapping, a full example). Verify: a unit test loads and validates the doc's example manifest

## 5. Build pipeline and CLI (build-orchestration, command-line-interface)

- [x] 5.1 Implement `IPackageFormatResolver`, `IRuntimeProfileResolver` and `IPreflightService`: host OS, tool presence and minimum version, every target preflighted before any target builds, and failures isolated per target. Verify: unit tests with mocked format services for the spec's "Preflight before building" and "Tool discovery" scenarios
- [x] 5.2 Implement `IBuildService`: targets built one at a time, `--target` selection (`not-selected`), per-target work directories (deleted on success, kept on failure), `BuildFailedException` isolation, artifact naming, default and overridden output directory, replacement of existing artifacts, and result assembly. Verify: unit tests for "Independent targets", "Output and working directories" and "Build result"
- [x] 5.3 Implement the `build` command and `IBuildCommandHandler` (`--output`, `--target`, `--verbose`, exit codes 0/1/2, unknown target, missing manifest), `IResultWriter` with `IConsoleWrapper` (exactly one JSON document on stdout), and console logging to stderr. Verify: CLI unit tests for every command-line-interface spec scenario
- [x] 5.4 Write `docs/usage.md` (invocation, options, exit codes, result JSON, error codes). Verify: the documented options and exit codes match the CLI tests

## 6. Payload and runtime profiles

- [x] 6.1 Implement `IPayloadService`: working copy, single-top-folder zip detection, path-escape rejection, and execute bits for Mach-O, ELF and `#!` files on macOS/Linux targets. Verify: unit tests for the spec's "Payload preparation" scenarios, including the untouched directory payload
- [x] 6.2 Implement `IBinaryInspectionService`: PE machine, thin and fat Mach-O (telling them apart from Java class files) and ELF `e_machine`, plus the target platform check. Verify: unit tests with header fixtures for "Main executable platform check", including the universal-binary match
- [x] 6.3 Implement `GenericProfileService`. Verify: unit tests for the "Generic profile" scenarios
- [x] 6.4 Implement `DotnetProfileService`: runtimeconfig discovery and `executable` override, apphost per OS, self-contained vs framework-dependent detection with the runtime warning, the deps.json runtime identifier check, and default entitlements with caller merge and `false` removal. Verify: unit tests against the `TestData/Payloads` fixtures (framework-dependent, self-contained, portable, single-file, two apps, no apphost) for every runtime-profiles scenario
- [x] 6.5 Wire preparation, analysis and the header check into `IBuildService`. Verify: a pipeline unit test with mocks asserts the order and that a failed check stops only that target

## 7. Icons

- [x] 7.1 Add SkiaSharp behind `IImageWrapper`, and `IImageDao` (PNG dimensions, resized PNG bytes). Verify: an integration test resizes a 1024 px PNG to 16 px and 256 px and decodes both
- [x] 7.2 Implement `IIcoEncoderService` (16–256 px PNG entries) and `IIcnsEncoderService` (`ic07`–`ic14`). Verify: unit tests parse the encoded headers and check every entry's type, size and offset
- [x] 7.3 Implement `IIconService` (sizes per target OS). Verify: unit tests with a mocked `IImageDao` check the requested sizes for each OS

## 8. macOS package

- [x] 8.1 Implement `IKeychainDao`, `ICodesignDao`, `IDiskImageDao`, `IMacArchiveDao` and `INotaryDao`. Verify: unit tests with a mocked `IToolDao` assert the exact argument lists and parse sample `security find-identity` output and `notarytool` JSON (Accepted, Invalid)
- [x] 8.2 Implement `IPlistService`: `Info.plist` generation with the JSON → plist type mapping and version keys, plus the entitlements plist. Verify: golden-file tests (`TestData/Golden`) for the "Info.plist" scenarios
- [x] 8.3 Implement `IAppBundleService`: payload into `Contents/MacOS`, directories moved to `Contents/Resources` with relative symlinks (or the fallback from 2.3), `.icns` placement, and the collision error. Verify: unit tests with a mocked `IWorkspaceDao` for the "App bundle layout" scenario
- [x] 8.4 Implement `IMacSigningService` and `CodesignFileSigner`: `xattr -cr`, inside-out order without following symlinks, Mach-O files in Resources, the bundle signed with identifier and entitlements, the hardened runtime always on, the timestamp rule, and strict verification. Verify: unit tests assert the call order and options for a self-signed and a Developer ID identity
- [x] 8.5 Implement `IMacOutputService` (`.dmg` staging with an Applications link, `.dmg` signing, `ditto` zip) and `INotarizationService` (first output submitted, non-Accepted fails with the saved log path, staple the `.dmg` and the `.app`, zip from the stapled `.app`). Verify: unit tests for the "Outputs" and "Notarization" scenarios
- [x] 8.6 Implement `MacFormatService` preflight (macOS host, identity in the keychain) and build, and register it. Verify: unit tests for the "macOS host" and "Signing identity" scenarios
- [ ] 8.7 Add a macOS integration test that packages `Samples.DotnetApp` for `osx-arm64` with a test identity (ignored when there's no Mac or identity). Verify: it passes on the Mac agent, and the `.dmg` mounts and holds an app whose signature verifies
- [ ] 8.8 Write `docs/macos.md` (identity setup, notarytool profile, hardened runtime and entitlements, bundle layout). Verify: the steps match `docs/ci-jenkins.md` and the spike doc

## 9. Windows installer

- [x] 9.1 Implement `INsisDao` (`makensis -VERSION` parsed and checked against 3.08, compile). Verify: unit tests for version parsing, the too-old error and the compile arguments
- [x] 9.2 Implement `INsisScriptService`: per-user execution level, `$LOCALAPPDATA\Programs\<name>`, Start Menu shortcut, HKCU uninstall values, upgrade by silently running the old uninstaller, finish page with run option, version info, `.ico`, and `!finalize` / `!uninstfinalize` when signing is configured. Verify: golden-file tests with and without `signCommand`
- [x] 9.3 Implement `ISignCommandDao`, `CommandFileSigner`, `IWindowsSigningService` and the hidden `sign-file` command. Verify: unit tests for `{file}` substitution, failure output, and the CLI handler calling the service
- [x] 9.4 Implement `WindowsFormatService` (preflight, main-executable signing, script generation, compile, output name) and register it. Verify: unit tests for "Setup executable output" and "Optional Authenticode signing hook"
- [ ] 9.5 Add an integration test that builds a setup executable for `Samples.DotnetApp` `win-x64` with makensis (ignored when makensis is missing). Verify: it passes on the Mac agent and the output is a PE file with the expected file version
- [ ] 9.6 Write `docs/windows.md`, including a manual checklist run on a Windows machine as a standard user: install without UAC, shortcut, *Installed apps* entry, upgrade removing a dropped file, `/S` silent install and uninstall. Verify: the checklist has been run once and its results recorded in the doc

## 10. Linux AppImage

- [ ] 10.1 Add the embedded Dockerfile to `Installer.Dao` (base image pinned by digest, pinned `appimagetool` for `TARGETARCH`, `x86_64` and `aarch64` runtimes, SHA-256 checks) and `IContainerDefinitionDao`. Verify: `docker build` of it succeeds on the Mac agent, and fails with a deliberately wrong checksum
- [x] 10.2 Implement `IDockerDao` (`info`, `image inspect`, `build` from stdin, `create`, `cp` in and out, `start -a`, `rm`). Verify: unit tests assert the argument lists and the image-missing detection
- [x] 10.3 Implement `IAppDirService` and `IDesktopEntryService` (`usr/lib/<id>/`, `AppRun` symlink, `<id>.desktop`, icon and `.DirIcon`). Verify: golden-file test for the `.desktop` file and unit tests for the layout
- [x] 10.4 Implement `IAppImageService` (tag from the Dockerfile hash, build only when missing, create/cp/start/cp, `rm` also on failure, runtime per target arch) and `LinuxFormatService` (preflight with `docker info`), and register it. Verify: unit tests for the "Containerized build" scenarios and call order
- [ ] 10.5 Add an integration test that builds AppImages for `Samples.DotnetApp` `linux-x64` and `linux-arm64`, extracts each in a Linux container of its architecture and runs it with `--help` (ignored without Docker). Verify: it passes on the Mac agent, and a second run reuses the image
- [ ] 10.6 Write `docs/linux.md` (Colima setup for the Jenkins user, image caching, updating the pins). Verify: a fresh `colima start` following the doc lets 10.5 pass

## 11. End-to-end and release

- [ ] 11.1 Add `[Category("EndToEnd")]` tests that run the real composed container on a manifest covering `Samples.DotnetApp` (framework-dependent and self-contained for `win-x64`, `osx-arm64` and `linux-x64`) and the Go sample under `generic`. Verify: they pass in `jenkins/test.Jenkinsfile` on the Mac agent, and the result JSON lists every artifact with its hash
- [ ] 11.2 Extend `jenkins/build.Jenkinsfile` to publish and archive the versioned builder zip. Verify: the archived zip, unpacked on the agent, runs `dotnet Installer.Cli.dll --version` and builds the 11.1 manifest
- [ ] 11.4 Add `jenkins/release.Jenkinsfile` (manual) and `scripts/Publish-Release.ps1` (GitHub REST API, no gh) that publish a chosen `installer-build` run's zip as a GitHub release tagged on its commit, with the credential and job setup in `docs/ci-jenkins.md`. Verify: a first release on the real repository holds the zip and `.sha256`, and a second run with the same build is refused
- [ ] 11.3 Dry run for screen-rec: without committing to screen-rec, write an `installer.json` for its real publish outputs and build every platform with the released zip. Verify: the `.dmg` app launches with its permissions intact (notarized if a Developer ID is available), the setup executable installs on Windows, and the AppImage runs on Linux
