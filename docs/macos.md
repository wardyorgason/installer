# macOS package

A macOS target produces a signed `<name>.app`, packaged as `<name>-<displayVersion>-<target>.dmg` (default) and/or
`.zip`, optionally notarized. It needs a macOS host with the tools that ship with macOS / Command Line Tools
(`codesign`, `security`, `xattr`, `hdiutil`, `ditto`, `xcrun`). The flow generalizes screen-rec's `New-MacApp.ps1`.

## The bundle

```
<name>.app/Contents/
  Info.plist
  MacOS/
    <main executable>        from the payload (found by the profile)
    *.dll, *.dylib, *.json   every other top-level payload file
    wwwroot -> ../Resources/wwwroot     one relative link per payload folder
  Resources/
    <name>.icns              from the manifest's PNG (128–1024 px, @2x included)
    wwwroot/                 each payload folder, moved here
```

codesign treats any folder in `Contents/MacOS` as a nested bundle and fails ("bundle format unrecognized"), so each
top-level payload folder moves to `Contents/Resources`, and a relative symbolic link keeps it reachable at its original
path next to the executable. .NET's `runtimes/`, satellite-assembly folders and `wwwroot/` keep working either way,
and apps that already look in `Contents/Resources` (screen-rec's Blazor host) keep working too. A payload folder named
like the icon file fails the build (`bundle.name-collision`).

`Info.plist` is generated: name, display name, identifier (`id`), executable, `APPL`, `CFBundleShortVersionString`
(first three parts of `version`), `CFBundleVersion` (all of `version`), icon and high-resolution support, followed by
the manifest's `macos.infoPlist` keys (usage descriptions, `LSMinimumSystemVersion`, …). The identifier, executable and
version keys can't be overridden.

## Signing

Every macOS target names `macos.identity`, a code-signing identity in the build user's keychain. Before any target
builds, the builder checks it with `security find-identity -v -p codesigning` and fails the target (listing what is
available) if it's missing. Signing is inside out:

1. `xattr -cr` the bundle (quarantine and Finder attributes make codesign refuse with "detritus not allowed").
2. Every regular file in `Contents/MacOS` except the main executable (links aren't followed).
3. Every Mach-O file in `Contents/Resources` (native libraries in moved folders).
4. The bundle, with `--identifier <id>` and the entitlements.
5. `codesign --verify --strict --deep`.

Every signature uses the **hardened runtime**, whatever the identity, so local builds behave like release builds. A
`Developer ID Application:` identity also gets a secure timestamp (`--timestamp`); other identities use
`--timestamp=none`, because Apple's timestamp server only serves Apple-issued certificates.

**Entitlements** are the profile's defaults plus `macos.entitlements`. The `dotnet` profile adds
`com.apple.security.cs.allow-jit`, `allow-unsigned-executable-memory` and `disable-library-validation`; set one to
`false` to remove it. Under the hardened runtime, device access needs its entitlement, for example
`com.apple.security.device.audio-input` for the microphone (plus the usage description in `infoPlist`).

TCC permissions (Screen Recording, Microphone) are tied to the app's identifier and signing certificate, so a stable
identity, such as a self-signed one kept in the keychain, keeps them across builds.

## Outputs

- `dmg`: a compressed disk image holding the app and an `Applications` link for drag-to-install, signed with the same
  identity.
- `zip`: the app zipped with `ditto --sequesterRsrc --keepParent`, which keeps the signature, permissions and links.

## Notarization

With `macos.notarize.keychainProfile`, the builder submits the target's first output (the `.dmg` if built, otherwise
the `.zip`) with `xcrun notarytool submit --wait`, then staples the ticket to the `.dmg` and to the `.app` and creates
the `.zip` from the stapled app. A result other than `Accepted` saves the notarization log in the work directory and
fails the target (`notarize.rejected`) with the log's path. Notarization requires a Developer ID identity; the manifest
is rejected otherwise.

Only the `.dmg` and the `.app` in the `.zip` are stapled; the app inside the `.dmg` is checked online by Gatekeeper
the first time it opens, which is fine for downloads.

## One-time setup on each Mac that builds

1. **Identity.** Either a self-signed one (Keychain Access › Certificate Assistant › Create a Certificate, type Code
   Signing, then Trust › Code Signing: Always Trust) or a `Developer ID Application` certificate from the Apple
   Developer Program, installed in the build user's login keychain.
2. **Unattended access.** Let codesign use the key without a dialog:
   `security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "<login password>" ~/Library/Keychains/login.keychain-db`
3. **Notarization (optional).** Once, as the build user:
   `xcrun notarytool store-credentials <profile> --apple-id <id> --team-id <team> --password <app-specific password>`,
   then use `<profile>` as `macos.notarize.keychainProfile`.
4. Check: `security find-identity -v -p codesigning` lists the identity.

The keychain must be unlocked while building (a logged-in session, or `security unlock-keychain`); see
`docs/ci-jenkins.md` for the Jenkins agent.

## Troubleshooting

- `identity.missing`: the identity isn't in this user's keychain or isn't trusted for code signing; the message lists
  what is available.
- `sign.failed` with `errSecInternalComponent`: the keychain is locked.
- Build hangs while signing: codesign is waiting for a keychain dialog; do setup step 2.
- `notarize.rejected`: open the log named in the message; usually an unsigned binary or a missing hardened runtime in
  something the payload ships.
