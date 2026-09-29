# Design

## Context

See proposal.md for motivation and scope. This design turns the proposal into a buildable structure. It leans on the working reference that already exists: screen-rec's `scripts/New-MacApp.ps1`, `Publish.ps1`, `Get-Version.ps1`, `ScreenRec.entitlements` and `docs/design/ci-jenkins.md`. Its signing order, its Contents/Resources workaround and its identity checks are proven on the Jenkins Mac agent, and this design keeps them unless a decision below says otherwise.

Constraints that shape the approach:

- The primary build host is the Jenkins Mac agent (Apple Silicon, `osx-arm64`). Jenkins runs there as a LaunchAgent of a logged-in user, with a PATH that lacks Homebrew and `dotnet` unless the job adds them.
- screen-rec builds several flavors per platform (framework-dependent and self-contained), and its version changes on every CI build (`1.0.0.<BUILD_NUMBER>`, shown as `1.0.0-b<BUILD_NUMBER>`).
- screen-rec's Blazor host loads `wwwroot` from `Contents/Resources` on macOS (`BlazorWebViewHost.DefaultContentRoot`). Whatever bundle layout we choose must keep that working.
- There are no external consumers yet, so the manifest and CLI can still be shaped freely before the first release.

## Goals / Non-Goals

**Goals:**
- One console app built in layers (Cli → Services → Dao, with shared Dtos; see D1), where every platform step can be unit-tested without that platform's tools by mocking Daos.
- Output that matches what screen-rec produces today on macOS: same identity checks, same inside-out signing, same notarization profile.
- CI-friendly: all values that change per build (version, payload paths) come in without editing the manifest, and the result is machine-readable.

**Non-Goals:**
- Windows as a build *host* is not tested in v1. The code avoids gratuitous Unix assumptions, but only macOS and Linux hosts run in CI.
- Building targets in parallel. Targets run one after another. This keeps logs readable and avoids codesign/keychain contention.
- A published JSON Schema file for editor completion. Unknown manifest keys are rejected instead (see D3), and a schema file can come later.
- Detecting or killing a running app during a Windows install or upgrade (see Risks).

## Decisions

### D1. Repository layout and layered solution

The structure follows the layering screen-rec uses: an entry point, services, data access and DTOs, each in its own project, with its own unit test project.

```
installer/
├─ openspec/
├─ docs/                      manifest reference for consumers, CI setup (like screen-rec's docs/design/ci-jenkins.md)
├─ jenkins/                   build.Jenkinsfile, test.Jenkinsfile
├─ scripts/                   Common.ps1, Build.ps1, Test.ps1, Publish.ps1: thin dotnet wrappers that build, test
│                             and publish this repository (no packaging logic, which all lives in C#)
└─ solution/
   ├─ Installer.sln
   ├─ Directory.Build.props   shared settings; test packages added to every UnitTests.* project
   ├─ Directory.Packages.props, global.json, nuget.config
   ├─ Installer.Cli/          entry point (console app)
   ├─ Installer.Services/     business logic
   ├─ Installer.Dao/          files, zips, environment, external tools, images, embedded Dockerfile
   ├─ Installer.Dtos/         records and enums shared by every project
   ├─ Samples.DotnetApp/      .NET 10 console app used only as an end-to-end payload; never shipped
   ├─ UnitTests.Installer.Cli/
   ├─ UnitTests.Installer.Services/
   └─ UnitTests.Installer.Dao/
```

**Layer rules**

| Project | Holds | May reference |
|---|---|---|
| `Installer.Cli` | `Program`, the composition root (`ServiceRegistration`), command definitions and handlers, the result writer, `IConsoleWrapper` | Services, Dtos; Dao **only** from `ServiceRegistration` |
| `Installer.Services` | All business logic: manifest expansion, merging and validation, the pipeline, payload rules, binary header parsing, profiles, formats, generating `.nsi` / `Info.plist` / entitlements / `.desktop`, icon encoding, signing order, notarization flow | Dao, Dtos |
| `Installer.Dao` | Everything that touches the outside world: file system, zips, environment, running processes, one Dao per external tool, image resizing, the embedded Dockerfile, and the wrappers | Dtos |
| `Installer.Dtos` | Records and enums only; no logic, no package dependencies | nothing |

- The CLI calls services only. `ServiceRegistration` is the single CLI type allowed to name `Installer.Dao`, and only to call `AddInstallerDao()`.
- Services never use `File`, `Directory`, `Process`, `Environment`, `ZipFile` or SkiaSharp directly; they go through Daos.
- Layer-rule tests (NetArchTest) in each test project enforce this, in the same way as screen-rec's `LayerRulesTests`:
  - Cli types other than `ServiceRegistration` have no dependency on `Installer.Dao`.
  - Services have no dependency on `Installer.Cli`, `System.Diagnostics`, `System.IO.Compression` or `SkiaSharp`.
  - Dao has no dependency on Services or Cli.
  - Dtos have no dependencies on the other projects.

