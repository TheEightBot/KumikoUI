# KumikoUI → Uno Platform Port

This folder contains the implementation plan for adding **Uno Platform** support to KumikoUI as a
set of executable checklists. Work through them in order; each document is self-contained and ends
with a checklist you can tick off top-to-bottom.

> **Status:** Planning artifact for review. No `KumikoUI.Uno` code exists yet — these documents
> describe exactly how it will be built. Implement only after this plan set is reviewed.

---

## 1. Why this works: the existing separation of concerns

KumikoUI is already structured so that adding a platform is a *thin* exercise. The library is split
into three layers, and **only the bottom layer is platform-specific**:

| Layer | Project | TFM | Knows about… | Reused for Uno? |
|---|---|---|---|---|
| Platform-agnostic engine | `KumikoUI.Core` | `net9.0` | nothing UI-specific — only `IDrawingContext`, models, layout, input dispatch | ✅ **unchanged** |
| Drawing backend | `KumikoUI.SkiaSharp` | `net9.0` | a bare `SKCanvas` | ✅ **unchanged** |
| Platform host | `KumikoUI.Maui` | `net10.0-*` | MAUI controls, native input | ➕ add a sibling `KumikoUI.Uno` |

The core insight: **`KumikoUI.SkiaSharp` only needs an `SKCanvas`** — it has no MAUI dependency.
Uno Platform exposes `SKXamlCanvas`, whose `PaintSurface` event hands you an `SKCanvas` through the
**identical** `SKPaintSurfaceEventArgs` type that MAUI's `SKCanvasView` uses. So the Uno port is a
near 1:1 mirror of the MAUI host that **reuses Core and SkiaSharp with zero changes**, preserving the
library's core tenets.

See also: [../ARCHITECTURE.md](../ARCHITECTURE.md) and [../RENDERING.md](../RENDERING.md).

---

## 2. Architecture Decision Record

| # | Decision | Rationale |
|---|---|---|
| ADR-1 | **Reuse `KumikoUI.SkiaSharp` via `SKXamlCanvas`** rather than writing a new `IDrawingContext`. | Maximum reuse, zero changes to Core/SkiaSharp, pixel-identical to MAUI, lowest risk. Drawing Skia-on-Skia natively on Uno's Skia targets anyway. |
| ADR-2 | **One cross-targeted `unolib`** covering all heads (Windows, Desktop/Skia, WASM, Android, iOS, Mac Catalyst). | Uno libraries cross-target by default; supporting every head costs almost nothing at the project level. |
| ADR-3 | **Scaffold everything with the `dotnet` CLI** (`dotnet new`, `dotnet sln add`, `dotnet add reference`). | Guarantees all Uno SDK plumbing is generated correctly; no hand-authored csproj drift. |
| ADR-4 | **`-tfm net9.0`** for `KumikoUI.Uno`, matching Core/SkiaSharp. | Keeps the whole `src/` stack on one runtime. The template defaults to `net10.0`; flipping is one flag if desired. |
| ADR-5 | **`KumikoUI.Maui` stays untouched.** | The two hosts are independent siblings; Uno work cannot regress MAUI. |

---

## 3. Dependency graph

```
                 KumikoUI.Core            (net9.0, zero deps — UNCHANGED)
                   ▲        ▲
                   │        │
      KumikoUI.SkiaSharp    │              (net9.0, refs Core + SkiaSharp — UNCHANGED)
            ▲   ▲           │
            │   └───────────┤
            │               │
   KumikoUI.Maui     KumikoUI.Uno          (host layer — Maui exists, Uno is new)
   (net10.0-*)       (net9.0-<heads>)
```

`KumikoUI.Uno` references **`KumikoUI.Core`** and **`KumikoUI.SkiaSharp`** only — exactly like
`KumikoUI.Maui` does.

---

## 4. Target matrix

`KumikoUI.Uno` cross-targets the Uno head TFMs. Representative set (exact monikers come from the
template — confirm after scaffolding):

| Head | TFM | Render path | Builds on |
|---|---|---|---|
| Windows (WinAppSDK / WinUI 3) | `net9.0-windows10.0.xxxxx.0` | `SKXamlCanvas` via `SkiaSharp.Views.WinUI` | Windows only |
| Desktop (Skia: Win/macOS/Linux) | `net9.0-desktop` | `SKXamlCanvas` via `SkiaSharp.Views.Uno.WinUI` | any OS |
| WebAssembly | `net9.0-browserwasm` | `SKXamlCanvas` via `SkiaSharp.Views.Uno.WinUI` | any OS (needs `wasm-tools`) |
| Android | `net9.0-android` | `SKXamlCanvas` via `SkiaSharp.Views.Uno.WinUI` | any OS (needs `android`) |
| iOS | `net9.0-ios` | `SKXamlCanvas` via `SkiaSharp.Views.Uno.WinUI` | macOS only |
| Mac Catalyst | `net9.0-maccatalyst` | `SKXamlCanvas` via `SkiaSharp.Views.Uno.WinUI` | macOS only |

> `SKXamlCanvas` lives in namespace `SkiaSharp.Views.Windows` in **both** the `SkiaSharp.Views.WinUI`
> (Windows) and `SkiaSharp.Views.Uno.WinUI` (all other heads) packages, so the host code is identical
> across targets. Only the `<PackageReference>` is conditional.

