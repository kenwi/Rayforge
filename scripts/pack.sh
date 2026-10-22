#!/usr/bin/env bash
# Pack the three Rayforge framework packages and push them into the local folder feed.
#
#   ./scripts/pack.sh                    # default version from Directory.Build.props
#   ./scripts/pack.sh -p:RayforgeVersion=0.1.0-preview.2
#
# The feed is the "rayforge-local" source in NuGet.config. The template restores from it, so a
# generated game never needs a ProjectReference back into this repository.
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
packages_dir="$repo_root/artifacts/packages"
feed_dir="${RAYFORGE_FEED:-$repo_root/nuget-local}"

mkdir -p "$packages_dir" "$feed_dir"

echo "Packing Rayforge packages..."
rm -f "$packages_dir"/Rayforge.*.nupkg
dotnet pack "$repo_root/src/Rayforge.Core/Rayforge.Core.csproj" -c Release -o "$packages_dir" "$@"
dotnet pack "$repo_root/src/Rayforge.Web/Rayforge.Web.csproj" -c Release -o "$packages_dir" "$@"
dotnet pack "$repo_root/src/Rayforge.Desktop/Rayforge.Desktop.csproj" -c Release -o "$packages_dir" "$@"

echo "Pushing packages into $feed_dir..."
rm -f "$feed_dir"/Rayforge.*.nupkg
for package in "$packages_dir"/Rayforge.*.nupkg; do
    dotnet nuget push "$package" --source "$feed_dir"
done

echo
echo "Local feed ready: $feed_dir"
ls -1 "$feed_dir"/Rayforge.*.nupkg
