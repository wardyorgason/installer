# Manifest reference

The builder reads one JSON manifest (conventionally `installer.json`) that describes the app once and lists the
targets to build. Comments (`//`, `/* */`) and trailing commas are allowed.

```
dotnet Installer.Cli.dll build installer.json
```

Relative paths resolve against the manifest's directory, not the current directory. Unknown properties are errors,
so a typo such as `identiy` fails instead of being ignored; every problem in the manifest is reported in one run.

## App metadata

| Field | Required | Meaning |
|---|---|---|
| `schemaVersion` | yes | Always `1`. |
| `id` | yes | Reverse-DNS identifier, e.g. `dev.screenrec.app`. Used as the macOS bundle identifier, the Windows uninstall key and the Linux desktop entry name. |
| `name` | yes | Display name, and the prefix of every artifact file name and the Windows install folder. Must not contain `/ \ : * ? " < > \|`. |
| `version` | yes | Three or four numbers, each 0–65535: `1.2.0` or `1.2.0.45`. |
| `displayVersion` | no | Free text shown to users and used in file names, e.g. `1.2.0-b45`. Defaults to `version`. |
| `publisher` | yes | Shown in Windows *Installed apps* and the setup's version information. |
| `description` | yes | One line; the setup's file description and the Linux desktop entry comment. |
| `icon` | yes | A square PNG of at least 512×512 pixels (1024×1024 recommended). Converted to `.ico`, `.icns` and the AppImage icon. |
| `profile` | yes | `dotnet` (finds the main executable in `dotnet publish` output) or `generic` (any prebuilt app). |
| `executable` | `generic`: yes | The main executable's path relative to the payload root. With `dotnet`, it picks the app when a payload holds several. |

## Version mapping

| Where | Value | Example for `1.0.0.12` / `1.0.0-b12` |
|---|---|---|
| macOS `CFBundleShortVersionString` | first three parts of `version` | `1.0.0` |
| macOS `CFBundleVersion` | all of `version` | `1.0.0.12` |
| Windows file and product version | `version` padded to four parts | `1.0.0.12` (`2.1.0` → `2.1.0.0`) |
| Windows *Installed apps* version, AppImage version, artifact names | `displayVersion` | `1.0.0-b12` |

## Targets

`targets` is a non-empty array. Each target builds one artifact set:

| Field | Required | Meaning |
|---|---|---|
| `os` | yes | `windows` (setup `.exe`), `macos` (`.dmg` and/or `.zip`) or `linux` (`.AppImage`). |
| `arch` | yes | `x64` or `arm64`. |
| `payload` | yes | The app's built files: a `.zip` (contents at its root or in one top-level folder) or a directory. |
| `name` | no | Defaults to `<os>-<arch>`. Letters, digits, `.`, `_`, `-`; unique. Appears in artifact names. |
| `macos` / `windows` / `linux` | no | Options for this target; only the section matching its `os` is allowed. |

Artifacts are named `<name>-<displayVersion>-<target name>` plus a suffix: `-setup.exe`, `.dmg`, `.zip`, `.AppImage`.

## Platform sections

`macos`, `windows` and `linux` can appear at the top level, where they apply to every target of that OS, and inside a
target, where they override the top level key by key. Nested objects (`entitlements`, `infoPlist`, `notarize`) merge
the same way; arrays and plain values replace.

### `macos`

| Field | Default | Meaning |
|---|---|---|
| `identity` | required | Code-signing identity name in the build user's keychain, e.g. `ScreenRec Dev` or `Developer ID Application: Name (TEAMID)`. |
| `entitlements` | `{}` | Added to the profile's defaults (`dotnet` adds JIT, unsigned executable memory and disabled library validation). `false` removes a default. |
| `infoPlist` | `{}` | Extra `Info.plist` keys. Strings, booleans, integers, arrays and objects map to plist types. The identifier, executable and version keys are set from the manifest and can't be overridden. |
| `outputs` | `["dmg"]` | Any of `dmg` (drag-to-install disk image) and `zip` (the signed `.app`). |
| `notarize.keychainProfile` | none | A `notarytool` keychain profile. Notarizes and staples; requires a Developer ID identity. |

The app is always signed with the hardened runtime. A Developer ID identity also adds a secure timestamp.

### `windows`

| Field | Default | Meaning |
|---|---|---|
| `signCommand` | none | Optional Authenticode hook: an argument list containing `{file}`, run for the main executable, the uninstaller and the setup executable. |

### `linux`

| Field | Default | Meaning |
|---|---|---|
| `categories` | `["Utility"]` | Desktop entry categories. |

## Environment variables

`${env:NAME}` in any string value is replaced by the environment variable `NAME` before validation. An unset variable
is an error naming it. There is no other substitution syntax and no default values. Use it for anything that changes
per build, typically the version and the payload paths:

```bash
APP_VERSION=1.0.0.$BUILD_NUMBER APP_DISPLAY_VERSION=1.0.0-b$BUILD_NUMBER dotnet Installer.Cli.dll build installer.json
```

## Full example

```jsonc
{
  "schemaVersion": 1,
  "id": "dev.screenrec.app",
  "name": "ScreenRec",
  "version": "${env:APP_VERSION}",
  "displayVersion": "${env:APP_DISPLAY_VERSION}",
  "publisher": "Ward Yorgason",
  "description": "Screen and microphone recorder",
  "icon": "assets/app-1024.png",
  "profile": "dotnet",
  "macos": {
    "identity": "ScreenRec Dev",
    "entitlements": { "com.apple.security.device.audio-input": true },
    "infoPlist": {
      "LSMinimumSystemVersion": "14.0",
      "NSMicrophoneUsageDescription": "ScreenRec records your microphone when you turn it on for a recording.",
      "NSAppTransportSecurity": { "NSAllowsLocalNetworking": true }
    },
    "outputs": ["dmg", "zip"]
  },
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