**Dependency injection**

- `Microsoft.Extensions.DependencyInjection`. Each layer exposes one extension method: `AddInstallerDao()`, `AddInstallerServices()` and `AddInstallerCli()`. `ServiceRegistration.AddInstaller()` in the CLI calls all three.
- Every service and Dao is a public interface plus an `internal sealed` implementation, registered as a singleton. Implementations are stateless: per-target state travels in method parameters (`TargetContext`), never in fields. `InternalsVisibleTo` exposes the implementations to their test project and to `DynamicProxyGenAssembly2` (Moq).
- Contracts with several implementations are registered as enumerables and picked by a resolver: `IPackageFormatService` (Windows, macOS, Linux) through `IPackageFormatResolver`, by target OS; `IRuntimeProfileService` (generic, dotnet) through `IRuntimeProfileResolver`, by name. Adding a format or profile means new classes plus one registration line.
- `TimeProvider.System` is registered for run ids and timestamps. It is already abstract, so tests use `FakeTimeProvider` and need no wrapper.
- Logging uses `Microsoft.Extensions.Logging`, with the console provider writing everything to stderr and `ILogger<T>` injected.
- A registration test builds the container with `ValidateOnBuild` and `ValidateScopes` and resolves every command handler, so a missing registration fails a test instead of a build run.

**Wrappers** (`Installer.Dao/Wrappers/`, plus the console wrapper in the CLI). Each wraps one type that can't be mocked, as `I<Name>Wrapper` + `<Name>Wrapper`. They are pass-through only, with no logic, so they are covered by integration tests rather than unit tests.

| Wrapper | Wraps |
|---|---|
| `IProcessWrapper` | `System.Diagnostics.Process`: start with arguments, stdin, captured stdout/stderr, exit code, kill the process tree on cancellation |
| `IFileSystemWrapper` | `File`, `Directory`, `File.SetUnixFileMode`, symbolic links, file streams |
| `IZipFileWrapper` | `ZipFile` / `ZipArchive` |
| `IEnvironmentWrapper` | environment variables, `OperatingSystem` / `RuntimeInformation` (host OS and architecture), the temp path |
| `IImageWrapper` | SkiaSharp: decode, resize and encode PNG |
| `IEmbeddedResourceWrapper` | `Assembly.GetManifestResourceStream` |
| `IConsoleWrapper` (Cli) | `Console.Out` |

**Daos** (`Installer.Dao/<Area>/`). Each Dao turns one external thing into DTOs: it builds command lines and parses tool output, but makes no decisions.

| Dao | Responsibility |
|---|---|
| `IManifestDao` | read the manifest file |
| `IPayloadDao` | list zip entries, extract (optionally stripping a prefix), copy a directory, list files, read the first bytes of a file, set Unix modes |
| `IWorkspaceDao` | create and delete work directories, write generated files, move directories, create symlinks, copy artifacts to the output directory with size and SHA-256 |
| `IEnvironmentDao` | read environment variables; describe the host (OS, architecture, temp directory) |
| `IToolDao` | find a tool on PATH plus `/opt/homebrew/bin` and `/usr/local/bin`, and run a `ToolCommand` into a `ToolResult`, with logging; every tool Dao below runs through it |
| `IKeychainDao` | `security find-identity -v -p codesigning` → identities |
| `ICodesignDao` | `xattr -cr`, `codesign` sign (per `CodesignOptions`), `codesign --verify` |
| `IDiskImageDao` | `hdiutil create` |
| `IMacArchiveDao` | `ditto -c -k --sequesterRsrc --keepParent` |
| `INotaryDao` | `notarytool submit --wait` (JSON → `NotaryResult`), `notarytool log`, `stapler staple` |
| `INsisDao` | `makensis -VERSION`, compile a script |
| `IDockerDao` | `info`, `image inspect`, `build` from stdin, `create`, `cp` in and out, `start -a`, `rm` |
| `ISignCommandDao` | run the user's `signCommand` with `{file}` substituted |
| `IImageDao` | PNG dimensions; resized PNG bytes (through `IImageWrapper`) |
| `IContainerDefinitionDao` | the embedded Dockerfile text |

**Services** (`Installer.Services/<Area>/`)

