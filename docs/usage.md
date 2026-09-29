# Usage

The builder is a .NET 10 console app, distributed as a zip (`Installer-<version>.zip`, from the `installer-build`
Jenkins job or `scripts/Publish.ps1`). Unpack it once on the build host and run it with `dotnet`:

```bash
dotnet /opt/installer/Installer.Cli.dll build installer.json
```

The build host needs the .NET 10 runtime, plus the tools each target format uses (see `docs/ci-jenkins.md`). The
manifest format is in `docs/manifest-reference.md`.

## `build`

```
Installer.Cli build <manifest> [--output <dir>] [--target <name>]... [--verbose]
```

| Option | Meaning |
|---|---|
| `<manifest>` | Path to the manifest. Relative paths inside it resolve against its directory. |
| `-o`, `--output <dir>` | Where artifacts go. Default: `dist` next to the manifest. Existing artifacts with the same name are replaced. |
| `-t`, `--target <name>` | Build only this target; repeat for several. Other targets are reported as `not-selected`. An unknown name builds nothing and exits with code 2. |
| `-v`, `--verbose` | Include each external tool's command line and output in the log. |
| `--version` | Print the builder's version and exit. |

Before building anything, the builder checks every selected target: the host OS its format needs, the tools
(`makensis`, `codesign`, `docker`, …), signing identities and the Docker engine. A target that fails these checks is
reported as failed without being built; the other targets still build. Targets then build one at a time, in manifest
order, each in its own temporary work directory, which is deleted when the target succeeds and kept (and reported)
when it fails.

## Output

stdout carries exactly one JSON document, the build result, whatever the outcome. Logs go to stderr, so a CI step can
pipe stdout straight into a JSON parser:

```bash
dotnet Installer.Cli.dll build installer.json > result.json
```

```json
{
  "schemaVersion": 1,
  "succeeded": false,
  "errors": [],
  "targets": [
    {
      "name": "macos-arm64", "os": "macos", "arch": "arm64", "status": "succeeded",
      "artifacts": [ { "kind": "dmg", "path": "/repo/dist/ScreenRec-1.0.0-b12-macos-arm64.dmg", "size": 71234567, "sha256": "…" } ],
      "warnings": [ { "code": "dotnet.runtime-required", "message": "Target 'macos-arm64' is framework-dependent: users need the Microsoft.NETCore.App 10.0 runtime installed." } ],
      "errors": []
    },
    {
      "name": "linux-x64", "os": "linux", "arch": "x64", "status": "failed",
      "artifacts": [], "warnings": [],
      "errors": [ { "code": "docker.unreachable", "message": "…" } ],
      "workDir": "/tmp/installer/20260928-120000-a1b2c3/linux-x64"
    }
  ]
}
```

- `status` is `succeeded`, `failed` or `not-selected`.
- `kind` is `setup-exe`, `dmg`, `zip` or `appimage`.
- Top-level `errors` are manifest or argument problems; when present, `targets` is empty and nothing was built.
- `workDir` appears only for failed targets.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Every selected target succeeded. |
| `1` | The manifest was valid, but at least one selected target failed. |
| `2` | The arguments or the manifest were invalid (including an unknown `--target`); nothing was built. |

## Error codes

Every warning and error carries a stable `code`; the message text may change between releases, the codes don't.

| Code | Meaning |
|---|---|
| `manifest.not-found`, `manifest.invalid-json` | The manifest file is missing or isn't JSON. |
| `manifest.unknown-property`, `manifest.wrong-type`, `manifest.missing-field`, `manifest.invalid-value` | A manifest field is misspelled, mistyped, missing or invalid. |
| `manifest.unsupported-schema` | `schemaVersion` isn't one this builder supports. |
| `manifest.env-unset` | A `${env:NAME}` reference names an unset variable. |
| `manifest.duplicate-target` | Two targets have the same name. |
| `cli.unknown-target`, `cli.invalid-arguments` | Bad command-line arguments. |
| `host.unsupported` | The target needs another host OS (macOS packages need a Mac). |
| `tool.missing`, `tool.version`, `tool.failed` | An external tool is missing, too old, or failed (its error output is in the message). |
| `docker.unreachable` | No running Docker engine. |
| `identity.missing` | The macOS signing identity isn't in the keychain. |
| `payload.not-found`, `payload.path-escape`, `payload.executable-missing`, `payload.platform-mismatch` | Payload problems: missing, unsafe zip entry, no main executable, or built for another OS/architecture. |
| `profile.no-runtimeconfig`, `profile.ambiguous-app`, `profile.no-apphost`, `profile.rid-mismatch` | The `dotnet` profile couldn't identify the app or it doesn't match the target. |
| `dotnet.runtime-required` | Warning: the app is framework-dependent and users need the named .NET runtime. |
| `bundle.name-collision` | A payload folder collides with a file the macOS bundle needs. |
| `sign.failed`, `notarize.rejected` | Signing failed, or Apple rejected the notarization (the log path is in the message). |
| `internal.unexpected` | A bug; the message has the details. |
