# The AppImage build environment for Installer.Cli (embedded in Installer.Dao). Every download is pinned and verified
# against its SHA-256, so building an AppImage never downloads anything. The builder tags the image with a hash of this
# file: editing it (for example to update a pin) produces a new image automatically.
#
# appimagetool runs natively on the host's architecture and packs AppImages for either target architecture with the
# matching type-2 runtime (runtime-x86_64, runtime-aarch64) passed via --runtime-file.
FROM debian:bookworm-slim@sha256:3783cc01769c7b2b1b83a5c5ad96c815348e28ed7da68e2e3687004faa906251

ARG APPIMAGETOOL_VERSION=1.9.1
ARG RUNTIME_VERSION=20251108

RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl file \
 && rm -rf /var/lib/apt/lists/*

RUN set -eu; \
    arch="$(uname -m)"; \
    case "$arch" in \
      x86_64)  tool_sha=ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0 ;; \
      aarch64) tool_sha=f0837e7448a0c1e4e650a93bb3e85802546e60654ef287576f46c71c126a9158 ;; \
      *) echo "Unsupported build host architecture: $arch" >&2; exit 1 ;; \
    esac; \
    mkdir -p /opt/appimage /work; \
    cd /opt/appimage; \
    curl -fsSL -o appimagetool.AppImage "https://github.com/AppImage/appimagetool/releases/download/${APPIMAGETOOL_VERSION}/appimagetool-${arch}.AppImage"; \
    echo "${tool_sha}  appimagetool.AppImage" | sha256sum -c -; \
    chmod +x appimagetool.AppImage; \
    ./appimagetool.AppImage --appimage-extract > /dev/null; \
    mv squashfs-root appimagetool; \
    rm appimagetool.AppImage; \
    curl -fsSL -o runtime-x86_64 "https://github.com/AppImage/type2-runtime/releases/download/${RUNTIME_VERSION}/runtime-x86_64"; \
    echo "2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d  runtime-x86_64" | sha256sum -c -; \
    curl -fsSL -o runtime-aarch64 "https://github.com/AppImage/type2-runtime/releases/download/${RUNTIME_VERSION}/runtime-aarch64"; \
    echo "00cbdfcf917cc6c0ff6d3347d59e0ca1f7f45a6df1a428a0d6d8a78664d87444  runtime-aarch64" | sha256sum -c -

WORKDIR /work