| Area | Services |
|---|---|
| Build | `IBuildService` (the entry point the CLI calls: `BuildRequest` → `BuildResult`), `IPreflightService` |
| Manifest | `IManifestService` (load → expand → parse → merge → validate), `IEnvironmentExpansionService`, `IManifestValidationService`, `IVersionService` (parsing and platform mapping) |
| Payload | `IPayloadService` (root detection, path-escape check, execute bits), `IBinaryInspectionService` (PE / Mach-O / ELF headers, platform check) |
| Profiles | `IRuntimeProfileService` ×2 (`GenericProfileService`, `DotnetProfileService`), `IRuntimeProfileResolver` |
| Formats | `IPackageFormatService` ×3 (`WindowsFormatService`, `MacFormatService`, `LinuxFormatService`), `IPackageFormatResolver` |
| Windows | `INsisScriptService`, `IWindowsSigningService` (payload signing, and the hidden `sign-file` command) |
| macOS | `IAppBundleService`, `IPlistService` (`Info.plist` and entitlements), `IMacSigningService` (inside-out order), `IMacOutputService` (`.dmg` staging, `.zip`), `INotarizationService` |
| Linux | `IAppDirService`, `IDesktopEntryService`, `IAppImageService` (image tag from the Dockerfile hash, container steps) |
| Imaging | `IIconService` (sizes per OS), `IIcoEncoderService`, `IIcnsEncoderService` |
| Signing | `IFileSigner`, a stateless contract taking `(path, SigningOptions)`, with `CodesignFileSigner` and `CommandFileSigner` |

**CLI** (`Installer.Cli`)
- `Program` builds the service provider and the `System.CommandLine` tree (`build`, and the hidden `sign-file`), then resolves the handlers.
- `IBuildCommandHandler` maps arguments to a `BuildRequest`, calls `IBuildService`, writes the result through `IResultWriter` and returns the exit code.
- `ISignFileCommandHandler` calls `IWindowsSigningService`.
- `IResultWriter` serializes the result with a source-generated JSON context to `IConsoleWrapper`.

*Alternatives:*
- A single console project, the earlier draft. Rejected: it doesn't follow the layering used across these projects, and it gives no compile-level seam for mocking.
- Letting `AddInstallerServices()` register the Dao, so the CLI never names it at all. Rejected in favor of screen-rec's explicit composition root, which keeps the layer test simple and the wiring visible in one place.

### D2. Distribution and invocation

`Installer.Cli` is published (`scripts/Publish.ps1`) as a portable (RID-agnostic) framework-dependent build: `Installer.Cli.dll` plus dependencies, archived as a zip by this repository's Jenkins build. A build host unpacks it once and runs:

```
dotnet /opt/installer/Installer.Cli.dll build path/to/installer.json [--output dist] [--target <name>]... [--verbose]
```

- `build` is the only command in v1.
- `--target` (repeatable) builds a subset of targets. This helps on a dev box that can't build every platform, e.g., a Linux laptop without a signing identity.
- `--output` defaults to `dist` next to the manifest.
- `--version` prints the app's own version.
- Command-line parsing uses `System.CommandLine` 2.x.

*Alternative:* an apphost per host RID. Rejected: one portable zip serves macOS and Linux hosts alike, and `dotnet <dll>` is already on the agent's PATH.

### D3. Manifest shape

```jsonc
{
  "schemaVersion": 1,
  "id": "dev.screenrec.app",                 // reverse-DNS; bundle id, Windows uninstall key, .desktop name
  "name": "ScreenRec",                       // display name and artifact file-name prefix
  "version": "${env:APP_VERSION}",           // numeric, 3 or 4 parts, e.g. 1.0.0.123
  "displayVersion": "${env:APP_DISPLAY_VERSION}", // optional free text, e.g. 1.0.0-b123; defaults to version
  "publisher": "Ward Yorgason",
  "description": "Screen and microphone recorder",
  "icon": "assets/app-1024.png",
  "profile": "dotnet",                       // "generic" | "dotnet"
  "executable": null,                        // required for generic, optional override for dotnet
  "macos": {                                 // defaults for every macOS target, overridable per target
    "identity": "ScreenRec Dev",
    "entitlements": { "com.apple.security.device.audio-input": true },
    "infoPlist": {
      "LSMinimumSystemVersion": "14.0",
      "NSMicrophoneUsageDescription": "ScreenRec records your microphone when you turn it on for a recording.",
      "NSAppTransportSecurity": { "NSAllowsLocalNetworking": true }
    },
    "outputs": ["dmg", "zip"],               // default ["dmg"]
    "notarize": { "keychainProfile": "${env:NOTARY_PROFILE}" } // optional; Developer ID identity required
  },
  "windows": { "signCommand": null },        // optional Authenticode hook, see D10
  "linux": { "categories": ["AudioVideo"] },
  "targets": [
    { "os": "windows", "arch": "x64", "payload": "dist/ScreenRec-${env:APP_DISPLAY_VERSION}-win-x64.zip" },
    { "name": "windows-x64-selfcontained", "os": "windows", "arch": "x64",
      "payload": "dist/ScreenRec-${env:APP_DISPLAY_VERSION}-win-x64-selfcontained.zip" },
    { "os": "macos", "arch": "arm64", "payload": "dist/publish/osx-arm64" },
    { "os": "linux", "arch": "x64", "payload": "dist/publish/linux-x64" }
  ]
}
```

