# Repository settings

GitHub settings that protect `main` and releases, kept in the repository so they are reviewed like code.
Apply them (as a repository admin) with:

```bash
eng/apply-github-settings.sh --dry-run   # show what would change
eng/apply-github-settings.sh
```

The script is idempotent: rulesets are updated in place by name, and the environment is re-applied.

## What they enforce

| Setting | File | Effect |
|---|---|---|
| `main` ruleset | `ruleset-main.json` | Changes reach `main` only through pull requests. The three CI jobs must pass (`Build & Test (Core + SkiaSharp)`, `Build (KumikoUI.Maui — all TFMs)`, `Build & Test (KumikoUI.Uno — all heads)`). Review threads must be resolved. History stays linear, so pull requests merge by squash or rebase. No force pushes or deletion. Admins may bypass only through a pull request. |
| `release-tags` ruleset | `ruleset-release-tags.json` | Only admins can create, move or delete `v*` tags, the tags that publish packages. |
| `nuget-publish` environment | `eng/apply-github-settings.sh` | The publish job waits for approval by the configured reviewer and can only deploy from `v*` tags. |
| Security features | `eng/apply-github-settings.sh` | Secret scanning with push protection, Dependabot alerts and Dependabot security updates. |
| Workflow token default | `eng/apply-github-settings.sh` | `GITHUB_TOKEN` is read-only unless a workflow job asks for more, and Actions cannot approve pull requests. |

Checked-in automation that complements these settings: Dependabot version updates (`.github/dependabot.yml`), CodeQL (`.github/workflows/codeql.yml`) and OpenSSF Scorecard (`.github/workflows/scorecard.yml`). Every action is pinned to a commit SHA with its version in a comment; Dependabot keeps the pins current.

The publish workflow adds its own checks on top: the ref must be a `vX.Y.Z[-suffix]` tag, the tagged commit must be on `main`, MinVer must compute the same version as the tag, and the Core and Uno test suites must pass.

## Changing them

Edit the JSON (the GitHub [rulesets REST schema](https://docs.github.com/rest/repos/rules)), open a pull request, and run the script after it merges. When a CI job is renamed or added, update `required_status_checks` in `ruleset-main.json` in the same pull request, or pull requests will wait for a check that never reports.
