# 01 — Prerequisites & Tooling

Goal: a machine (and CI runner) that can scaffold and build every Uno head. Do this once per
environment before touching project files.

> The Uno port targets `net9.0-*` heads (see [ADR-4](README.md#2-architecture-decision-record)). The
> `.NET 10` SDK can build `net9.0` target frameworks, so a single recent SDK covers the whole `src/`
> stack.

---

## 1. .NET SDK

- [ ] Install the **.NET 10 SDK** (or .NET 9 SDK). Verify:
  ```bash
  dotnet --list-sdks
  ```
- [ ] Decide the library TFM. Default plan: `net9.0` heads (matches `KumikoUI.Core` / `KumikoUI.SkiaSharp`).

## 2. Uno Platform templates

- [ ] Install (or update) the Uno templates:
  ```bash
  dotnet new install Uno.Templates
  ```
- [ ] Confirm the templates are available:
  ```bash
  dotnet new list | grep -i uno
  # Expect: unoapp, unolib, unomauilib, unoapp-uitest
  ```
- [ ] (Optional) Inspect template options so the scaffold commands in [02](02-library-scaffold.md) and
  [06](06-sample-app.md) match your installed version:
  ```bash
  dotnet new unolib -h
  dotnet new unoapp -h
  ```

## 3. `uno-check` — environment doctor

`uno-check` validates and installs the workloads/SDK bits Uno needs. It is the canonical way to get
a machine build-ready.

- [ ] Install the tool:
  ```bash
  dotnet tool install --global uno.check
  ```
- [ ] Run it and apply fixes:
  ```bash
  uno-check
  ```

## 4. Workloads

Uno heads require these workloads. `uno-check` installs them, or do it manually:

- [ ] `wasm-tools` — WebAssembly (`net9.0-browserwasm`)
- [ ] `android` — Android (`net9.0-android`)
- [ ] `ios`, `maccatalyst` — Apple heads (macOS only)
- [ ] Verify:
  ```bash
  dotnet workload list
  ```

```bash
# manual install (skip if uno-check handled it)
dotnet workload install wasm-tools android ios maccatalyst
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

- [ ] `dotnet --list-sdks` shows .NET 9 or 10.
- [ ] `dotnet new list` shows `unolib` and `unoapp`.
- [ ] `uno-check` reports no required fixes (for the heads you intend to build on this machine).
- [ ] `dotnet workload list` shows the workloads for your target heads.
