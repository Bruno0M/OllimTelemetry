#!/usr/bin/env bash
set -euo pipefail

REPO="Bruno0M/OllimTelemetry"

# ── Platform detection ────────────────────────────────────────────────────────

OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
  Linux)  rid_os="linux" ;;
  Darwin) rid_os="osx" ;;
  *)
    echo "error: unsupported OS: $OS" >&2
    echo "       Supported platforms: Linux, macOS" >&2
    exit 1
    ;;
esac

case "$ARCH" in
  x86_64)        rid_arch="x64" ;;
  aarch64|arm64) rid_arch="arm64" ;;
  *)
    echo "error: unsupported architecture: $ARCH" >&2
    echo "       Supported architectures: x86_64, aarch64/arm64" >&2
    exit 1
    ;;
esac

RID="${rid_os}-${rid_arch}"

# ── Fetch latest version ──────────────────────────────────────────────────────

echo "Fetching latest release..."

VERSION=$(curl -fsSL "https://api.github.com/repos/${REPO}/releases/latest" \
  | grep '"tag_name"' \
  | sed 's/.*"tag_name": *"\(.*\)".*/\1/')

if [ -z "$VERSION" ]; then
  echo "error: could not determine latest version from GitHub" >&2
  exit 1
fi

echo "Installing ollim ${VERSION} (${RID})..."

# ── Download & extract ────────────────────────────────────────────────────────

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

URL="https://github.com/${REPO}/releases/download/${VERSION}/ollim-${RID}.tar.gz"

if ! curl -fsSL "$URL" -o "$TMP/ollim.tar.gz"; then
  echo "error: failed to download $URL" >&2
  echo "       Check https://github.com/${REPO}/releases for available platforms." >&2
  exit 1
fi

tar -xzf "$TMP/ollim.tar.gz" -C "$TMP"

# ── Choose install directory ──────────────────────────────────────────────────
# Prefer /usr/local/bin; fall back to ~/.local/bin (no sudo required).

if [ -w "/usr/local/bin" ]; then
  INSTALL_DIR="/usr/local/bin"
  SUDO=""
elif command -v sudo &>/dev/null && sudo -n true 2>/dev/null; then
  INSTALL_DIR="/usr/local/bin"
  SUDO="sudo"
else
  INSTALL_DIR="${HOME}/.local/bin"
  SUDO=""
  mkdir -p "$INSTALL_DIR"
fi

# ── Install binary + SQLite library ──────────────────────────────────────────
# The native library must live alongside the binary so the RPATH $ORIGIN resolves.

$SUDO install -m 755 "$TMP/ollim" "$INSTALL_DIR/ollim"

for lib in libe_sqlite3.so libe_sqlite3.dylib; do
  if [ -f "$TMP/$lib" ]; then
    $SUDO install -m 644 "$TMP/$lib" "$INSTALL_DIR/$lib"
  fi
done

# ── PATH notice ───────────────────────────────────────────────────────────────

echo ""
echo "✓ ollim ${VERSION} installed to ${INSTALL_DIR}/ollim"

if [[ ":$PATH:" != *":${INSTALL_DIR}:"* ]]; then
  echo ""
  echo "  ${INSTALL_DIR} is not in your PATH."
  echo "  Add this to your shell profile (~/.bashrc, ~/.zshrc, etc.):"
  echo ""
  echo "    export PATH=\"\$PATH:${INSTALL_DIR}\""
  echo ""
fi

echo "  Run: ollim start"
