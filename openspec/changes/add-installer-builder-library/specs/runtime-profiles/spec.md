# Spec Delta

## Purpose

Adds stack-specific inference and defaults on top of the stack-agnostic package definition: the `generic` profile for any prebuilt app, and the `dotnet` profile for .NET publish output.

## ADDED Requirements

### Requirement: Generic profile
The `generic` profile SHALL add no inference and no defaults. It MUST require the manifest's `executable`, a path relative to the payload root, and SHALL fail the target when that file does not exist in the prepared payload.

#### Scenario: Executable named
- **WHEN** the profile is `generic` and `executable` is `bin/mytool`, which exists in the payload
- **THEN** `bin/mytool` is the main executable

#### Scenario: Executable missing from the manifest
- **WHEN** the profile is `generic` and no `executable` is declared
- **THEN** validation fails with an error that `generic` requires `executable`

#### Scenario: Executable not in the payload
- **WHEN** `executable` is `mytool` and the payload has no such file
- **THEN** the target fails with an error naming the missing file

### Requirement: .NET main executable detection
The `dotnet` profile SHALL find the app by its `<app>.runtimeconfig.json` at the payload root and SHALL use the apphost next to it (`<app>.exe` for Windows targets, `<app>` otherwise) as the main executable. When the manifest names `executable`, the profile SHALL use `<executable>.runtimeconfig.json` instead of searching. The profile SHALL fail the target when there is no runtimeconfig file, when there is more than one and no `executable` is named, or when the apphost is missing.

#### Scenario: Standard publish output
- **WHEN** the payload root contains `ScreenRec.runtimeconfig.json` and `ScreenRec` (for a macOS target)
- **THEN** the main executable is `ScreenRec`, without the caller naming it

#### Scenario: Single-file or NativeAOT output
- **WHEN** the payload root contains no `*.runtimeconfig.json`
- **THEN** the target fails with an error that single-file and NativeAOT output are not detected and that the `generic` profile can package them

#### Scenario: Several apps in one payload
- **WHEN** the payload root contains `App.runtimeconfig.json` and `Helper.runtimeconfig.json` and no `executable` is named
- **THEN** the target fails with an error listing both and asking for `executable`

#### Scenario: Published without an apphost
- **WHEN** `App.runtimeconfig.json` exists but `App.exe` does not (for a Windows target)
- **THEN** the target fails with an error that the payload has no apphost and must be published with an apphost

### Requirement: .NET deployment mode and runtime warning
The `dotnet` profile SHALL determine whether the payload is self-contained or framework-dependent from its runtimeconfig file. For a framework-dependent payload it SHALL add a warning to the target's result naming each required shared framework and its version.

#### Scenario: Framework-dependent payload
- **WHEN** the runtimeconfig declares framework `Microsoft.NETCore.App` version `10.0.0`
- **THEN** the target succeeds with a warning that users need the `Microsoft.NETCore.App` 10.0 runtime installed

#### Scenario: Self-contained payload
- **WHEN** the runtimeconfig lists included frameworks
- **THEN** the target has no runtime warning

### Requirement: .NET runtime identifier consistency
When the payload's dependency manifest (`<app>.deps.json`) names a runtime identifier, the `dotnet` profile MUST fail the target if that identifier's OS or architecture differs from the target's. A payload without a runtime identifier (portable publish) SHALL be accepted, subject to the main executable check in build-orchestration.

#### Scenario: Mismatched runtime identifier
- **WHEN** a macOS arm64 target's payload has a deps file for runtime identifier `win-x64`
- **THEN** the target fails with an error naming `win-x64` and the target's OS and architecture

#### Scenario: Portable publish
- **WHEN** the deps file names no runtime identifier and the apphost matches the target
- **THEN** the profile accepts the payload

### Requirement: .NET macOS entitlements
For macOS targets, the `dotnet` profile SHALL supply the default entitlements `com.apple.security.cs.allow-jit`, `com.apple.security.cs.allow-unsigned-executable-memory` and `com.apple.security.cs.disable-library-validation`, each set to true. Caller entitlements SHALL be merged over the defaults; a caller value of `false` SHALL remove a default entitlement.

#### Scenario: Defaults plus caller entitlement
- **WHEN** the profile is `dotnet` and the manifest adds `com.apple.security.device.audio-input: true`
- **THEN** the app is signed with the three defaults and `audio-input`

#### Scenario: Caller removes a default
- **WHEN** the manifest sets `com.apple.security.cs.disable-library-validation: false`
- **THEN** the app is signed without that entitlement
