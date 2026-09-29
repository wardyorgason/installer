# Jenkins

Two pipelines, both in `jenkins/`, both on the Mac agent (label `dotnet10 && macos`). Every stage calls a script under
`scripts/`, so a failing stage can be rerun by hand with the same command.

| Pipeline | File | What it does | Results |
|---|---|---|---|
| Test | `jenkins/test.Jenkinsfile` | Clean → every `UnitTests.*` project, including `Integration` and `EndToEnd` tests | JUnit on the build page, `dist/test-results/**` archived |
| Build | `jenkins/build.Jenkinsfile` | Clean → Build (Release, versioned) → Publish the builder zip | `dist/Installer-<version>.zip`, `dist/version.txt` |

The Mac agent is the only host that can run every test: macOS packaging needs a Mac, and the same agent runs
`makensis` (Windows installers) and Docker (Linux AppImages). Tests whose host requirement is missing call
`Assert.Ignore` with the reason, so a dev box without Docker or a signing identity still passes.

## 1. Tools on the Mac (as the Jenkins user)

```bash
brew install --cask dotnet-sdk        # .NET 10 SDK
brew install --cask powershell        # pwsh 7
brew install git makensis             # makensis 3.08 or later
brew install colima docker            # a headless Docker engine; Docker Desktop needs a GUI session
dotnet --version && pwsh --version && makensis -VERSION
```

The Jenkinsfiles prepend `/opt/homebrew/bin` and `/usr/local/share/dotnet` to `PATH`, which a launchd-started Jenkins
lacks. The builder itself also looks in `/opt/homebrew/bin` and `/usr/local/bin`.

## 2. Docker engine (Colima)

Colima runs Docker in a small Linux VM without a desktop session:

```bash
colima start --cpu 4 --memory 6 --arch aarch64 --vm-type vz
brew services start colima            # restart it at login
docker info                           # must work for the Jenkins user
```

The builder never bind-mounts host folders (it copies files into and out of the container), so Colima's default
mounts are enough. The AppImage image is built on first use and cached; see `docs/linux.md`.

## 3. How Jenkins runs on the Mac

Run Jenkins as a **LaunchAgent of a logged-in user** (`brew services start jenkins-lts` does this), not as a
LaunchDaemon: `codesign` needs that user's unlocked login keychain. Enable automatic login so a reboot brings it back.
If you must run as a daemon, set the `UNLOCK_KEYCHAIN` build parameter (with the credential in step 5).

## 4. Signing identities (once, as the Jenkins user)

The macOS integration and end-to-end tests sign with the identity named in the node environment variable
`INSTALLER_TEST_IDENTITY` (for example the `ScreenRec Dev` self-signed identity screen-rec already uses). Without it,
those tests are ignored.

1. Create a self-signed identity if the agent has none: Keychain Access › Certificate Assistant › Create a
   Certificate, type Code Signing, then set Trust › Code Signing to Always Trust.
2. Let codesign use its key without a dialog, which would otherwise stall an unattended build:
   ```bash
   security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "<login password>" ~/Library/Keychains/login.keychain-db
   ```
3. Check: `security find-identity -v -p codesigning` lists the identity.

Optional notarization test (the `NOTARIZE` parameter of the test job): set `INSTALLER_TEST_DEVELOPER_ID` to a
`Developer ID Application: …` identity and `INSTALLER_TEST_NOTARY_PROFILE` to a profile created with
`xcrun notarytool store-credentials`. See `docs/macos.md`.

## 5. Jenkins configuration

1. **Plugins:** Git, Pipeline, Credentials Binding (the default set). `pwsh` steps need only `pwsh` on the PATH.
2. **Label the node** `dotnet10 macos`, and set `INSTALLER_TEST_IDENTITY` (and the optional notarization variables)
   under the node's environment variables.
3. **Credentials** (only for `UNLOCK_KEYCHAIN`): Secret text, ID `mac-login-keychain`, the Jenkins user's login password.

## 6. Jobs

| Job name | Script path | Branches |
|---|---|---|
| `installer-test` | `jenkins/test.Jenkinsfile` | All branches and pull requests |
| `installer-build` | `jenkins/build.Jenkinsfile` | `master` only |

Both are Multibranch Pipelines. The build job's `BUILD_NUMBER` becomes the builder's version suffix (`0.1.0-b12`).

## Troubleshooting

- **Signing tests ignored with "INSTALLER_TEST_IDENTITY is not set":** set it on the node (step 5.2).
- **Build hangs while signing:** codesign is waiting on a keychain dialog; do step 4.2.
- **`errSecInternalComponent` from codesign:** the keychain is locked; log in, or use `UNLOCK_KEYCHAIN`.
- **Linux tests ignored with "Docker engine not reachable":** `colima start` as the Jenkins user, then `docker info`.
- **Windows tests ignored with "makensis was not found":** `brew install makensis`.
