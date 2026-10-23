# Contributing to KumikoUI

Thanks for helping. This file currently covers building and testing; the full guide (branching, commit conventions, pull requests, releases) is being written in #93.

## Toolchain

The repository pins its toolchain so every machine and CI build the same way:

| What | Where | Value |
|---|---|---|
| .NET SDK | `global.json` → `sdk.version` (roll forward: latest patch) | 10.0.401 |
| Workload set (MAUI, iOS, Android, Mac Catalyst, WebAssembly) | `global.json` → `sdk.workloadVersion` | 10.0.402.1 |
| Uno.Sdk | `global.json` → `msbuild-sdks` | 6.7.30 |
| NuGet package versions | `Directory.Packages.props` (central package management) | — |
| Package sources | `nuget.config` (nuget.org only) | — |

Package versions live only in `Directory.Packages.props`; project files reference packages without a `Version`. SkiaSharp is a single `SkiaSharpVersion` property there, which Uno.Sdk also uses for its implicit packages.

## Setup

1. Install the .NET SDK band from `global.json` (any 10.0.40x patch).
2. Install the workloads. Because `global.json` pins a workload set, these commands install exactly that set:

   ```bash
   dotnet workload install maui wasm-tools
   ```

   On macOS these need `sudo` when the SDK lives in a system location. `uno-check` (https://aka.platform.uno/uno-check) can verify a machine for the Uno heads.
3. Platform requirements for app heads: Xcode for iOS and Mac Catalyst (macOS only), the Android SDK for Android, Windows for the WinAppSDK heads.

## Build and test

Build what you are working on; the platform-agnostic projects need no workloads:

```bash
dotnet build src/KumikoUI.Core
dotnet build src/KumikoUI.SkiaSharp
dotnet test tests/KumikoUI.Core.Tests
```

Hosts and samples (one head at a time with `-f`):

```bash
dotnet build src/KumikoUI.Maui -f net10.0-maccatalyst
dotnet build src/KumikoUI.Uno -f net10.0-desktop
dotnet test tests/KumikoUI.Uno.Tests
dotnet run --project samples/SampleApp.Uno -f net10.0-desktop
```

`dotnet build KumikoUI.sln` builds every head and therefore needs every workload, plus Windows for the WinAppSDK heads.

## Updating the toolchain

- **SDK or workloads:** update `sdk.version` and `sdk.workloadVersion` together (`dotnet workload --version` prints the installed set). CI reads both from `global.json`.
- **Uno.Sdk:** update `msbuild-sdks` in `global.json` and rebuild every head; Uno changes its implicit packages between versions.
- **Packages:** change the version in `Directory.Packages.props` only.