---

## 5. MAUI → Uno parity (the heart of the port)

| Concern | `KumikoUI.Maui` (existing) | `KumikoUI.Uno` (new) |
|---|---|---|
| Render surface | `SKCanvasView` | `SKXamlCanvas` |
| Paint event | `PaintSurface` → `e.Surface.Canvas` | `PaintSurface` → `e.Surface.Canvas` (**identical**) |
| Drawing bridge | `new SkiaDrawingContext(canvas)` | `new SkiaDrawingContext(canvas)` (**reused**) |
| Render call | `DataGridRenderer.Render(ctx, …)` | `DataGridRenderer.Render(ctx, …)` (**reused**) |
| Request redraw | `InvalidateSurface()` | `SKXamlCanvas.Invalidate()` |
| Pointer input | `SKCanvasView.Touch` | `UIElement.PointerPressed/Moved/Released`, `PointerWheelChanged` |
| Keyboard input | native responder / hidden `Entry` | `UIElement.KeyDown`/`KeyUp` + `CharacterReceived` |
| Inertial tick | `IDispatcherTimer` | `DispatcherQueue` / `DispatcherTimer` |
| Bindable surface | `BindableProperty` | `DependencyProperty` |
| Host hook | `builder.UseSkiaKumikoUI()` | `builder.UseKumikoUI()` (`IHostBuilder` / `IApplicationBuilder`) |

### Input wiring contract

Core is already the seam — the host only adapts framework events into Core calls:

| Uno event | Build | Core call (unchanged API) |
|---|---|---|
| `PointerPressed`/`Moved`/`Released`, `PointerWheelChanged` | `GridPointerEventArgs` | `InputController.HandlePointer(evt, scroll, selection, style, dataSource)` |
| `KeyDown`/`KeyUp`, `CharacterReceived` | `GridKeyEventArgs` (map `VirtualKey` → `GridKey`) | `InputController.HandleKey(evt, scroll, selection, style, dataSource)` |
| timer tick (~16 ms) | — | `InputController.UpdateInertialScroll(scroll, 16f)` |
| `InputController.NeedsRedraw` | — | `skXamlCanvas.Invalidate()` |

These are the exact members exercised by [`src/KumikoUI.Maui/DataGridView.cs`](../../src/KumikoUI.Maui/DataGridView.cs) today.

---

## 6. Master checklist

- [ ] **[01 — Prerequisites & tooling](01-prerequisites.md)** — SDKs, Uno templates, `uno-check`, workloads, per-OS build constraints.
- [ ] **[02 — Library scaffold](02-library-scaffold.md)** — `dotnet new unolib`, solution wiring, TFMs, conditional SkiaSharp.Views references, packaging metadata.
- [ ] **[03 — DataGridView host](03-datagridview-host.md)** — the control: `SKXamlCanvas` hosting, paint loop, `DependencyProperty` surface, lifecycle.
- [ ] **[04 — Input, keyboard & focus](04-input-keyboard-focus.md)** — pointer/keyboard/focus bridge into `GridInputController`, inertial scroll, per-head nuances.
- [ ] **[05 — Fonts & assets](05-fonts-and-assets.md)** — `SkiaFontRegistrar` reuse (CJK/icons), Uno library-asset packaging rules.
- [ ] **[06 — Sample app](06-sample-app.md)** — `dotnet new unoapp` `SampleApp.Uno`, font registration, demo pages, per-head run commands.
- [ ] **[07 — Tests](07-tests.md)** — unchanged Core xUnit suite + optional Uno.UITest runtime tests + smoke matrix.
- [ ] **[08 — CI/CD & packaging](08-ci-cd-packaging.md)** — extend `ci.yml`/`publish.yml`, workload restore, `nuget-uno` artifact, NuGet publish.

---

## 7. Reference material

- Uno — [How to Create Control Libraries](https://platform.uno/docs/articles/guides/how-to-create-control-libraries.html)
- Uno — [Creating Custom Controls](https://platform.uno/docs/articles/guides/creating-custom-controls.html)
- Uno — [`dotnet new` templates](https://platform.uno/docs/articles/get-started-dotnet-new.html)
- Uno — [`SKCanvasElement`](https://platform.uno/docs/articles/controls/SKCanvasElement.html) (a hardware-accelerated alternative to `SKXamlCanvas` on Skia heads; noted as a future optimization)
- Uno blog — [Porting a custom-drawn Xamarin.Forms control to Uno Platform](https://platform.uno/blog/porting-a-custom-drawn-xamarin-forms-control-to-uno-platform/) (the same SkiaSharp-reuse scenario)
- Uno sample — [Uno.Samples / UI / ControlLibrary](https://github.com/unoplatform/Uno.Samples/tree/master/UI/ControlLibrary)
- Microsoft Learn — [Templated controls with C# (WinUI 3)](https://learn.microsoft.com/windows/apps/winui/winui3/xaml-templated-controls-csharp-winui-3), [Dependency properties overview](https://learn.microsoft.com/windows/uwp/xaml-platform/dependency-properties-overview)