Rules:

- **Platform sections.** `macos`, `windows` and `linux` can appear at the top level (defaults for all targets of that OS) and inside a target (overrides). Objects merge key by key; arrays and scalars replace.
- **Target names.** Each target's `name` defaults to `<os>-<arch>` and must be unique. It appears in artifact file names and in the result, which is how screen-rec's `-selfcontained` flavor gets its own artifacts.
- **Environment expansion.** `${env:NAME}` in any string is replaced with that environment variable before validation. An unset variable is a validation error. This is the only substitution syntax: no defaults and no references between fields. It covers both per-build values (version, payload paths) without editing the manifest or adding CLI flags.
- **Paths.** Relative paths resolve against the manifest's directory.
- **Strict parsing.** The text is parsed into a `JsonNode` tree, expanded, and read by a small hand-written reader in Services that knows every allowed key. Unknown properties and wrong types are collected with their JSON paths (`$.macos.identiy`) instead of stopping at the first, so one run reports every problem. A strict source-generated `System.Text.Json` context was considered, but it stops at the first unmapped member and can't meet that requirement.
- **Versions.** `version` must be 3 or 4 dot-separated integers, each 0–65535. It maps as follows:

| Where | Value |
|---|---|
| `CFBundleShortVersionString` | first three parts (`1.0.0`) |
| `CFBundleVersion` | the full `version` (`1.0.0.123`) |
| NSIS `VIProductVersion` / `FileVersion` | `version`, padded to 4 parts |
| Windows *Installed apps* `DisplayVersion`, `X-AppImage-Version`, artifact file names | `displayVersion` |

*Alternatives:* a CLI `--set key=value` override mechanism (rejected: it needs its own path syntax, and payload paths would still need templating), and accepting semantic version strings and deriving numeric parts (rejected: guessing how `-beta.1` maps to a build number is exactly the kind of drift this tool exists to remove).

### D4. Pipeline and internal contracts

```
load + expand + validate manifest ──fail──► exit 2, nothing built
          │
          ▼
 preflight every target (host OS, tools, identity, option checks) ── a failed target is marked failed; others continue
          │
          ▼
 for each surviving target, in manifest order:
   prepare payload ─► profile analysis ─► format build (sign, package, notarize) ─► artifacts
          │
          ▼
 result JSON on stdout; exit 0 if every selected target succeeded, else 1
```

All preflight runs before any target builds, so a missing identity or `makensis` shows up in seconds, not after a Windows build. The pipeline lives in `IBuildService`. The contracts the formats and profiles plug into (all DI-registered, see D1; no plugin loading):

```csharp
public interface IRuntimeProfileService { string Name { get; }
    ProfileAnalysis Analyze(PreparedPayload payload, TargetSpec target); }
public interface IPackageFormatService  { TargetOs Os { get; }
    IReadOnlyList<Problem> Preflight(TargetSpec target, HostInfo host);
    Task<IReadOnlyList<BuiltArtifact>> BuildAsync(TargetContext context, CancellationToken ct); }
public interface IFileSigner            { Task SignAsync(string path, SigningOptions options, CancellationToken ct); }
```

- **DTOs.** `ProfileAnalysis` (main executable, warnings, default macOS entitlements), `TargetContext`, `Problem`, `BuiltArtifact` and the manifest records are all DTOs in `Installer.Dtos`.
- **One error shape.** `Problem` (`Code`, `Message`, `Severity`) is used for validation, preflight and build failures alike, and the error codes are constants in one `ErrorCodes` class in Dtos. Build steps throw `BuildFailedException(Problem)`, which `IBuildService` catches per target, so one target's failure never reaches the next.
- **External tools.** Every external tool call goes through `IToolDao`, which uses `IProcessWrapper` underneath. It logs the command line, captures output, and turns a non-zero exit into a `BuildFailedException` carrying the tool's stderr. Services unit tests mock the tool-specific Daos; Dao unit tests mock `IToolDao` or `IProcessWrapper` and assert the exact argument lists.

### D5. Payload preparation

Each target gets a fresh work directory: `$TMPDIR/installer/<run-id>/<target-name>/`. It is deleted when the target succeeds and kept when it fails; the result reports its path.

