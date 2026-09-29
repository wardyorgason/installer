# Spec Delta

## Purpose

Produces a signed macOS `.app` bundle from the payload, optionally notarizes it, and packages it as a drag-to-install disk image and/or a zip.

## ADDED Requirements

### Requirement: macOS host
macOS targets MUST be built on a macOS host.

#### Scenario: Non-Mac host
- **WHEN** a macOS target is built on Linux
- **THEN** the target fails preflight with an error that macOS packages require a macOS host

### Requirement: App bundle layout
The builder SHALL create `<name>.app`. It SHALL place the payload's files in `Contents/MacOS`, and SHALL move each top-level payload directory to `Contents/Resources`, leaving a relative symbolic link with the directory's name in `Contents/MacOS`, so the app finds each directory both at its original path next to the executable and under `Contents/Resources`. The icon SHALL be written to `Contents/Resources/<name>.icns`. A payload directory whose name collides with the icon file's name MUST fail the target.

#### Scenario: Payload with subfolders
- **WHEN** the payload contains `ScreenRec`, `ScreenRec.dll` and `wwwroot/index.html`
- **THEN** `Contents/MacOS/ScreenRec` and `Contents/MacOS/ScreenRec.dll` exist, `Contents/Resources/wwwroot/index.html` exists, and `Contents/MacOS/wwwroot/index.html` resolves to the same file

### Requirement: Info.plist
The builder SHALL generate `Contents/Info.plist` with the bundle name and display name (`name`), identifier (`id`), executable (the main executable's file name), package type `APPL`, short version string (the first three parts of `version`), bundle version (the full `version`), the icon file, and high-resolution support. Keys from `macos.infoPlist` SHALL be added; JSON strings, booleans, integers, arrays and objects SHALL map to the matching property list types. A caller key that would override the identifier, executable or version keys MUST fail validation.

#### Scenario: Caller keys added
- **WHEN** `macos.infoPlist` sets `NSMicrophoneUsageDescription` and `NSAppTransportSecurity: { "NSAllowsLocalNetworking": true }`
- **THEN** `Info.plist` contains that string and a dictionary with a true boolean

#### Scenario: Versions mapped
- **WHEN** `version` is `1.0.0.12`
- **THEN** `CFBundleShortVersionString` is `1.0.0` and `CFBundleVersion` is `1.0.0.12`

#### Scenario: Reserved key overridden
- **WHEN** `macos.infoPlist` sets `CFBundleIdentifier`
- **THEN** validation fails with an error that the key is set from the manifest's `id`

### Requirement: Signing identity
Every macOS target MUST declare `macos.identity`, the name of a code-signing identity in the build user's keychain. Preflight MUST fail the target when that identity is not installed and valid for code signing, and the error SHALL list the identities that are available.

#### Scenario: Identity missing
- **WHEN** `macos.identity` is `ScreenRec Dev` and the keychain has no such valid identity
- **THEN** the target fails preflight, before any target is built, with an error naming the identity and listing the available ones

#### Scenario: No identity declared
- **WHEN** a macOS target has no `macos.identity`
- **THEN** validation fails with an error that macOS targets require a signing identity

### Requirement: Code signing
The builder SHALL sign the bundle inside out: every file in `Contents/MacOS` other than the main executable (without following symbolic links), every Mach-O file in `Contents/Resources`, and then the bundle itself, with the app's `id` as identifier. Every signature SHALL use the hardened runtime. The bundle signature SHALL carry the entitlements: the profile's defaults merged with `macos.entitlements`. Signatures SHALL include a secure timestamp when the identity is a `Developer ID Application` identity, and SHALL NOT otherwise. After signing, the builder MUST verify the bundle's signature strictly and fail the target if verification fails.

#### Scenario: Self-signed identity
- **WHEN** the identity is a self-signed `ScreenRec Dev`
- **THEN** the bundle is signed with the hardened runtime and entitlements, without a secure timestamp, and its signature verifies

#### Scenario: Developer ID identity
- **WHEN** the identity is `Developer ID Application: Example (TEAMID)`
- **THEN** every signature has the hardened runtime and a secure timestamp

#### Scenario: Payload files with extended attributes
- **WHEN** payload files carry quarantine or Finder extended attributes
- **THEN** signing still succeeds

### Requirement: Outputs
`macos.outputs` SHALL select the artifacts, from `dmg` and `zip`, and SHALL default to `["dmg"]`. The disk image, `<name>-<displayVersion>-<target-name>.dmg`, SHALL contain the app and a link to `/Applications` for drag-to-install, and SHALL itself be signed with the target's identity. The zip, `<name>-<displayVersion>-<target-name>.zip`, SHALL contain the `.app` with its signature, permissions and symbolic links intact. An empty `outputs` MUST fail validation.

#### Scenario: Default output
- **WHEN** a macOS target does not set `outputs`
- **THEN** only a signed `.dmg` is produced, which, when opened, shows the app next to an Applications link

#### Scenario: Zip keeps the signature
- **WHEN** `outputs` includes `zip` and the zip is extracted on a Mac
- **THEN** the extracted app's signature verifies

### Requirement: Notarization
When `macos.notarize.keychainProfile` is set, the builder SHALL submit the target's first output (the `.dmg` when produced, otherwise the `.zip`) for notarization with that keychain profile and wait for the result. It SHALL then staple the ticket to the `.dmg` (when produced) and to the `.app`, and SHALL produce the `.zip` from the stapled `.app`. A notarization result other than accepted MUST fail the target, and the error SHALL include the path of the saved notarization log. Notarization MUST require a `Developer ID Application` identity; any other identity SHALL fail validation.

#### Scenario: Notarized disk image
- **WHEN** notarization is configured with a Developer ID identity and outputs `["dmg", "zip"]`
- **THEN** the `.dmg` is notarized and stapled, and the `.zip` contains a stapled `.app`

#### Scenario: Notarization rejected
- **WHEN** Apple returns status `Invalid`
- **THEN** the target fails, and the error contains the path of the saved notarization log

#### Scenario: Notarization with a self-signed identity
- **WHEN** `notarize` is set and the identity is `ScreenRec Dev`
- **THEN** validation fails with an error that notarization requires a Developer ID Application identity
