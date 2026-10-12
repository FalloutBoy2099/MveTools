#!/usr/bin/env bash
# Publishes the CLI for one platform and packs it.
#
#   package.sh <rid> <mode> <version> <output-directory>
#
#   rid     win-x64 | win-arm64 | osx-x64 | osx-arm64 | linux-x64 | linux-arm64
#   mode    framework-dependent   executable only, needs .NET 10 installed
#           self-contained        executable with the .NET runtime inside
#   version 1.2.3 or 1.2.3-beta.1
#
# Produces <rid>[-self-contained].zip and, for macOS and Linux, a .tar.gz with the same content
# (the executable bit survives in both archives because the packing runs on a Unix system).
set -euo pipefail

if [[ $# -ne 4 ]]; then
	echo "usage: $0 <rid> <mode> <version> <output-directory>" >&2
	exit 2
fi

rid="$1"
mode="$2"
version="$3"
output="$4"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if ! command -v zip > /dev/null; then
	echo "zip is required" >&2
	exit 3
fi

case "$mode" in
	framework-dependent)
		self_contained=false
		suffix=""
		extra=()
		;;
	self-contained)
		self_contained=true
		suffix="-self-contained"
		extra=(-p:EnableCompressionInSingleFile=true)
		;;
	*)
		echo "unknown mode: $mode" >&2
		exit 2
		;;
esac

case "$rid" in
	win-x64 | win-arm64)
		published_name="MveTools.Cli.exe"
		installed_name="MveConverter.exe"
		unix=false
		;;
	osx-x64 | osx-arm64)
		published_name="MveTools.Cli"
		installed_name="MveConverter"
		unix=true
		;;
	linux-x64 | linux-arm64)
		published_name="MveTools.Cli"
		installed_name="mveconverter"
		unix=true
		;;
	*)
		echo "unknown runtime identifier: $rid" >&2
		exit 2
		;;
esac

# Assembly versions are numeric: 1.2.3-beta.1 becomes 1.2.3.0.
numeric="${version%%[-+]*}"
IFS=. read -r -a parts <<< "$numeric"
while ((${#parts[@]} < 4)); do
	parts+=(0)
done
assembly_version="${parts[0]}.${parts[1]}.${parts[2]}.${parts[3]}"

publish_dir="$(mktemp -d)"
stage_dir="$(mktemp -d)"
trap 'rm -rf "$publish_dir" "$stage_dir"' EXIT

dotnet publish "$root/MveTools.Cli/MveTools.Cli.csproj" \
	--configuration Release \
	--runtime "$rid" \
	--self-contained "$self_contained" \
	-p:PublishSingleFile=true \
	-p:IncludeNativeLibrariesForSelfExtract=true \
	-p:DebugType=None \
	-p:DebugSymbols=false \
	-p:Version="$version" \
	-p:AssemblyVersion="$assembly_version" \
	-p:FileVersion="$assembly_version" \
	${extra[@]+"${extra[@]}"} \
	--output "$publish_dir"

if [[ ! -f "$publish_dir/$published_name" ]]; then
	echo "expected executable was not produced: $publish_dir/$published_name" >&2
	exit 1
fi

cp "$publish_dir/$published_name" "$stage_dir/$installed_name"
if [[ -f "$root/README.md" ]]; then
	cp "$root/README.md" "$stage_dir/README.md"
fi

if [[ "$unix" == true ]]; then
	chmod +x "$stage_dir/$installed_name"
fi

mkdir -p "$output"
output="$(cd "$output" && pwd)"
base="$rid$suffix"

rm -f "$output/$base.zip" "$output/$base.tar.gz"
(cd "$stage_dir" && zip -q -9 "$output/$base.zip" -- *)
if [[ "$unix" == true ]]; then
	tar -C "$stage_dir" -czf "$output/$base.tar.gz" .
fi

ls -l "$output"/"$base".*