- **Folders** are copied into the work directory, never modified in place, because chmod, bundle moves and signing would otherwise alter the caller's build output.
- **Zips** are extracted with `System.IO.Compression`. If every entry sits under one top-level directory and there are no files at the root, that directory becomes the payload root. Entries that would escape the destination (`..`, absolute paths) are rejected.
- **Execute permission.** For macOS and Linux targets, every file is sniffed after extraction. Mach-O (`FEEDFACE`/`FEEDFACF`, or `CAFEBABE` with a plausible fat-arch count so Java class files don't match), ELF (`7F 45 4C 46`) and `#!` scripts get `u+x,g+x,o+x` via `File.SetUnixFileMode`. This covers the main executable, `createdump` and helper binaries alike.
- **Main executable check.** The core parses the main executable's header (PE machine, Mach-O CPU type including fat slices, ELF `e_machine`) and fails when its OS family or architecture doesn't match the target. This check runs for every profile, so the `generic` profile catches a mismatched payload too, and `dotnet` doesn't need a runtime identifier to detect the platform.

### D6. `dotnet` profile detection

1. **Find the app.** Find `*.runtimeconfig.json` at the payload root. Exactly one is required, unless the manifest names `executable`, in which case `<executable>.runtimeconfig.json` is used. If none is found, the error states that single-file and NativeAOT output are not detected and suggests `profile: generic`.
2. **Find the apphost.** The app name is the file name without `.runtimeconfig.json`. The apphost is `<app>.exe` for Windows targets and `<app>` otherwise. A missing apphost fails with a hint about `UseAppHost=false`.
3. **Deployment mode.** `runtimeOptions.includedFrameworks` means self-contained. `framework` / `frameworks` means framework-dependent, which adds a warning naming the required framework and version (e.g., `Microsoft.NETCore.App 10.0`).
4. **Platform check.** OS and architecture come from the apphost header (D5). When `<app>.deps.json` names a runtime identifier (`runtimeTarget.name` after `/`), it must agree; a portable framework-dependent publish has none, and that is fine.
5. **No TFM gate.** The files above have kept the same format since .NET Core 3. The profile doesn't reject other TFMs; .NET 10 is what it's tested against.
6. **Default macOS entitlements:** `com.apple.security.cs.allow-jit`, `com.apple.security.cs.allow-unsigned-executable-memory` and `com.apple.security.cs.disable-library-validation`. Caller entitlements merge over them, and a caller can set one to `false` to remove it.

### D7. macOS bundle layout

- **Layout.** The payload is copied into `Contents/MacOS`. Each top-level directory then moves to `Contents/Resources/<dir>`, and a relative symlink `Contents/MacOS/<dir> -> ../Resources/<dir>` takes its place. codesign seals symlinks as symlinks, so it no longer sees a folder in `Contents/MacOS` (the "bundle format unrecognized" failure screen-rec works around). The app still finds `runtimes/`, satellite-assembly folders and `wwwroot/` at their original relative paths. screen-rec's lookup in `Contents/Resources` keeps working as well. A payload directory whose name collides with a generated resource (`<name>.icns`) is a validation error.
- **`Info.plist`.** It is written by a small C# plist writer. JSON strings, booleans, integers, arrays and objects map to plist types. The tool generates:
  - `CFBundleName`, `CFBundleDisplayName`, `CFBundleIdentifier`, `CFBundleExecutable`
  - `CFBundlePackageType=APPL`, `CFBundleInfoDictionaryVersion`
  - both version keys (D3)
  - `CFBundleIconFile`, `NSHighResolutionCapable`

  Caller `infoPlist` keys are added on top. Overriding the identifier, executable or version keys is a validation error, because the manifest owns those. Anything else, including `LSMinimumSystemVersion`, the caller may set.
- **Icons.** See D11.

### D8. macOS signing and notarization

- **Preflight.** Check that the identity is listed by `security find-identity -v -p codesigning` as `"<identity>"`. The error message includes the keychain listing and the setup hint, as screen-rec's `Assert-MacSigningIdentity` does today. If notarization is requested, the identity must start with `Developer ID Application:` and `notarize.keychainProfile` must be set.
- **Signing**, inside out:
  1. `xattr -cr` the bundle, so codesign doesn't fail with "detritus not allowed".
  2. Sign every regular file under `Contents/MacOS` except the main executable, without following symlinks. .NET's managed dlls and json files live there, and codesign treats everything in that folder as code.
  3. Sign every Mach-O file under `Contents/Resources` (e.g., native libraries under a moved `runtimes/`). Notarization requires every binary to carry a Developer ID signature with the hardened runtime.
  4. Sign the bundle with `--identifier <id> --entitlements <generated.plist>`. The main executable is covered by the bundle signature.
  5. Run `codesign --verify --strict --deep`.

  All signing uses `--force --options runtime` (the hardened runtime is always on, as decided). It adds `--timestamp` for a Developer ID identity and `--timestamp=none` otherwise, because Apple's timestamp server only serves Apple-issued certificates.
- **Outputs**, per `macos.outputs`:
  - `dmg`: a staging folder holding the `.app` and an `Applications -> /Applications` symlink, then `hdiutil create -format UDZO -fs HFS+ -volname <name> -srcfolder …`, then the `.dmg` is signed with the same identity.
  - `zip`: `ditto -c -k --sequesterRsrc --keepParent`, which keeps the signature and permissions.
- **Notarization.** It happens once per target, on the first requested output (the `.dmg` if present):
  1. `xcrun notarytool submit <file> --keychain-profile <p> --wait --timeout 30m --output-format json`
  2. A status other than `Accepted` fails the target. The `notarytool log` output is saved into the work directory and its path goes into the error.
  3. `stapler staple` the `.dmg` (if built), then the `.app`, since the ticket covers nested code.
  4. The `.zip` is created (or re-created) from the stapled `.app`.

*Alternative:* notarize the `.app` first, then build and notarize the `.dmg` (two submissions, so the app inside the `.dmg` is stapled too). Rejected for v1 because it doubles the Apple round-trip. The stapled `.dmg` passes Gatekeeper, and the app is checked online the first time it opens.

### D9. Windows installer (NSIS)

- **Script generation.** C# generates `installer.nsi` into the work directory (raw string literal templates with escaped values) and runs `makensis -V2 -INPUTCHARSET UTF8 installer.nsi`. Preflight requires `makensis` 3.08 or later, because the signing hook uses `!uninstfinalize` (D10).
- **Script contents:**
  - `Unicode true`, `RequestExecutionLevel user`, `InstallDir "$LOCALAPPDATA\Programs\<name>"`, and MUI2 pages: install files, then a finish page with a "Run <name>" checkbox.
  - `File /r` from the prepared payload.
  - A Start Menu shortcut `$SMPROGRAMS\<name>.lnk` pointing to the main executable, plus `uninstall.exe` in `$INSTDIR`.
  - Registry key `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\<id>`, with `DisplayName`, `DisplayVersion`, `Publisher`, `DisplayIcon` (the main executable), `InstallLocation`, `UninstallString`, `QuietUninstallString`, `NoModify`, `NoRepair` and `EstimatedSize`.
  - `VIProductVersion` / `VIAddVersionKey` from D3.
- **Upgrade.** If the uninstall key exists, the installer runs the previous `uninstall.exe /S _?=<old InstallLocation>`, then installs fresh. This removes files that the new version no longer ships. Downgrades are allowed. Silent install (`/S`) works through NSIS itself.
- **Output name:** `<name>-<displayVersion>-<target-name>-setup.exe`.

### D10. Windows signing hook

`windows.signCommand` is an optional argument array with a `{file}` placeholder, for example `["osslsigncode", "sign", "-pkcs12", "${env:CERT}", "-in", "{file}", "-out", "{file}.signed"]`. It is run by `CommandFileSigner` (through `IWindowsSigningService` and `ISignCommandDao`):

- in C#, on the payload's main executable, before packaging;
- through NSIS `!uninstfinalize` on the uninstaller;
- through NSIS `!finalize` on the setup executable.

The two NSIS hooks call back into the builder (`dotnet Installer.Cli.dll sign-file <file> --command-json <path>`, a hidden command), so quoting and placeholder rules live in one C# implementation. When `signCommand` is absent, nothing is signed.

*Alternative:* signing only the finished setup executable from C#. Rejected: the uninstaller would stay unsigned, and NSIS embeds it, so it can't be signed afterwards.

### D11. Icons in C# with SkiaSharp

The manifest's single PNG, which must be square and at least 512 px (1024 px recommended; checked from the PNG header), is resized with SkiaSharp, behind `IImageWrapper` / `IImageDao`, while the `.ico` and `.icns` encoders are pure C# services. Resizing is needed because Windows needs a multi-size `.ico` and no built-in cross-platform .NET API resizes images. The same code writes all three formats:

- **`.icns`:** PNG-encoded entries `ic07`–`ic14`, from 128 px to 1024 px, including the @2x sizes.
- **`.ico`:** 16, 24, 32, 48, 64 and 256 px, with PNG-compressed entries.
- **AppImage:** a 256 px PNG.

Because this runs in C#, icon output is testable on any host, and the macOS format no longer needs `sips` or `iconutil`.

*Alternatives:*
- `sips` + `iconutil`, as screen-rec does. Rejected: it only works on macOS and doesn't solve `.ico`.
- ImageSharp. Rejected: its Six Labors split license needs checking for each consumer. SkiaSharp is MIT and already used by screen-rec.

### D12. Linux AppImage in Docker

- **AppDir, built in C# on the host:**
  - the payload goes under `AppDir/usr/lib/<id>/`;
  - `AppDir/AppRun` is a relative symlink to the main executable;
  - `AppDir/<id>.desktop` holds `Type=Application`, `Name`, `Comment`, `Exec=AppRun`, `Icon=<id>`, `Categories` (from `linux.categories`, default `Utility;`) and `X-AppImage-Version`;
  - `AppDir/<id>.png` is the icon, with `.DirIcon` linked to it.
- **Container image.** The Dockerfile is an embedded resource in `Installer.Dao`. The image tag is `installer-appimage:<first 12 hex of the SHA-256 of the Dockerfile>`. If `docker image inspect` fails, the builder runs `docker build -t <tag> -` with the Dockerfile on stdin (no build context). The Dockerfile itself:
  - is based on `debian:bookworm-slim`, pinned by digest;
  - downloads a tagged `appimagetool` release for the image's own architecture (`TARGETARCH`, so the container runs natively on the arm64 Mac);
  - downloads the matching type-2 runtime files for `x86_64` and `aarch64` into `/opt/appimage/`;
  - verifies every download against a SHA-256 in the Dockerfile.
- **Build run:**
  1. `docker create` with `appimagetool --no-appstream --runtime-file /opt/appimage/runtime-<arch> /work/AppDir /work/out.AppImage`, plus the environment variables `ARCH=<x86_64|aarch64>` and `APPIMAGE_EXTRACT_AND_RUN=1`, since there is no FUSE in the container.
  2. `docker cp` the AppDir into the container, which preserves modes and symlinks.
  3. `docker start -a`.
  4. `docker cp` the AppImage out.
  5. `docker rm`.
- **Output name:** `<name>-<displayVersion>-<target-name>.AppImage`.

*Alternatives:*
- Bind mounts. Rejected: Colima only shares `$HOME` by default, `$TMPDIR` under `/var/folders` isn't visible, and file ownership differs between engines. `docker cp` works with any engine.
- Streaming a tar through `docker run -i`. Rejected: tool logs and the archive would share one stdout.

### D13. Result, logging and exit codes

stdout carries only the result document:

```json
{ "schemaVersion": 1, "succeeded": false,
  "targets": [
    { "name": "macos-arm64", "os": "macos", "arch": "arm64", "status": "succeeded",
      "artifacts": [ { "kind": "dmg", "path": "/…/dist/ScreenRec-1.0.0-b123-macos-arm64.dmg", "size": 71234567, "sha256": "…" } ],
      "warnings": [ { "code": "dotnet.runtime-required", "message": "…" } ], "errors": [] },
    { "name": "linux-x64", "os": "linux", "arch": "x64", "status": "failed", "artifacts": [], "warnings": [],
      "errors": [ { "code": "tool.missing", "message": "docker was not found on PATH …" } ], "workDir": "/…" } ] }
```

- `status` is `succeeded`, `failed` or `not-selected` (excluded by `--target`).
- Error and warning `code`s are stable identifiers, part of the public contract.
- Logs go to stderr: step headers always, external tool output with `--verbose`.
- Exit codes: `0` when all selected targets succeeded, `1` when any target failed, `2` when the manifest or arguments are invalid. With exit `2`, stdout still carries a result with a top-level `errors` array and no targets.

### D14. Testing

- **Stack.** NUnit 4 (`NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`) with its constraint assertions (`Assert.That`), Moq for mocks, NetArchTest for layer rules, `Microsoft.Extensions.TimeProvider.Testing`, and `JunitXml.TestLogger` so Jenkins shows the results. `Directory.Build.props` adds these to every `UnitTests.*` project.
- **`UnitTests.Installer.Cli`:**
  - command handlers with mocked services: argument mapping, exit codes 0/1/2, and that stdout carries exactly one JSON document;
  - `ServiceRegistrationTests` and `LayerRulesTests`;
  - `[Category("EndToEnd")]` tests that run the real composed container on `Samples.DotnetApp` publish output (framework-dependent and self-contained, for `win-x64`, `osx-arm64` and `linux-x64`) and on the non-.NET sample in `TestData/generic/` (a small Go program; its source and its three cross-compiled binaries are committed, so the agent needs no Go), then check the result JSON. They run on the Mac agent.
- **`UnitTests.Installer.Services`**, the bulk of the tests, with mocked Daos:
  - manifest expansion, merging and validation;
  - version mapping;
  - zip root detection and path-escape rejection;
  - header parsing;
  - `dotnet` detection against fixture payloads in `TestData/Payloads` (framework-dependent, self-contained, portable, and single-file → error);
  - golden-file tests (`TestData/Golden`) for the `.nsi`, `Info.plist`, entitlements and `.desktop` output;
  - icon encoders;
  - the order of Dao calls (inside-out signing, stapling before zipping, one notarization per target);
  - `LayerRulesTests`.
- **`UnitTests.Installer.Dao`:**
  - Daos with mocked `IToolDao` or wrappers: exact argument lists, and parsing of `security find-identity`, `notarytool` JSON, `makensis -VERSION` and `docker image inspect` output;
  - `[Category("Integration")]` tests that use the real wrappers and tools and call `Assert.Ignore` when the host lacks them: `makensis` builds a real setup executable; `docker` builds a real AppImage, which is then run with `--appimage-extract` in a Linux container to check its layout and execute the app; macOS with a test identity signs, verifies and builds a `.dmg`. Notarization runs only in the Jenkins test job, behind a parameter, as in screen-rec;
  - `LayerRulesTests`.
- **`scripts/Test.ps1`** runs everything except `EndToEnd` and `Integration` by default, with switches to include them. `jenkins/test.Jenkinsfile` includes both on the Mac agent.

## Risks / Trade-offs

- **[codesign rejects the symlinks in `Contents/MacOS`, or Gatekeeper treats them differently than expected]** → The first macOS task is a spike: sign, verify and notarize screen-rec's real publish output with the symlink layout. If it fails, fall back to screen-rec's current move-only layout, documented as "apps must look in `Contents/Resources`", before building further on it.
- **[The hardened runtime is now always on, even with the self-signed `ScreenRec Dev` identity]** → That changes screen-rec's local builds: the microphone needs `com.apple.security.device.audio-input` in the manifest's entitlements (it is already in `ScreenRec.entitlements`). TCC grants depend on the designated requirement (identifier + certificate), which doesn't change, so existing Screen Recording permissions should survive. The spike above confirms this.
- **[Only the `.dmg` is stapled, so the app inside it is not]** → Gatekeeper checks the app online on first launch. Acceptable for v1; the two-submission flow (D8, alternative) can be added as an option later without changing manifests.
- **[Pinned `appimagetool` and runtime downloads disappear from GitHub]** → A failure only happens when the image is first built on a host. Once built, the cached image keeps working. Updating pins is a Dockerfile edit, and the new hash produces a new tag automatically.
- **[Docker on the Mac agent (Colima) isn't running for the Jenkins user]** → Preflight runs `docker info` and reports "Docker engine not reachable" as a target error, so the macOS and Windows targets still build.
- **[The app is running during a Windows upgrade, so files are locked]** → NSIS shows its standard Retry/Cancel dialog. Detecting and closing the app is out of scope for v1.
- **[`--timestamp=none` for non-Developer ID identities]** → Signatures from a self-signed identity carry no secure timestamp. That's fine for local or internal builds, which are never notarized.
- **[SkiaSharp adds native assets (`libSkiaSharp`) for many runtimes, about 440 MB untrimmed]** → Only the macOS and Linux native packages are referenced (Windows hosts are a non-goal), and `scripts/Publish.ps1` keeps just `osx`, `linux-x64` and `linux-arm64`, so the release zip is about 16 MB. A new supported host means adding its runtime folder back.
- **[Many small interfaces and registrations, and a forgotten one only fails at runtime]** → The registration test (D1) resolves every handler with `ValidateOnBuild`, so a missing registration fails CI.
- **[Environment expansion makes the manifest depend on the environment]** → Unset variables fail validation with the variable name, and the result JSON records the expanded version and payload paths.

## Migration Plan

This repository has nothing to migrate. screen-rec, the first consumer:

1. Keep `dotnet publish` in `Publish.ps1`, but stop bundling on macOS: the `osx-*` flavors are published as plain folders or zips, like Windows.
2. Add `installer.json` (as in D3). Export `APP_VERSION` (`Version.File`) and `APP_DISPLAY_VERSION` (`Version.Full`) from `Get-Version.ps1`, and move the `audio-input` entitlement and the `Info.plist` keys into the manifest.
3. Call `dotnet $INSTALLER/Installer.Cli.dll build installer.json` after publishing. Archive `dist/*.dmg`, `dist/*.zip`, `dist/*-setup.exe` and `dist/*.AppImage`.
4. Delete `New-MacApp.ps1`, `ScreenRec.entitlements` and the macOS branch of `Publish.ps1` once one Jenkins build has produced a working, notarized `.dmg`.

Rollback: until step 4, reverting the Jenkinsfile change restores the old script path.

## Open Questions

- **Published file name.** The published entry point is `Installer.Cli.dll`. Setting a friendlier `AssemblyName` (e.g., `installer`) only changes the invocation line.
- **Where release zips of the tool live.** A Jenkins artifact or a GitHub release. Either works with D2.
