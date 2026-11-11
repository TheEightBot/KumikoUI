#!/usr/bin/env bash
# Coverage ratchet (#85): compares a merged Cobertura report with eng/coverage/baseline.json.
#
#   eng/coverage/check-coverage.sh <Cobertura.xml> [baseline.json]
#
# Prints a Markdown table (with the delta against the baseline) to stdout and, in GitHub Actions, to the job
# summary. Exits 1 when line or branch coverage is below the baseline. Needs jq (preinstalled on GitHub runners).
set -euo pipefail

report="${1:?usage: check-coverage.sh <Cobertura.xml> [baseline.json]}"
baseline="${2:-$(dirname "$0")/baseline.json}"

# The root element carries the totals: line-rate="0.9606" branch-rate="0.8662"
root="$(grep -m1 '<coverage ' "$report")"
rate() { sed -n "s/.* $1=\"\([0-9.eE+-]*\)\".*/\1/p" <<<"$root"; }
line="$(awk -v r="$(rate line-rate)" 'BEGIN { printf "%.2f", r * 100 }')"
branch="$(awk -v r="$(rate branch-rate)" 'BEGIN { printf "%.2f", r * 100 }')"
min_line="$(jq -r '.lineCoveragePercent' "$baseline")"
min_branch="$(jq -r '.branchCoveragePercent' "$baseline")"

status=0
row() { # name actual floor
  local ok="pass"
  if awk -v a="$2" -v b="$3" 'BEGIN { exit !(a < b) }'; then ok="FAIL"; status=1; fi
  awk -v n="$1" -v a="$2" -v b="$3" -v s="$ok" 'BEGIN { printf "| %s | %.2f%% | %.2f%% | %+.2f | %s |\n", n, a, b, a - b, s }'
}

# Not in a $(...) subshell: row() sets $status.
table="$(mktemp)"
{
  echo "### Coverage ratchet"
  echo
  echo "| Metric | Measured | Baseline | Delta | Result |"
  echo "|:---|---:|---:|---:|:---|"
  row "Line" "$line" "$min_line"
  row "Branch" "$branch" "$min_branch"
  echo
} > "$table"
if [ "$status" -ne 0 ]; then
  echo "Coverage fell below eng/coverage/baseline.json. Add tests for the new code (do not lower the baseline)." >> "$table"
else
  echo "Raise the baseline in eng/coverage/baseline.json when coverage has grown well past it." >> "$table"
fi
cat "$table"
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then cat "$table" >> "$GITHUB_STEP_SUMMARY"; fi
rm -f "$table"
exit "$status"
