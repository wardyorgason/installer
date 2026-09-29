# Spec Delta

## Purpose

Produces a per-user Windows setup executable that installs, upgrades and uninstalls the app without administrator rights, built on any supported host.

## ADDED Requirements

### Requirement: Setup executable output
A Windows target SHALL produce one setup executable named `<name>-<displayVersion>-<target-name>-setup.exe`, with the app icon as its icon and version information set from the manifest (product name, publisher, description, and the `version` padded to four parts as the file and product version).

#### Scenario: Windows target built
- **WHEN** app `ScreenRec`, display version `1.0.0-b12`, version `1.0.0.12` and target `windows-x64` are built
- **THEN** `ScreenRec-1.0.0-b12-windows-x64-setup.exe` is produced, with file version `1.0.0.12`

#### Scenario: Three-part version padded
- **WHEN** `version` is `2.1.0`
- **THEN** the setup executable's file version is `2.1.0.0`

### Requirement: Per-user install without elevation
The setup executable MUST run without an administrator (UAC) prompt and SHALL install the payload into `%LOCALAPPDATA%\Programs\<name>`. It SHALL support a silent install with `/S`.

#### Scenario: Standard user installs
- **WHEN** a standard (non-admin) user runs the setup executable
- **THEN** no elevation prompt appears and the payload is installed under `%LOCALAPPDATA%\Programs\<name>`

#### Scenario: Silent install
- **WHEN** the setup executable runs with `/S`
- **THEN** it installs without showing any UI

### Requirement: Start Menu shortcut and launch
The installer SHALL create a Start Menu shortcut named `<name>` that starts the main executable, and SHALL offer to start the app when the interactive install finishes.

#### Scenario: Shortcut created
- **WHEN** installation completes
- **THEN** the Start Menu contains `<name>`, which starts the installed main executable

### Requirement: Installed apps registration
The installer SHALL register the app for the current user under the uninstall key named by the app's `id`, with its display name, display version (`displayVersion`), publisher, icon, install location, estimated size, and uninstall commands (interactive and quiet). The entry SHALL NOT offer Modify or Repair.

#### Scenario: Visible in Installed apps
- **WHEN** installation completes
- **THEN** Windows *Installed apps* lists `<name>` with the publisher and `displayVersion`, and uninstalling from there runs the app's uninstaller

### Requirement: Uninstaller
The installer SHALL include an uninstaller that removes the installed files, the install directory, the Start Menu shortcut and the registration, without an administrator prompt. It SHALL support a silent uninstall with `/S`.

#### Scenario: Uninstall
- **WHEN** a user uninstalls the app
- **THEN** the install directory, the shortcut and the *Installed apps* entry are removed

### Requirement: Upgrade in place
When the app (same `id`) is already installed for the user, the installer SHALL remove the previous installation with its uninstaller, silently, before installing, so files the new version no longer ships do not remain. Installing an older version over a newer one SHALL be allowed and behave the same way.

#### Scenario: Newer version over older
- **WHEN** version 1.1 is installed over 1.0, and 1.0 contained a file that 1.1 does not
- **THEN** after installation that file is gone, the app is at 1.1, and *Installed apps* shows a single entry with 1.1's display version

### Requirement: Optional Authenticode signing hook
When `windows.signCommand` is configured, it is an argument list in which `{file}` stands for the file to sign. The builder SHALL run it on the payload's main executable before packaging, on the uninstaller, and on the setup executable. When no command is configured, nothing SHALL be signed. A command without a `{file}` placeholder MUST fail validation.

#### Scenario: Signing configured
- **WHEN** `signCommand` is configured
- **THEN** the command runs once each for the main executable, the uninstaller and the setup executable, each with `{file}` replaced by that file's path

#### Scenario: No signing configured
- **WHEN** `signCommand` is absent
- **THEN** the setup executable is produced unsigned and no signing command runs

#### Scenario: Placeholder missing
- **WHEN** `signCommand` does not contain `{file}`
- **THEN** validation fails with an error that the command must contain `{file}`
