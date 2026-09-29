# Installer

Builds installers for Windows, macOS and Linux from one JSON manifest, on build/release machines (never on end-user
machines). A .NET 10 console app: point it at your app's build output, from any toolchain, and it produces:

| Target | Output |
|---|---|
| Windows | Per-user setup `.exe` (NSIS): no admin prompt, Start Menu shortcut, *Installed apps* entry, upgrade in place, uninstaller |
| macOS | Signed `.app` (hardened runtime, entitlements) as a drag-to-install `.dmg` and/or `.zip`, optionally notarized and stapled |
| Linux | `.AppImage`, packed in a pinned Docker image |

The Jenkins Mac agent builds all three: NSIS runs on macOS, and the AppImage is packed in a container. The `dotnet`
profile understands `dotnet publish` output directly; the `generic` profile packages anything else.

## Use it

Download `Installer-<version>.zip` from the [releases](https://github.com/wardyorgason/installer/releases), unpack it
on the build host (macOS or Linux with the .NET 10 runtime), and run:

```bash
dotnet Installer.Cli.dll build installer.json           # every target
dotnet Installer.Cli.dll build installer.json -t windows-x64 -o out
```

A minimal manifest:

```jsonc
{
  "schemaVersion": 1,
  "id": "com.example.myapp",
  "name": "MyApp",
  "version": "${env:APP_VERSION}",          // 1.2.0.45: numbers only
  "displayVersion": "${env:APP_DISPLAY_VERSION}",
  "publisher": "Example",
  "description": "What it does",
  "icon": "assets/icon-1024.png",
  "profile": "dotnet",
  "macos": { "identity": "Developer ID Application: Example (TEAMID)" },
  "targets": [
    { "os": "windows", "arch": "x64", "payload": "dist/publish/win-x64" },
    { "os": "macos", "arch": "arm64", "payload": "dist/publish/osx-arm64.zip" },
    { "os": "linux", "arch": "x64", "payload": "dist/publish/linux-x64" }
  ]
}
```

stdout is a JSON result (artifacts with SHA-256, warnings, errors per target); logs go to stderr. Exit code 0 means
every target succeeded, 1 means a target failed, 2 means the manifest or arguments are invalid.

| Doc | Covers |
|---|---|
| `docs/usage.md` | Command line, result JSON, exit and error codes |
| `docs/manifest-reference.md` | Every manifest field, platform sections, `${env:…}`, version mapping |
| `docs/windows.md`, `docs/macos.md`, `docs/linux.md` | What each format does, signing, notarization, Docker, setup |
| `docs/ci-jenkins.md` | The Mac agent, the Jenkins jobs, releasing |

## Layout

| Folder | Purpose |
|---|---|
| `solution/` | `Installer.sln` and one folder per project: `Installer.Cli` → `Installer.Services` → `Installer.Dao`, shared `Installer.Dtos`, a `UnitTests.*` project for each, and `Samples.DotnetApp` |
| `scripts/` | PowerShell 7 scripts; the same commands run locally and on Jenkins |
| `jenkins/` | `test.Jenkinsfile`, `build.Jenkinsfile` (zip on every branch), `release.Jenkinsfile` (manual GitHub release) |
| `docs/` | Usage, manifest reference, per-platform notes, CI setup |
| `openspec/` | The OpenSpec change that specifies and designs the builder |
| `dist/` | Gitignored. Published zip, test results |

## Build

```powershell
pwsh scripts/Build.ps1                                  # restore + build Release
pwsh scripts/Test.ps1                                   # unit tests, JUnit XML into dist/test-results
pwsh scripts/Test.ps1 -IncludeIntegration -IncludeEndToEnd   # plus real makensis, Docker, codesign (skipped when missing)
pwsh scripts/Publish.ps1                                # dist/Installer-<version>.zip + .sha256 + release.json
pwsh scripts/Clean.ps1
```

Releases: `installer-build` archives the zip for every branch; `installer-release` is run by hand and publishes a chosen
build as a GitHub release (`scripts/Publish-Release.ps1`, GitHub REST API). See `docs/ci-jenkins.md`.
