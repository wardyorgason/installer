# Linux AppImage

A Linux target produces `<name>-<displayVersion>-<target>.AppImage`: one executable file for the target architecture
(x86-64 for `x64`, AArch64 for `arm64`) that starts the app and passes its arguments through. There is no install step;
users `chmod +x` it (the file already carries the bit) and run it.

## How it is built

The AppImage is always packed inside a container, so any host with a Docker engine builds it, including the Mac
agent. The builder:

1. Lays out an AppDir in its work directory: the payload under `usr/lib/<id>/`, `AppRun` linked to the main executable,
   `<id>.desktop` (name, description, `linux.categories`, `displayVersion`), and a 256 px `<id>.png` icon with
   `.DirIcon` linked to it.
2. Uses the image `installer-appimage:<hash>`, where `<hash>` is the first 12 hex digits of the SHA-256 of the
   Dockerfile embedded in the builder. If that image isn't on the host, it builds it once (`docker build -` with the
   Dockerfile on stdin; no build context).
3. Creates a container, copies the AppDir in with `docker cp`, runs `appimagetool` with the target architecture's
   runtime (`--runtime-file`), copies the AppImage out, and removes the container (also when packing fails).

The builder never bind-mounts host folders, so it works with Colima's default mounts, Docker Desktop, or a remote engine.

## Pinned tooling

The Dockerfile (`solution/Installer.Dao/Linux/appimage.Dockerfile`) pins:

| What | Version | Checked by |
|---|---|---|
| Base image | `debian:bookworm-slim@sha256:…` (multi-arch index digest) | Docker |
| `appimagetool` | 1.9.1, for the host's architecture (`x86_64` or `aarch64`) | SHA-256 in the Dockerfile |
| AppImage type-2 runtime | 20251108, both `runtime-x86_64` and `runtime-aarch64` | SHA-256 in the Dockerfile |

Downloads happen only while the image is built. A checksum mismatch fails the image build, and the Linux target
fails with its output. Building an AppImage from a cached image needs no network.

`appimagetool` runs natively on the host's architecture and packs either target architecture, so an arm64 Mac builds
x86-64 AppImages (and vice versa) without emulation.

Note: the files attached to the `appimagetool` 1.9.1 release were replaced after it was published (the binary reports
itself as a December 2025 continuous build). The pinned checksum is of the file served today; if the release changes
again, the image build fails with a checksum error, and the pin needs a deliberate update.

## Updating the pins

1. Pick new releases from <https://github.com/AppImage/appimagetool/releases> and
   <https://github.com/AppImage/type2-runtime/releases> (use a dated tag, not `continuous`).
2. Download each file and run `sha256sum`; compare with the digest GitHub shows for the asset.
3. Update the version `ARG`s and the checksums in the Dockerfile. For the base image, run
   `docker pull debian:bookworm-slim && docker inspect --format '{{index .RepoDigests 0}}' debian:bookworm-slim`.
4. Run the Linux integration tests (`pwsh scripts/Test.ps1 -IncludeIntegration`). The changed file hashes to a new
   tag, so every host builds the new image on its next AppImage build; old `installer-appimage:*` images can be removed
   with `docker image rm`.

## Docker on the Mac agent (Colima)

Docker Desktop needs a logged-in GUI session, which an unattended Jenkins agent may not have. Colima runs the engine
in a small VM:

```bash
brew install colima docker
colima start --cpu 4 --memory 6 --arch aarch64 --vm-type vz
brew services start colima        # start it again at login
docker info                       # must succeed as the Jenkins user
```

Without a reachable engine, Linux targets fail their preflight check (`docker.unreachable`) and the other targets still
build.

## Checking an AppImage without FUSE

AppImages normally mount themselves with FUSE. To inspect or run one in a container (as the integration tests do):

```bash
./App.AppImage --appimage-extract      # creates squashfs-root/
./squashfs-root/AppRun --help
```
