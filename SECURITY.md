# Security policy

## Supported versions

KumikoUI is pre-1.0. Security fixes go into the latest release only; there are no maintained release branches yet.

| Version | Supported |
|---|---|
| Latest `0.x` release on NuGet.org | ✅ |
| Older releases | ❌ — please upgrade |

From 1.0 on, this table will list the supported release lines.

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
