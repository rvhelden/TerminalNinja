#!/usr/bin/env bash
# Locates SDL3 for TerminalNinja.Skia on Linux/macOS and places it in the
# runtimes/<rid>/native/ layout TerminalNinja.Skia's MSBuild targets expect.
#
# Unlike Windows where we can download a pre-built zip from libsdl-org's GitHub releases,
# Linux/macOS don't have an official pre-built distribution channel — the recommended
# approach is to install SDL3 via the platform package manager, then point this script
# at it.
#
# Usage:
#   ./scripts/get-sdl3.sh                          # auto-detect platform, copy to ./runtimes/<rid>/native/
#   ./scripts/get-sdl3.sh /path/to/MyApp           # copy into MyApp's runtimes/ layout
#   ./scripts/get-sdl3.sh --rid linux-x64          # explicit RID
#
# Install hints by platform:
#   Debian / Ubuntu:    sudo apt install libsdl3-0
#   Fedora:             sudo dnf install SDL3
#   Arch:               sudo pacman -S sdl3
#   macOS (Homebrew):   brew install sdl3
#
# After installing system-wide, run this script to copy the shared library into the
# expected layout. The MSBuild targets will then copy it to your app's bin/ on build.

set -euo pipefail

output_dir="."
rid=""
explicit_lib=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --rid) rid="$2"; shift 2 ;;
        --lib) explicit_lib="$2"; shift 2 ;;
        -h|--help) sed -n '2,/^$/p' "$0"; exit 0 ;;
        *) output_dir="$1"; shift ;;
    esac
done

# Auto-detect RID if not provided.
if [[ -z "$rid" ]]; then
    case "$(uname -s)" in
        Linux)
            case "$(uname -m)" in
                x86_64)  rid="linux-x64" ;;
                aarch64) rid="linux-arm64" ;;
                *) echo "Unknown Linux arch $(uname -m); pass --rid explicitly" >&2; exit 1 ;;
            esac
            ;;
        Darwin)
            case "$(uname -m)" in
                x86_64)  rid="osx-x64" ;;
                arm64)   rid="osx-arm64" ;;
                *) echo "Unknown macOS arch $(uname -m); pass --rid explicitly" >&2; exit 1 ;;
            esac
            ;;
        *) echo "Unsupported OS $(uname -s); use the Windows .ps1 script or pass --rid + --lib explicitly" >&2; exit 1 ;;
    esac
fi

lib_name=""
case "$rid" in
    linux-*)  lib_name="libSDL3.so.0" ;;
    osx-*)    lib_name="libSDL3.dylib" ;;
    *) echo "Unsupported RID $rid for this script" >&2; exit 1 ;;
esac

# Locate the system-installed SDL3 if no explicit path given.
src=""
if [[ -n "$explicit_lib" ]]; then
    src="$explicit_lib"
elif command -v ldconfig >/dev/null 2>&1; then
    src=$(ldconfig -p 2>/dev/null | awk '/libSDL3\.so\.0/ {print $NF; exit}') || true
fi

if [[ -z "$src" ]]; then
    # Fallback: common installation paths.
    for candidate in \
        /usr/lib/x86_64-linux-gnu/$lib_name \
        /usr/lib64/$lib_name \
        /usr/lib/$lib_name \
        /usr/local/lib/$lib_name \
        /opt/homebrew/lib/$lib_name \
        /opt/local/lib/$lib_name; do
        if [[ -f "$candidate" ]]; then
            src="$candidate"
            break
        fi
    done
fi

if [[ -z "$src" || ! -f "$src" ]]; then
    cat >&2 <<EOF
Could not locate $lib_name on this system.

Install SDL3 first:
  Debian / Ubuntu:    sudo apt install libsdl3-0
  Fedora:             sudo dnf install SDL3
  Arch:               sudo pacman -S sdl3
  macOS (Homebrew):   brew install sdl3

Then re-run this script. You can also pass --lib /path/to/$lib_name explicitly.
EOF
    exit 1
fi

dest_dir="$output_dir/runtimes/$rid/native"
dest="$dest_dir/$lib_name"

if [[ -f "$dest" ]]; then
    echo "$dest already exists; delete to re-copy." >&2
    exit 0
fi

mkdir -p "$dest_dir"
cp "$src" "$dest"

echo "Copied $src → $dest"
echo "TerminalNinja.Skia's MSBuild targets will pick it up on the next build."
