# Windows setup executable

A Windows target produces `<name>-<displayVersion>-<target>-setup.exe`, built with NSIS (`makensis` 3.08 or later).
NSIS runs on macOS and Linux, so the Jenkins Mac agent builds Windows installers too (`brew install makensis`).

## What the installer does

- Installs for the current user only, without an administrator (UAC) prompt, into `%LOCALAPPDATA%\Programs\<name>`.
- Creates the Start Menu shortcut `<name>` and offers to start the app when the interactive install finishes.
- Registers the app under `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\<id>`, so it appears in
  *Settings › Apps › Installed apps* with its publisher and `displayVersion` (no Modify or Repair).
- Includes `uninstall.exe`, which removes the install folder, the shortcut and the registration.
- `/S` runs the setup or the uninstaller silently.
- **Upgrades in place:** when the same `id` is already installed, the setup first runs the previous uninstaller
  silently, so files the new version no longer ships are removed. Installing an older version over a newer one works
  the same way.
- The setup's version information comes from the manifest: product name, publisher (`CompanyName`), description,
  `version` padded to four parts (file version) and `displayVersion` (product version).

The app must not be running during an upgrade or uninstall; NSIS shows its usual Retry/Cancel prompt for locked files.

## Signing (optional)

Real Authenticode signing isn't built in. Instead, `windows.signCommand` names any signing tool, with `{file}`
standing for the file to sign:

```jsonc
"windows": {
  "signCommand": ["osslsigncode", "sign", "-pkcs12", "${env:WINDOWS_CERT}", "-pass", "${env:WINDOWS_CERT_PASSWORD}",
                  "-t", "http://timestamp.digicert.com", "-in", "{file}", "-out", "{file}.signed"]
}
```

The command runs for the app's main executable (before packaging), for the uninstaller (before NSIS embeds it) and for
the finished setup executable. A command that signs to a new file must also move it back; wrap it in a small script if
the tool can't sign in place. A non-zero exit fails the target with the tool's error output (`sign.failed`). Without
`signCommand`, nothing is signed and Windows SmartScreen will warn on first run.

## Manual checklist

Run once per change to the installer script, on a Windows 10/11 machine, **as a standard (non-admin) user**, with a
setup built from `Samples.DotnetApp` (self-contained, so no .NET install is needed). Record the result below.

1. Run the setup: no UAC prompt appears; files land in `%LOCALAPPDATA%\Programs\Samples`; the finish page offers to
   run the app, and it starts.
2. The Start Menu has `Samples`, which starts the app.
3. *Installed apps* lists `Samples` with the publisher and display version.
4. Build a newer version whose payload drops one file; install it over the first. The dropped file is gone, the app
   is at the new version, and *Installed apps* shows one entry with the new display version.
5. `setup.exe /S` installs without any UI.
6. Uninstall from *Installed apps*: the folder, the shortcut and the entry are removed.
7. `"%LOCALAPPDATA%\Programs\Samples\uninstall.exe" /S` uninstalls silently.

| Date | Builder version | Windows | Result |
|---|---|---|---|
| | | | Not run yet |
