#!/usr/bin/env bash
# M4 exit test: pack the framework, generate a game from the template, and build both hosts against the
# packed packages. Run from anywhere; it uses a throwaway directory for the feed and the generated game.
#
#   ./tests/template-smoke.sh
#
# This is deliberately a script rather than an xunit test: it installs a template, restores NuGet
# packages, and runs the native wasm link. That is an integration check, not a unit test.
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
work_dir="$(mktemp -d "${TMPDIR:-/tmp}/rayforge-template-smoke.XXXXXX")"
trap 'rm -rf "$work_dir"' EXIT

feed_dir="$work_dir/feed"
packages_dir="$work_dir/packages"
game_dir="$work_dir/TemplateGame"
publish_dir="$work_dir/publish"
version="$(sed -n 's:.*<RayforgeVersion>\(.*\)</RayforgeVersion>.*:\1:p' "$repo_root/Directory.Build.props")"

if [[ -z "$version" ]]; then
    echo "Could not read RayforgeVersion from Directory.Build.props" >&2
    exit 1
fi

mkdir -p "$feed_dir" "$packages_dir"

echo "==> Packing Rayforge $version"
dotnet pack "$repo_root/src/Rayforge.Core/Rayforge.Core.csproj" -c Release -o "$packages_dir" -p:RayforgeVersion="$version" >/dev/null
dotnet pack "$repo_root/src/Rayforge.Web/Rayforge.Web.csproj" -c Release -o "$packages_dir" -p:RayforgeVersion="$version" >/dev/null
dotnet pack "$repo_root/src/Rayforge.Desktop/Rayforge.Desktop.csproj" -c Release -o "$packages_dir" -p:RayforgeVersion="$version" >/dev/null

for package in "$packages_dir"/Rayforge.*.nupkg; do
    dotnet nuget push "$package" --source "$feed_dir" >/dev/null

    contents="$(unzip -Z1 "$package")"
    for required in README.md LICENSE THIRD-PARTY-NOTICES.md; do
        if ! grep -qx "$required" <<<"$contents"; then
            echo "$(basename "$package") is missing $required" >&2
            exit 1
        fi
    done

    if grep -qE '(^|/)(runtimes/|.*\.(a|so|dylib)$)' <<<"$contents"; then
        echo "$(basename "$package") contains a native binary; it must come from Raylib-cs" >&2
        exit 1
    fi
done

if ! unzip -p "$packages_dir"/Rayforge.Web.*.nupkg buildTransitive/Rayforge.Web.targets | grep -q 'EmccLinkOptimizationFlag'; then
    echo "Rayforge.Web package does not carry the wasm host targets" >&2
    exit 1
fi

echo "==> Installing the template"
dotnet new install "$repo_root/templates/rayforge-game" --force >/dev/null

echo "==> Creating TemplateGame"
dotnet new rayforge-game \
    -n TemplateGame \
    -o "$game_dir" \
    --RayforgeFeed "$feed_dir" \
    --RayforgeVersion "$version" >/dev/null

if grep -RIn --include='*.csproj' --include='*.props' 'Rayforge\.\(Core\|Web\|Desktop\)\.csproj' "$game_dir" >/dev/null; then
    echo "Generated project references framework sources instead of packages" >&2
    exit 1
fi

echo "==> Restoring"
dotnet restore "$game_dir/TemplateGame.slnx" --force-evaluate >/dev/null

echo "==> Building desktop"
dotnet build "$game_dir/TemplateGame.Desktop/TemplateGame.Desktop.csproj" -c Release --no-restore >/dev/null

echo "==> Building web"
build_log="$work_dir/web-build.log"
if ! dotnet build "$game_dir/TemplateGame.Web/TemplateGame.Web.csproj" -c Release --no-restore >"$build_log" 2>&1; then
    cat "$build_log" >&2
    exit 1
fi

if ! grep -q 'Linking with emcc with -O0' "$build_log"; then
    echo "The web build did not use the Rayforge -O0 link flag: $build_log" >&2
    grep -i 'linking\|emcc' "$build_log" >&2 || true
    exit 1
fi

if ! grep -q 'raylib\.a' "$build_log"; then
    echo "The web build did not link the Raylib-cs native archive: $build_log" >&2
    exit 1
fi

test -s "$game_dir/TemplateGame.Web/wwwroot/assets/motd.txt"
test -s "$game_dir/TemplateGame.Web/wwwroot/assets/rayforge.assets.json"

echo "==> Publishing web"
dotnet publish "$game_dir/TemplateGame.Web/TemplateGame.Web.csproj" -c Release --no-restore -o "$publish_dir" >/dev/null
test -s "$publish_dir/wwwroot/assets/motd.txt"
test -s "$publish_dir/wwwroot/_framework/blazor.webassembly.js"

echo
echo "PASS: template game built and published against Rayforge $version"
echo "      (temporary build directory removed)"
