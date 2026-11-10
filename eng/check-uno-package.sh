#!/usr/bin/env bash
# Asserts the contents and nuspec of a packed KumikoUI.Uno package (#66).
#
#   eng/check-uno-package.sh <dir-or-nupkg>      # a directory holding KumikoUI.Uno.<version>.nupkg
#
# Fails on: content/contentFiles entries or AndroidX resources (abc_*), a package over 2 MB, a missing
# target framework group, a dependency outside the allowlist (dev-time Uno packages such as Lottie,
# Resizetizer or DevServer must stay private), or missing required metadata. Prints size and dependencies.
set -euo pipefail

target="${1:?usage: check-uno-package.sh <dir-or-nupkg>}"
if [ -d "$target" ]; then
  nupkg=$(ls "$target"/KumikoUI.Uno.*.nupkg 2>/dev/null | head -n 1 || true)
else
  nupkg="$target"
fi
[ -f "${nupkg:-}" ] || { echo "error: no KumikoUI.Uno nupkg found in $target" >&2; exit 1; }

max_bytes=$((2 * 1024 * 1024))
errors=0
fail() { echo "FAIL: $*" >&2; errors=$((errors + 1)); }

size=$(wc -c < "$nupkg" | tr -d ' ')
entries=$(unzip -Z1 "$nupkg")
echo "Package: $(basename "$nupkg") ($size bytes, $(printf '%s\n' "$entries" | wc -l | tr -d ' ') entries)"
[ "$size" -lt "$max_bytes" ] || fail "package is $size bytes; the limit is $max_bytes"

# --- Contents -------------------------------------------------------------------------------------
if printf '%s\n' "$entries" | grep -Ei '^(content|contentFiles|build|buildTransitive|analyzers)/'; then
  fail "unexpected content/build/analyzer entries (listed above)"
fi
if printf '%s\n' "$entries" | grep -E '(^|/)abc_|(^|/)res/|\.(axml|aar)$'; then
  fail "Android resource entries (listed above)"
fi
other=$(printf '%s\n' "$entries" | grep -Ev '^(lib/|_rels/|package/|\[Content_Types\]\.xml$|KumikoUI\.Uno\.nuspec$|README\.md$|logo\.png$)' || true)
[ -z "$other" ] || fail "unexpected entries: $(echo "$other" | head -n 5 | tr '\n' ' ')"
for required in README.md logo.png; do
  printf '%s\n' "$entries" | grep -qx "$required" || fail "missing $required"
done

# --- Nuspec ---------------------------------------------------------------------------------------
nuspec=$(unzip -p "$nupkg" KumikoUI.Uno.nuspec)
require() { printf '%s' "$nuspec" | grep -Eq "$1" || fail "nuspec: $2"; }
require '<id>KumikoUI\.Uno</id>' 'id is not KumikoUI.Uno'
require '<license type="expression">MIT</license>' 'license expression MIT missing'
require '<readme>README\.md</readme>' 'readme missing'
require '<icon>logo\.png</icon>' 'icon missing'
require '<projectUrl>https://github.com/TheEightBot/KumikoUI</projectUrl>' 'projectUrl missing'
require '<repository type="git" url="https://github.com/TheEightBot/KumikoUI"' 'repository metadata missing'
require '<description>[^<]{40,}' 'description missing or too short'
require '<authors>[^<]+' 'authors missing'
for tag in uno skiasharp datagrid wasm desktop android ios; do
  printf '%s' "$nuspec" | grep -Eq "<tags>([^<]* )?$tag( [^<]*)?</tags>" || fail "nuspec: tag '$tag' missing"
done

# --- Dependencies per target framework group ------------------------------------------------------
groups=$(printf '%s\n' "$nuspec" | awk '
  /<group targetFramework=/ { match($0, /targetFramework="[^"]*"/); tfm = substr($0, RSTART + 17, RLENGTH - 18); next }
  /<\/group>/ { tfm = ""; next }
  tfm != "" && /<dependency / { match($0, /id="[^"]*"/); print tfm, substr($0, RSTART + 4, RLENGTH - 5) }')
for tfm in net10.0 net10.0-android36.0 net10.0-browserwasm1.0 net10.0-desktop1.0 net10.0-ios26.0; do
  printf '%s\n' "$groups" | grep -q "^$tfm " || { fail "dependency group $tfm missing"; continue; }
  for id in KumikoUI.Core KumikoUI.SkiaSharp Uno.WinUI SkiaSharp.Views.Uno.WinUI; do
    printf '%s\n' "$groups" | grep -qx "$tfm $id" || fail "$tfm: dependency $id missing"
  done
done
allowed='^(KumikoUI\.(Core|SkiaSharp)|SkiaSharp(\.Views(\.Uno\.WinUI|\.WinUI)?|\.Skottie|\.NativeAssets\.Linux)?|Uno\.WinUI|Microsoft\.Extensions\.Hosting\.Abstractions|Microsoft\.WindowsAppSDK|Microsoft\.Windows\.SDK\.BuildTools)$'
while read -r tfm id; do
  [ -z "${id:-}" ] && continue
  echo "  $tfm  $id"
  printf '%s' "$id" | grep -Eq "$allowed" || fail "$tfm: unexpected dependency $id (dev-time or unlisted package must be PrivateAssets=\"all\")"
done <<< "$groups"

if [ "$errors" -gt 0 ]; then
  echo "KumikoUI.Uno package check failed with $errors problem(s)." >&2
  exit 1
fi
echo "KumikoUI.Uno package check passed."
