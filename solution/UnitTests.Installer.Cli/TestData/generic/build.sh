#!/bin/sh
# Cross-compiles the committed sample binaries. Only needed when src/main.go changes; the tests use the committed files.
set -e
cd "$(dirname "$0")/src"
GOOS=windows GOARCH=amd64 CGO_ENABLED=0 go build -trimpath -ldflags "-s -w" -o ../windows-x64/generic-sample.exe .
GOOS=darwin  GOARCH=arm64 CGO_ENABLED=0 go build -trimpath -ldflags "-s -w" -o ../macos-arm64/generic-sample .
GOOS=linux   GOARCH=amd64 CGO_ENABLED=0 go build -trimpath -ldflags "-s -w" -o ../linux-x64/generic-sample .
