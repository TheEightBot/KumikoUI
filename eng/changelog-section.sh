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
  # Drop leading blank lines; hold blank lines back until more text follows, so trailing ones are trimmed.
  found {
    if ($0 ~ /^[[:space:]]*$/) { if (started) blanks = blanks "\n"; next }
    printf "%s%s\n", (started ? blanks : ""), $0
    started = 1; blanks = ""
  }
' "$file")

# (grep, not ${body//...}: bash 3.2 on macOS takes minutes to run that substitution on a long section)
if ! printf '%s' "$body" | grep -q '[^[:space:]]'; then
  echo "CHANGELOG.md has no '## [$version]' section (or it is empty)." >&2
  exit 1
fi
printf '%s\n' "$body"
