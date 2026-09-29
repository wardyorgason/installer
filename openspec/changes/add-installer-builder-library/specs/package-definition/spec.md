# Spec Delta

## Purpose

Defines the JSON manifest that describes an app to package: its shared metadata, the targets to build, per-platform options, and the validation rules that reject a bad manifest before any work starts.

## ADDED Requirements

### Requirement: Manifest schema version
The manifest SHALL contain an integer `schemaVersion`. The builder SHALL accept only schema versions it supports and SHALL reject any other value with an error naming the supported versions.

#### Scenario: Supported schema version
- **WHEN** a manifest has `"schemaVersion": 1`
- **THEN** the builder accepts it and continues validation

#### Scenario: Unsupported schema version
- **WHEN** a manifest has `"schemaVersion": 2` and the builder supports only 1
- **THEN** validation fails with an error that names the supported schema version

#### Scenario: Missing schema version
- **WHEN** a manifest has no `schemaVersion`
- **THEN** validation fails with an error naming the missing field

### Requirement: Shared app metadata
The manifest SHALL declare, once for all targets: `id`, `name`, `version`, `publisher`, `description`, `icon` and `profile`. It MAY declare `displayVersion` and `executable`. `id` MUST be a reverse-DNS identifier (at least two dot-separated segments of letters, digits and hyphens). `icon` MUST reference a square PNG of at least 512×512 pixels. `profile` MUST name a known runtime profile.

#### Scenario: Complete metadata
- **WHEN** a manifest declares every required field with valid values
- **THEN** validation of the metadata succeeds

#### Scenario: Invalid app id
- **WHEN** `id` is `ScreenRec` (a single segment)
- **THEN** validation fails with an error that `id` must be a reverse-DNS identifier

#### Scenario: Icon too small or not square
- **WHEN** `icon` references a 256×256 PNG, or a 1024×512 PNG
- **THEN** validation fails with an error stating the icon must be a square PNG of at least 512×512 pixels

#### Scenario: Unknown profile
- **WHEN** `profile` is `node`
- **THEN** validation fails with an error listing the known profiles

### Requirement: Version format
`version` MUST consist of three or four dot-separated integers, each between 0 and 65535. `displayVersion`, when present, MAY be any non-empty text without path separators; when absent it SHALL equal `version`.

#### Scenario: Four-part numeric version
- **WHEN** `version` is `1.0.0.123`
- **THEN** validation succeeds

#### Scenario: Semantic version with suffix rejected
- **WHEN** `version` is `1.2.0-beta.1`
- **THEN** validation fails with an error that `version` must be numeric and pointing to `displayVersion` for free-form text

#### Scenario: Part out of range
- **WHEN** `version` is `1.0.0.70000`
- **THEN** validation fails with an error that each part must be between 0 and 65535

#### Scenario: Display version defaults to version
- **WHEN** `version` is `1.0.0` and `displayVersion` is absent
- **THEN** every place that uses the display version uses `1.0.0`

### Requirement: Targets list
The manifest SHALL contain a non-empty `targets` array. Each target MUST declare `os` (`windows`, `macos` or `linux`), `arch` (`x64` or `arm64`) and `payload` (a path to a `.zip` file or a directory). Each target MAY declare `name`; it SHALL default to `<os>-<arch>`. Target names MUST be unique within the manifest and MUST contain only letters, digits, `.`, `_` and `-`.

#### Scenario: Default target name
- **WHEN** a target declares `"os": "macos", "arch": "arm64"` and no `name`
- **THEN** the target is named `macos-arm64`

#### Scenario: Two flavors for one platform
- **WHEN** two targets both have `os` `windows` and `arch` `x64`, and one has `"name": "win-x64-selfcontained"`
- **THEN** validation succeeds and both targets are built as separate targets

#### Scenario: Duplicate target names
- **WHEN** two targets resolve to the same name
- **THEN** validation fails with an error naming the duplicate

#### Scenario: Empty targets list
- **WHEN** `targets` is empty or absent
- **THEN** validation fails with an error that at least one target is required

#### Scenario: Payload not found
- **WHEN** a target's `payload` path does not exist, or is a file that is not a `.zip`
- **THEN** validation fails with an error naming the target and the path

### Requirement: Platform option sections
The manifest MAY contain `windows`, `macos` and `linux` option objects at the top level and inside any target. Top-level sections SHALL apply to every target of that OS; a target's section SHALL override them key by key, with nested objects merged the same way and arrays and scalar values replaced. A target MUST NOT contain a section for a different OS than its own.

#### Scenario: Target overrides a top-level option
- **WHEN** the top-level `macos` section sets `"outputs": ["dmg", "zip"]` and a macOS target sets `"macos": { "outputs": ["zip"] }`
- **THEN** that target produces only a `.zip`

#### Scenario: Nested objects merge
- **WHEN** the top-level `macos.infoPlist` sets key `A` and a target's `macos.infoPlist` sets key `B`
- **THEN** that target's `Info.plist` contains both `A` and `B`

#### Scenario: Section for the wrong OS
- **WHEN** a target with `os` `linux` contains a `macos` section
- **THEN** validation fails with an error naming the target and the section

### Requirement: Environment variable expansion
Every string value in the manifest SHALL have each `${env:NAME}` reference replaced with the value of environment variable `NAME` before validation. A reference to an unset variable MUST fail validation with an error naming the variable. No other substitution syntax SHALL be interpreted.

#### Scenario: Version from the environment
- **WHEN** `version` is `"${env:APP_VERSION}"` and `APP_VERSION` is `1.0.0.42`
- **THEN** the manifest's version is `1.0.0.42`

#### Scenario: Reference inside a longer string
- **WHEN** a payload is `"dist/App-${env:APP_DISPLAY_VERSION}-win-x64.zip"` and `APP_DISPLAY_VERSION` is `1.0.0-b42`
- **THEN** the payload path is `dist/App-1.0.0-b42-win-x64.zip`

#### Scenario: Unset variable
- **WHEN** a value references `${env:NOTARY_PROFILE}` and that variable is not set
- **THEN** validation fails with an error naming `NOTARY_PROFILE`

### Requirement: Path resolution
Relative paths in the manifest (icon, payloads, and any other file references) SHALL resolve against the directory containing the manifest, not the current working directory.

#### Scenario: Build started from another directory
- **WHEN** the manifest at `repo/installer.json` references `assets/icon.png` and the builder runs with working directory `/`
- **THEN** the icon is read from `repo/assets/icon.png`

### Requirement: Strict parsing
The builder MUST reject a manifest that is not valid JSON, that contains a property it does not recognize, or whose values have the wrong type. Errors SHALL name the offending property by its JSON path. Validation SHALL report all problems it finds, not only the first.

#### Scenario: Misspelled property
- **WHEN** a macOS section contains `"identiy": "ScreenRec Dev"`
- **THEN** validation fails with an error naming the unknown property `macos.identiy`

#### Scenario: Several problems
- **WHEN** a manifest has an invalid `id` and a duplicate target name
- **THEN** validation reports both errors
