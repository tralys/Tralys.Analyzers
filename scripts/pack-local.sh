#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
feed="${TRALYS_ANALYZERS_FEED:-$HOME/.local/share/tralys-analyzers-feed}"
if [ $# -gt 0 ]; then
    consumers=("$@")
else
    consumers=("$root/../Tralys.Ui" "$root/../Tralys.Ecs" "$root/../Tralys.BehaviorTree")
fi

# NuGet never re-reads a version it has cached, so every content change needs a new version.
prefix="$(dotnet msbuild "$root/src/Tralys.Analyzers/Tralys.Analyzers.csproj" -getProperty:VersionPrefix)"
suffix="dev"
if head="$(git -C "$root" rev-parse --short=10 HEAD 2>/dev/null)"; then
    suffix="$suffix.g$head"
fi
if [ -n "$(git -C "$root" status --porcelain)" ]; then
    dirty="$(
        cd "$root"
        { git diff HEAD 2>/dev/null || true; git ls-files --others --exclude-standard -z | xargs -0 -r sha1sum; } \
            | sha1sum | cut -c1-10
    )"
    suffix="$suffix.d$dirty"
fi
version="$prefix-$suffix"

mkdir -p "$feed"
if [ -f "$feed/Tralys.Analyzers.$version.nupkg" ]; then
    echo "already packed: $version"
else
    dotnet pack "$root/src/Tralys.Analyzers/Tralys.Analyzers.csproj" -c Release -o "$feed" \
        -p:VersionSuffix="$suffix" -p:ContinuousIntegrationBuild=true -v q -nologo
fi

for consumer in "${consumers[@]}"; do
    pinned="$(cd "$consumer" && rg -l --fixed-strings '<TralysAnalyzersVersion>' -g '*.props' . || true)"
    if [ -z "$pinned" ]; then
        echo "no TralysAnalyzersVersion in $consumer" >&2
        exit 1
    fi
    for file in $pinned; do
        sed -i "s|<TralysAnalyzersVersion>.*</TralysAnalyzersVersion>|<TralysAnalyzersVersion>$version</TralysAnalyzersVersion>|" \
            "$consumer/$file"
    done
    echo "pinned $version in $(realpath "$consumer")"
done
