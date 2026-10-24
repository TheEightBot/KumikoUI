#!/usr/bin/env bash
# Applies the repository settings checked in under .github/settings/ to GitHub.
# Idempotent: rulesets are updated in place by name; the environment is re-applied.
# Requires: gh (authenticated as a repository admin), jq.
#
#   eng/apply-github-settings.sh                 # apply
#   eng/apply-github-settings.sh --dry-run       # print what would change
set -euo pipefail

REPO="${REPO:-TheEightBot/KumikoUI}"
ENVIRONMENT="nuget-publish"
REVIEWER_LOGIN="${REVIEWER_LOGIN:-michaelstonis}"   # required reviewer for publishing
DRY_RUN=false
[[ "${1:-}" == "--dry-run" ]] && DRY_RUN=true

root="$(cd "$(dirname "$0")/.." && pwd)"
run() { if $DRY_RUN; then echo "DRY-RUN: $*"; else "$@"; fi; }

# ── Rulesets ────────────────────────────────────────────────────────────────
for file in "$root"/.github/settings/ruleset-*.json; do
  name=$(jq -r .name "$file")
  id=$(gh api "repos/$REPO/rulesets" --jq ".[] | select(.name == \"$name\") | .id")
  if [[ -n "$id" ]]; then
    echo "Updating ruleset '$name' ($id)"
    run gh api -X PUT "repos/$REPO/rulesets/$id" --input "$file" --silent
  else
    echo "Creating ruleset '$name'"
    run gh api -X POST "repos/$REPO/rulesets" --input "$file" --silent
  fi
done

# ── Publish environment: required reviewer, v* tags only ────────────────────
reviewer_id=$(gh api "users/$REVIEWER_LOGIN" --jq .id)
echo "Configuring environment '$ENVIRONMENT' (reviewer: $REVIEWER_LOGIN, deployments: v* tags)"
jq -n --argjson id "$reviewer_id" '{
  reviewers: [ { type: "User", id: $id } ],
  prevent_self_review: false,
  deployment_branch_policy: { protected_branches: false, custom_branch_policies: true }
}' > "${TMPDIR:-/tmp}/kumiko-env.json"
run gh api -X PUT "repos/$REPO/environments/$ENVIRONMENT" --input "${TMPDIR:-/tmp}/kumiko-env.json" --silent

# The list call 404s until custom policies are enabled (and prints the error body), so only trust success.
if ! existing=$(gh api "repos/$REPO/environments/$ENVIRONMENT/deployment-branch-policies" \
  --jq '.branch_policies[] | select(.type == "tag" and .name == "v*") | .id' 2>/dev/null); then
  existing=""
fi
if [[ -z "$existing" ]]; then
  run gh api -X POST "repos/$REPO/environments/$ENVIRONMENT/deployment-branch-policies" \
    -f name='v*' -f type=tag --silent
  echo "Added deployment policy: tags matching v*"
fi

# ── Security features ───────────────────────────────────────────────────────
echo "Enabling secret scanning + push protection, Dependabot alerts and security updates"
run gh api -X PATCH "repos/$REPO" --silent --input - <<'JSON'
{ "security_and_analysis": {
    "secret_scanning": { "status": "enabled" },
    "secret_scanning_push_protection": { "status": "enabled" } } }
JSON
run gh api -X PUT "repos/$REPO/vulnerability-alerts" --silent
run gh api -X PUT "repos/$REPO/automated-security-fixes" --silent

# ── Community: Discussions and private vulnerability reporting (SECURITY.md) ─
echo "Enabling Discussions, private vulnerability reporting and branch auto-delete after merge"
run gh api -X PATCH "repos/$REPO" --silent -F has_discussions=true -F delete_branch_on_merge=true
run gh api -X PUT "repos/$REPO/private-vulnerability-reporting" --silent

# ── Workflow token defaults: read-only; workflows request more per job ──────
echo "Setting the default GITHUB_TOKEN permission to read-only"
run gh api -X PUT "repos/$REPO/actions/permissions/workflow" --silent \
  -f default_workflow_permissions=read -F can_approve_pull_request_reviews=false

echo "Done."
