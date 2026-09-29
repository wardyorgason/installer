# Jenkins

Three pipelines, all in `jenkins/`, all on the Mac agent (label `dotnet10 && macos`). Every stage calls a script under
`scripts/`, so a failing stage can be rerun by hand with the same command.

| Pipeline | File | When | What it does | Results |
|---|---|---|---|---|
| Test | `jenkins/test.Jenkinsfile` | every branch and PR | Clean → every `UnitTests.*` project, including `Integration` and `EndToEnd` tests | JUnit on the build page, `dist/test-results/**` archived |
| Build | `jenkins/build.Jenkinsfile` | every branch change | Clean → Build (Release, versioned) → Publish the builder zip | `dist/Installer-<version>.zip` (+ `.sha256`), `dist/release.json`, `dist/version.txt` |
| Release | `jenkins/release.Jenkinsfile` | by hand only | Copies a Build run's artifacts (no rebuild), checks the zip's SHA-256, publishes it as a GitHub release | the release, tagged `v<version>` on the commit the zip was built from |

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
4. **Plugin for releases:** Manage Jenkins › Plugins › Available › **Copy Artifact** (install, no restart needed). The
   Release job uses it to take the zip from a Build run.

## 6. Jobs

| Job name | Type | Script path | Branches |
|---|---|---|---|
| `installer-test` | Multibranch Pipeline | `jenkins/test.Jenkinsfile` | All branches and pull requests |
| `installer-build` | Multibranch Pipeline | `jenkins/build.Jenkinsfile` | All branches |
| `installer-release` | Pipeline | `jenkins/release.Jenkinsfile` | `master` (the scripts it runs); no build triggers |

The names matter: the Release job copies from `installer-build/<branch>`, and the Build job only lets a job named
`installer-release` copy its artifacts. Each branch of `installer-build` counts its own build numbers, and that number
becomes the version suffix (`0.1.0-b12`), so a release is identified by branch and build number.

For `installer-release`: New Item › **Pipeline** › Pipeline › Definition: *Pipeline script from SCM*, Git, the
repository URL and read credentials, Branch Specifier `*/master`, Script Path `jenkins/release.Jenkinsfile`. Leave
every build trigger off. Run it once and cancel it so Jenkins loads its parameters; afterwards *Build with Parameters*
shows them.

## 7. GitHub release credential (once)

The Release job publishes with a GitHub token stored in Jenkins; no `gh` CLI is involved.

1. **Create a fine-grained token** on GitHub: your avatar › Settings › Developer settings › Personal access tokens ›
   **Fine-grained tokens** › Generate new token.
   - Token name: `jenkins-installer-release`; Expiration: up to a year (note the date: releases fail with `401` after it).
   - Resource owner: your account; Repository access: **Only select repositories** › the installer repository.
   - Repository permissions: **Contents: Read and write** (Metadata: Read-only is added automatically). Nothing else.
   - Generate, and copy the token (it is shown once).
2. **Store it in Jenkins:** Manage Jenkins › Credentials › System › Global credentials (unrestricted) › **Add
   Credentials**.
   - Kind: **Secret text**; Scope: Global.
   - Secret: the token.
   - ID: **`github-installer-release`** (the Release job looks for exactly this ID).
   - Description: `GitHub token: installer releases (expires <date>)`.
3. **Check it** from a terminal before the first release (expects the repository's JSON, not `Bad credentials`):
   ```bash
   curl -s -H "Authorization: Bearer <token>" https://api.github.com/repos/<owner>/<repo> | head -5
   ```

To rotate: generate a new token the same way, then Credentials › `github-installer-release` › Update › replace the
secret.

## 8. Releasing

1. Push to the branch and let `installer-build` build it (for a normal release, `master`).
2. `installer-release` › **Build with Parameters**:
   - `SOURCE_BRANCH`: `master`; `SOURCE_BUILD`: the `installer-build` run to release (empty: its last successful run).
   - `PRERELEASE`: tick it for anything that isn't a normal `master` release.
   - `GITHUB_REPOSITORY`: `owner/name` of the repository.
3. The run's description links the release. It holds `Installer-<version>.zip` and `Installer-<version>.zip.sha256`.

The release refuses to overwrite: if `v<version>` already has its assets, bump `solution/version.json` or release a
later build. If an upload failed halfway, run it again with the same parameters: it completes the missing assets.
Locally, the same script works with a token in `GITHUB_TOKEN`:
`pwsh scripts/Publish.ps1; GITHUB_TOKEN=… pwsh scripts/Publish-Release.ps1 -Repository owner/name`.

## Troubleshooting

- **Signing tests ignored with "INSTALLER_TEST_IDENTITY is not set":** set it on the node (step 5.2).
- **Build hangs while signing:** codesign is waiting on a keychain dialog; do step 4.2.
- **`errSecInternalComponent` from codesign:** the keychain is locked; log in, or use `UNLOCK_KEYCHAIN`.
- **Linux tests ignored with "Docker engine not reachable":** `colima start` as the Jenkins user, then `docker info`.
- **Windows tests ignored with "makensis was not found":** `brew install makensis`.
- **Release: `Unable to find project for artifact copy: installer-build/master`:** the Build job has another name, or
  that branch has no successful build yet.
- **Release: `… is not permitted to copy artifacts`:** the Release job isn't named `installer-release`; rename it or
  change `copyArtifactPermission` in `jenkins/build.Jenkinsfile`.
- **Release: `401 Bad credentials` / `403 Resource not accessible by personal access token`:** the token expired, or it
  lacks Contents: Read and write on that repository (step 7).
- **Release: `422 Validation Failed`:** usually the commit isn't on GitHub (push the branch) or the tag exists on another
  commit.
