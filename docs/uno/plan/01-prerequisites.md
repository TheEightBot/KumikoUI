# 01 — Prerequisites & Tooling

Goal: a machine (and CI runner) that can scaffold and build every Uno head. Do this once per
environment before touching project files.

> The Uno port targets `net9.0-*` heads (see [ADR-4](README.md#2-architecture-decision-record)). The
> `.NET 10` SDK can build `net9.0` target frameworks, so a single recent SDK covers the whole `src/`
> stack.

> **Environment status — verified 2026-06-06 (this macOS dev machine):**
> - ✅ .NET SDK `10.0.x` present (builds `net9.0` TFMs) · ✅ Uno templates (`unoapp`, `unolib`, `unoapp-uitest`) · ✅ `uno-check` 1.33.1
> - ✅ Workloads `ios`, `macos` installed · ❌ `android`, `wasm-tools`, `maccatalyst` **not installable here** — `dotnet workload install` returns *"Inadequate permissions. Run the command with elevated privileges."* (the SDK lives in `/usr/local/share/dotnet`; no non-interactive `sudo` is available in this session).
> - ➡️ **Local build/verify target: `net9.0-desktop`** (Skia desktop — no mobile workload required). The Android / WebAssembly / Apple / Windows heads are built & verified in **CI** ([08](08-ci-cd-packaging.md)), where runners install workloads with the needed privileges. To build them locally, run `sudo dotnet workload install android wasm-tools maccatalyst` yourself, then re-run the per-head builds.

---

## 1. .NET SDK

- [x] Install the **.NET 10 SDK** (or .NET 9 SDK). Verify: — *`10.0.100`–`10.0.300` present*
  ```bash
  dotnet --list-sdks
  ```
- [x] Decide the library TFM. Default plan: `net9.0` heads (matches `KumikoUI.Core` / `KumikoUI.SkiaSharp`).

## 2. Uno Platform templates

- [x] Install (or update) the Uno templates: — *already installed*
  ```bash
  dotnet new install Uno.Templates
  ```
- [x] Confirm the templates are available: — *`unoapp`, `unolib`, `unoapp-uitest` present*
  ```bash
  dotnet new list | grep -i uno
  # Expect: unoapp, unolib, unomauilib, unoapp-uitest
  ```
- [x] (Optional) Inspect template options so the scaffold commands in [02](02-library-scaffold.md) and
  [06](06-sample-app.md) match your installed version: — *captured (`unolib`/`unoapp` `-tfm`, `-platforms`, `-renderer` …)*
  ```bash
  dotnet new unolib -h
  dotnet new unoapp -h
  ```

## 3. `uno-check` — environment doctor

`uno-check` validates and installs the workloads/SDK bits Uno needs. It is the canonical way to get
a machine build-ready.

- [x] Install the tool: — *`uno-check` 1.33.1 present at `~/.dotnet/tools/uno-check`*
  ```bash
  dotnet tool install --global uno.check
  ```
- [ ] Run it and apply fixes: — ⚠️ *applying workload fixes needs elevated privileges here; run with `sudo` to install the missing workloads*
  ```bash
  uno-check
  ```

## 4. Workloads

Uno heads require these workloads. `uno-check` installs them, or do it manually:

- [ ] `wasm-tools` — WebAssembly (`net9.0-browserwasm`) — ❌ *not installed here (needs elevation); verified in CI*
- [ ] `android` — Android (`net9.0-android`) — ❌ *not installed here (needs elevation); verified in CI*
- [x] `ios` — Apple head — *installed*  · [ ] `maccatalyst` — ❌ *not installed here (needs elevation)*
- [x] Verify: — *ran `dotnet workload list` → `ios`, `macos` present*
  ```bash
  dotnet workload list
  ```

```bash
# manual install (skip if uno-check handled it) — requires elevation on this machine:
sudo dotnet workload install wasm-tools android ios maccatalyst
```

## 5. Per-OS build constraints

A single cross-targeted project can be *opened* anywhere, but some heads only **build** on a specific
OS. Plan CI runners accordingly (see [08](08-ci-cd-packaging.md)).

| Head | macOS | Windows | Linux |
|---|:---:|:---:|:---:|
| `net9.0-desktop` | ✅ | ✅ | ✅ |
| `net9.0-browserwasm` | ✅ | ✅ | ✅ |
| `net9.0-android` | ✅ | ✅ | ✅ |
| `net9.0-ios` | ✅ | ❌ | ❌ |
| `net9.0-maccatalyst` | ✅ | ❌ | ❌ |
| `net9.0-windows10.0.xxxxx` | ❌ | ✅ | ❌ |

> When building a single head locally, pass `-f`/`--framework` (e.g. `dotnet build -f net9.0-desktop`)
> to avoid building unavailable heads on the current OS.

> **Library vs. app:** the table above is for building/running the **app** (`SampleApp.Uno`). A **class
> library** like `KumikoUI.Uno` cross-compiles more permissively — the `-ios`/`-maccatalyst` heads
> compile on Windows/Linux with the workloads installed (no Mac needed for managed library code); only
> the WinAppSDK `-windows10.0.x` head strictly needs Windows. This is why the whole library can be
> packed on one Windows runner — see [08 §1](08-ci-cd-packaging.md#1-packaging-fact-that-simplifies-everything).

## 6. IDE (optional, for running heads)

- [ ] Visual Studio 2022/2026 (Windows head) or JetBrains Rider, with the **Uno Platform** extension
  for designer/hot-reload support. The CLI is sufficient for build/pack; an IDE is convenient for
  debugging the Windows and mobile heads.

---

## ✅ Exit criteria

- [x] `dotnet --list-sdks` shows .NET 9 or 10. — *.NET 10 SDKs present*
- [x] `dotnet new list` shows `unolib` and `unoapp`. — *present*
- [x] `uno-check` tool installed (1.33.1). — *workload auto-fix requires elevation here; deferred to CI / manual `sudo`*
- [~] `dotnet workload list` shows the workloads for your target heads. — *`ios`/`macos` ✅; `android`/`wasm-tools`/`maccatalyst` ❌ (elevation-blocked → built in CI). **Local verify target: `net9.0-desktop`.***
