#!/usr/bin/env bash
# Rewrites the PublicAPI.Unshipped.txt files of the four shipped libraries from the current source (#54).
#
#   eng/update-public-api.sh            all four libraries
#   eng/update-public-api.sh Maui Uno   only the named ones (Core, SkiaSharp, Maui, Uno)
#
# Run it after adding, changing or removing public API, then review `git diff -- '*PublicAPI*.txt'`.
#
# Core and SkiaSharp target one framework: the script empties the Unshipped file and lets the RS0016 code fix
# (through `dotnet format analyzers`) write the whole surface that PublicAPI.Shipped.txt does not already cover.
# Maui and Uno target several frameworks whose surface differs a little (platform-specific members): the script builds the
# surface of every target framework this machine can build, writes the members common to all of them to the
# project's PublicAPI.Unshipped.txt, and the remainder of each framework to PublicAPI/<framework>/PublicAPI.Unshipped.txt.
# Frameworks this machine cannot build (the Windows heads, which need Windows) keep their hand-maintained files, and members
# that exist in every buildable framework are assumed to exist there too: build the Windows head in CI or on Windows to confirm.
# Removing a member that is already in a Shipped file (RS0017) is never automatic; edit the Shipped file by hand.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

# Target frameworks buildable on this machine, per multi-framework project.
tfms_for() {
  case "$1" in
    Maui) echo "net10.0-android net10.0-ios net10.0-maccatalyst" ;;
    Uno)  echo "net10.0 net10.0-ios net10.0-android net10.0-browserwasm net10.0-desktop" ;;
    *)    echo "" ;;
  esac
}

# Public types that SDK source generators add to a head. The RS0016 code fix skips generated code, but the analyzer still
# checks it, so these lines are written into that head's own file (never into the common file: the Windows heads are
# built by other generators and do not have them). Add a line here when a build reports a symbol from generated code.
generated_for() { # <project> <tfm>
  local android_resource=$'KumikoUI.Maui.Resource\nKumikoUI.Maui.Resource.Resource() -> void'
  local uno_resources=$'KumikoUI.Uno.GlobalStaticResources\nKumikoUI.Uno.GlobalStaticResources.GlobalStaticResources() -> void\nstatic KumikoUI.Uno.GlobalStaticResources.Initialize() -> void\nstatic KumikoUI.Uno.GlobalStaticResources.RegisterDefaultStyles() -> void\nstatic KumikoUI.Uno.GlobalStaticResources.RegisterResourceDictionariesBySource() -> void'
  case "$1:$2" in
    Maui:net10.0-android) printf '%s\n' "$android_resource" ;;
    Uno:net10.0-android) printf '%s\n%s\n' "$uno_resources" "${android_resource//Maui/Uno}" ;;
    Uno:*) printf '%s\n' "$uno_resources" ;;
  esac
}

projects=("$@")
if [ ${#projects[@]} -eq 0 ]; then projects=(Core SkiaSharp Maui Uno); fi

work="$(mktemp -d)"
csproj_backup=""
restore_csproj() {
  if [ -n "$csproj_backup" ] && [ -f "$csproj_backup" ]; then cp "$csproj_backup" "$csproj"; fi
}
trap 'restore_csproj; rm -rf "$work"' EXIT

fix_all() { # <csproj>
  dotnet format analyzers "$1" --diagnostics RS0016 --severity info >/dev/null 2>&1 || true
}

for name in "${projects[@]}"; do
  dir="src/KumikoUI.$name"
  csproj="$dir/KumikoUI.$name.csproj"
  tfms="$(tfms_for "$name")"
  echo "== KumikoUI.$name"

  if [ -z "$tfms" ]; then
    : > "$dir/PublicAPI.Unshipped.txt"
    dotnet restore "$csproj" >/dev/null
    fix_all "$csproj"
    LC_ALL=C sort -o "$dir/PublicAPI.Unshipped.txt" "$dir/PublicAPI.Unshipped.txt"
    continue
  fi

  # dotnet format loads one project per target framework and cannot be pointed at one, so the project file is
  # narrowed to a single framework while that framework's surface is collected (restored afterwards, also on failure).
  csproj_backup="$work/$name.csproj.bak"
  cp "$csproj" "$csproj_backup"
  for tfm in $tfms; do
    : > "$dir/PublicAPI.Unshipped.txt"
    for f in "$dir"/PublicAPI/*/PublicAPI.Unshipped.txt; do [ -f "$f" ] && : > "$f"; done
    perl -0pi -e "s|<TargetFrameworks>[^<]*</TargetFrameworks>|<TargetFrameworks>$tfm</TargetFrameworks>|" "$csproj"
    dotnet restore "$csproj" >/dev/null
    fix_all "$csproj"
    LC_ALL=C sort -u "$dir/PublicAPI.Unshipped.txt" > "$work/$tfm.txt"
    cp "$csproj_backup" "$csproj"
    echo "   $tfm: $(wc -l < "$work/$tfm.txt" | tr -d ' ') members"
  done
  csproj_backup=""

  # Members present in every buildable framework go to the common file; the rest to the framework's own file.
  common="$work/common.txt"
  first=1
  for tfm in $tfms; do
    if [ $first -eq 1 ]; then cp "$work/$tfm.txt" "$common"; first=0
    else LC_ALL=C comm -12 "$common" "$work/$tfm.txt" > "$common.next"; mv "$common.next" "$common"; fi
  done
  cp "$common" "$dir/PublicAPI.Unshipped.txt"
  for tfm in $tfms; do
    mkdir -p "$dir/PublicAPI/$tfm"
    only="$dir/PublicAPI/$tfm/PublicAPI.Unshipped.txt"
    { LC_ALL=C comm -23 "$work/$tfm.txt" "$common"; generated_for "$name" "$tfm"; } | LC_ALL=C sort -u > "$only"
    if [ -s "$only" ]; then
      [ -f "$dir/PublicAPI/$tfm/PublicAPI.Shipped.txt" ] || printf '#nullable enable\n' > "$dir/PublicAPI/$tfm/PublicAPI.Shipped.txt"
      echo "   $tfm only: $(wc -l < "$only" | tr -d ' ') members"
    else
      rm -f "$only" "$dir/PublicAPI/$tfm/PublicAPI.Shipped.txt"
      rmdir "$dir/PublicAPI/$tfm" 2>/dev/null || true
    fi
  done
  rmdir "$dir/PublicAPI" 2>/dev/null || true
  dotnet restore "$csproj" >/dev/null
done
echo "Done. Review: git diff -- '*PublicAPI*.txt'"
