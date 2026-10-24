# Versioning and support policy

## One version for all packages

KumikoUI.Core, KumikoUI.SkiaSharp, KumikoUI.Maui and KumikoUI.Uno are released together and always share the same version. Use the same version of every KumikoUI package in an app. Versions come from git tags (see [CONTRIBUTING.md](../CONTRIBUTING.md#versions-and-releases)).

## Semantic Versioning

KumikoUI follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html): `MAJOR.MINOR.PATCH[-prerelease]`.

| Change | Before 1.0 (`0.y.z`) | From 1.0 |
|---|---|---|
| Breaking change to public API or documented behaviour | minor (`0.2.0` → `0.3.0`) | major |
| New feature, backward compatible | minor or patch | minor |
| Bug fix, backward compatible | patch | patch |
| Raised minimum OS, .NET or SkiaSharp major | minor | major |

Before 1.0 the API is still settling: breaking changes can land in any minor release and are always listed under **Changed** with a **Breaking:** prefix in the [CHANGELOG](../CHANGELOG.md).

Prerelease versions (`1.0.0-preview.1`, `0.2.0-alpha.0.8`) carry no compatibility promise.

## What counts as public API

- Every public type and member in the four packages, including XAML-settable properties and events.
- Documented behaviour: defaults, event order, rendering and input semantics described in the README or XML docs.
- The supported platforms and minimum versions in the README.

Not covered: members marked internal or experimental, sample code, and pixel-exact rendering output (anti-aliasing can change with SkiaSharp updates).

From 1.0, the public surface is tracked with API analyzers and package validation, so an unintended breaking change fails the build.

## Deprecation

From 1.0, a public API is removed only after it has been marked `[Obsolete]` with a message pointing to the replacement for at least one minor release; removal happens in the next major. Behaviour changes that cannot be deprecated are called out in the CHANGELOG and, where possible, kept behind an opt-in setting for one minor release.

## Support window

- **.NET:** each KumikoUI major targets the current .NET LTS release (currently .NET 10, supported until November 2028). When .NET moves to a new LTS, the next KumikoUI major moves with it; the previous major gets security fixes for six months.
- **Minimum OS versions** are re-baselined at each .NET major release; see the README's "Supported platforms".
- **Fixes:** bug and security fixes ship in the latest minor of the current major. Before 1.0, only the latest release is supported (see [SECURITY.md](../SECURITY.md)).
- **SkiaSharp, .NET MAUI and Uno Platform:** KumikoUI moves to new majors of its dependencies in its own minor (before 1.0) or major (from 1.0) releases, and states the required versions in the CHANGELOG.
