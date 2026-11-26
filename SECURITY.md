# Security policy

## Supported versions

The current release line is `1.0.x`. Security fixes ship as a patch release of the latest minor of the current major; there are no other maintained release branches (see the support window in [docs/VERSIONING.md](docs/VERSIONING.md#support-window)).

| Version | Supported |
|---|---|
| `1.0.x` | ✅ |
| `1.0.0-preview.N` prereleases | ❌ — please upgrade to `1.0.x` |
| `0.2.x` and older `0.x` releases | ❌ — please upgrade |

When a new minor or major ships, this table moves to it; the support window in VERSIONING.md says how long the previous major gets security fixes.

## Reporting a vulnerability

**Please do not report security problems in public issues, discussions or pull requests.**

Report privately through GitHub: open the repository's **Security** tab and choose **[Report a vulnerability](https://github.com/TheEightBot/KumikoUI/security/advisories/new)**. Only the maintainers can see the report.

Please include:

- the affected package(s) and version(s), and the host (MAUI or Uno) and platform;
- what an attacker could do, and under which conditions;
- steps or a minimal project that reproduces it.

## What to expect

- Acknowledgement within **5 business days**.
- An assessment and, if confirmed, a fix plan within **30 days**; we'll keep you updated on progress.
- A coordinated release: the fix ships in a new package version, followed by a published GitHub security advisory that credits you unless you prefer otherwise.

KumikoUI is a UI component library: issues in how it renders or handles input are usually bugs, not vulnerabilities. If you're unsure, report privately anyway.
