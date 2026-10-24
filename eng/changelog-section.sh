#!/usr/bin/env bash
# Prints the body of one CHANGELOG.md section, without its heading.
#
#   eng/changelog-section.sh 0.2.0         # section "## [0.2.0] - <date>"
#   eng/changelog-section.sh Unreleased    # section "## [Unreleased]"
#
# Exits 1 if the section is missing or empty.
set -euo pipefail
version="${1:?usage: changelog-section.sh <version|Unreleased>}"
file="${2:-$(cd "$(dirname "$0")/.." && pwd)/CHANGELOG.md}"

body=$(awk -v v="$version" '
  /^## \[/ {
    if (found) exit
    if (index($0, "## [" v "]") == 1) { found = 1; next }
  }
  /^\[[^]]+\]: / { if (found) exit }      # link reference definitions at the end
  found { print }
' "$file" | sed -e '/./,$!d' | sed -e ':a' -e '/^\n*$/{$d;N;ba' -e '}')

if [[ -z "${body//[[:space:]]/}" ]]; then
  echo "CHANGELOG.md has no '## [$version]' section (or it is empty)." >&2
  exit 1
fi
printf '%s\n' "$body"
