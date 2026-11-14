# KumikoUI → Uno Platform Port: design record

This folder holds the plan the Uno host (`KumikoUI.Uno`) was built from: eight phase documents, written as
checklists before the code existed. The host is built and shipped in the repository. **Read the
[Uno user guide](../README.md) for how to use it**; read this folder for why it is shaped the way it is.

> **Status: historical design record.** `KumikoUI.Uno` was implemented from these notes (PR #9) and has changed
> since (net10.0 heads, `SKCanvasElement`, the text-input adapter, keyboard avoidance). The checkboxes inside
> the phase documents show what was done at the time, not what is true now, and they are not updated. Where a
> note and the code disagree, the code and the [user guide](../README.md) win. Items the notes left open
> (interactive verification on each head) are tracked in #67, not here.

---

## 1. Why this works: the existing separation of concerns

KumikoUI is already structured so that adding a platform is a *thin* exercise. The library is split
into three layers, and **only the bottom layer is platform-specific**:

| Layer | Project | TFM | Knows about… | Reused for Uno? |
|---|---|---|---|---|
| Platform-agnostic engine | `KumikoUI.Core` | `net10.0` | nothing UI-specific — only `IDrawingContext`, models, layout, input dispatch | ✅ **unchanged** |
| Drawing backend | `KumikoUI.SkiaSharp` | `net10.0` | a bare `SKCanvas` | ✅ **unchanged** |
| Platform host | `KumikoUI.Maui` | `net10.0-*` | MAUI controls, native input | ➕ add a sibling `KumikoUI.Uno` |

The core insight: **`KumikoUI.SkiaSharp` only needs an `SKCanvas`** — it has no MAUI dependency.
Uno Platform exposes `SKXamlCanvas`, whose `PaintSurface` event hands you an `SKCanvas` through the
**identical** `SKPaintSurfaceEventArgs` type that MAUI's `SKCanvasView` uses. So the Uno port is a
near 1:1 mirror of the MAUI host that **reuses Core and SkiaSharp with zero changes**, preserving the
library's core tenets.

See also: [../../ARCHITECTURE.md](../../ARCHITECTURE.md) and [../../RENDERING.md](../../RENDERING.md).

> **Post-port Core change (1):** `KumikoUI.Core` was later extended with one shared rendering option —
> `KumikoStyle.PinBottomSummaryRows` (default `true`), which pins `Position=Bottom` table summary rows
> to the viewport bottom as a fixed footer. This is a deliberate feature (requested by the author) that
> also changes `KumikoUI.Maui` behavior, since both share Core. The port's "Core unchanged" property
> otherwise holds; this is the only Core edit, and it is additive (the `Render(...)` signature is
> unchanged and the flag defaults on).

---

## 2. Architecture Decision Record

| # | Decision | Rationale |
|---|---|---|
| ADR-1 | **Reuse `KumikoUI.SkiaSharp` via `SKXamlCanvas`** rather than writing a new `IDrawingContext`. | Maximum reuse, zero changes to Core/SkiaSharp, pixel-identical to MAUI, lowest risk. Drawing Skia-on-Skia natively on Uno's Skia targets anyway. |
| ADR-2 | **One cross-targeted `unolib`** covering all heads (Windows, Desktop/Skia, WASM, Android, iOS). | Uno libraries cross-target by default; supporting every head costs almost nothing at the project level. |
| ADR-3 | **Scaffold everything with the `dotnet` CLI** (`dotnet new`, `dotnet sln add`, `dotnet add reference`). | Guarantees all Uno SDK plumbing is generated correctly; no hand-authored csproj drift. |
| ADR-4 | **`net10.0`** heads for `KumikoUI.Uno`, matching Core/SkiaSharp. | Keeps the whole `src/` stack on one runtime. Originally `-tfm net9.0`; retargeted to `net10.0` in #10 because .NET 9 support ends 2026-11-10 (.NET 10 is LTS to 2028-11-14). |
| ADR-5 | **`KumikoUI.Maui` stays untouched.** | The two hosts are independent siblings; Uno work cannot regress MAUI. |

---

## 3. Dependency graph

```
                 KumikoUI.Core            (net10.0, zero deps — UNCHANGED)
                   ▲        ▲
                   │        │
      KumikoUI.SkiaSharp    │              (net10.0, refs Core + SkiaSharp — UNCHANGED)
            ▲   ▲           │
            │   └───────────┤
            │               │
   KumikoUI.Maui     KumikoUI.Uno          (host layer — Maui exists, Uno is new)
   (net10.0-*)       (net10.0-<heads>)
```

`KumikoUI.Uno` references **`KumikoUI.Core`** and **`KumikoUI.SkiaSharp`** only — exactly like
`KumikoUI.Maui` does.

---

## 4. Target matrix

`KumikoUI.Uno` cross-targets the Uno head TFMs. The shipped set:

| Head | TFM | Render path | Builds on |
|---|---|---|---|
| Windows (WinAppSDK / WinUI 3) | `net10.0-windows10.0.26100` | `SKXamlCanvas` via `SkiaSharp.Views.WinUI` (the only surface) | Windows only |
| Desktop (Skia: Win/macOS/Linux) | `net10.0-desktop` | `SKCanvasElement` (default) or `SKXamlCanvas` | any OS |
| WebAssembly | `net10.0-browserwasm` | `SKCanvasElement` (default) or `SKXamlCanvas` | any OS (needs `wasm-tools`) |
| Android | `net10.0-android` | `SKCanvasElement` (default) or `SKXamlCanvas` | any OS (needs `android`) |
| iOS | `net10.0-ios` | `SKCanvasElement` (default) or `SKXamlCanvas` | macOS only |

> `SKXamlCanvas` lives in namespace `SkiaSharp.Views.Windows` in **both** the `SkiaSharp.Views.WinUI`
> (Windows) and `SkiaSharp.Views.Uno.WinUI` (all other heads) packages, so the host code is identical
> across targets. Only the `<PackageReference>` is conditional.

---

## 5. MAUI → Uno parity (the heart of the port)

| Concern | `KumikoUI.Maui` (existing) | `KumikoUI.Uno` (new) |
|---|---|---|
| Render surface | `SKCanvasView` | `SKCanvasElement` on the Skia heads, `SKXamlCanvas` elsewhere (`RenderMode`, #65) |
| Paint event | `PaintSurface` → `e.Surface.Canvas` | `SKCanvasElement.RenderOverride(canvas, area)` (logical units) / `SKXamlCanvas.PaintSurface` (device pixels, scaled to logical) |
| Drawing bridge | `cache.CreateContext(canvas)` | `cache.CreateContext(canvas)` (**reused**; one `SkiaResourceCache` per control) |
| Render call | `KumikoRenderer.Render(ctx, …)` | `KumikoRenderer.Render(ctx, …)` (**reused**) |
| Request redraw | `InvalidateSurface()` | coalesced to one paint per frame (`KumikoDataGrid.Rendering.cs`) |
| Pointer input | `SKCanvasView.Touch` | `UIElement.PointerPressed/Moved/Released`, `PointerWheelChanged` |
| Keyboard input | native responder / hidden `Entry` | a hidden `TextBox` proxy: `TextChanging`, composition events, `KeyDown` |
| Inertial tick | `IDispatcherTimer` | `CompositionTarget.Rendering`, subscribed only while animating |
| Bindable surface | `BindableProperty` | `DependencyProperty` |
| Host hook | `builder.UseKumikoUI()` | none needed (`SKXamlCanvas` self-hosts; register fonts with `KumikoFonts`) |

### Input wiring contract

Core is already the seam — the host only adapts framework events into Core calls:

| Uno event | Build | Core call (unchanged API) |
|---|---|---|
| `PointerPressed`/`Moved`/`Released`, `PointerWheelChanged` | `KumikoPointerEventArgs` | `InputController.HandlePointer(evt, scroll, selection, style, dataSource)` |
| proxy `KeyDown` (command keys), `TextChanging` and composition events (text) | `KumikoKeyEventArgs` (map `VirtualKey` → `KumikoKey`), `ITextInputSession` calls | `InputController.HandleKey(evt, scroll, selection, style, dataSource)` |
| timer tick (~16 ms) | — | `InputController.UpdateInertialScroll(scroll, 16f)` |
| `InputController.NeedsRedraw` | — | `skXamlCanvas.Invalidate()` |

These are the exact members exercised by [`src/KumikoUI.Maui/KumikoDataGrid.cs`](../../../src/KumikoUI.Maui/KumikoDataGrid.cs) today.

---

## 6. Master checklist

- [x] **[01 — Prerequisites & tooling](01-prerequisites.md)** — SDKs, Uno templates, `uno-check`, workloads, per-OS build constraints.
- [x] **[02 — Library scaffold](02-library-scaffold.md)** — `dotnet new unolib`, solution wiring, TFMs, conditional SkiaSharp.Views references, packaging metadata.
- [x] **[03 — KumikoDataGrid host](03-kumikodatagrid-host.md)** — the control: `SKXamlCanvas` hosting, paint loop, `DependencyProperty` surface, lifecycle.
- [x] **[04 — Input, keyboard & focus](04-input-keyboard-focus.md)** — pointer/keyboard/focus bridge into `KumikoInputController`, inertial scroll, per-head nuances.
- [x] **[05 — Fonts & assets](05-fonts-and-assets.md)** — `SkiaFontRegistrar` reuse (CJK/icons), Uno library-asset packaging rules.
- [x] **[06 — Sample app](06-sample-app.md)** — `dotnet new unoapp` `SampleApp.Uno`, font registration, demo pages, per-head run commands.
- [x] **[07 — Tests](07-tests.md)** — unchanged Core xUnit suite + optional Uno.UITest runtime tests + smoke matrix.
- [x] **[08 — CI/CD & packaging](08-ci-cd-packaging.md)** — extend `ci.yml`/`publish.yml`, workload restore, `nuget-uno` artifact, NuGet publish.

---

## 7. Reference material

- Uno — [How to Create Control Libraries](https://platform.uno/docs/articles/guides/how-to-create-control-libraries.html)
- Uno — [Creating Custom Controls](https://platform.uno/docs/articles/guides/creating-custom-controls.html)
- Uno — [`dotnet new` templates](https://platform.uno/docs/articles/get-started-dotnet-new.html)
- Uno — [`SKCanvasElement`](https://platform.uno/docs/articles/controls/SKCanvasElement.html) (a hardware-accelerated alternative to `SKXamlCanvas` on Skia heads; the default surface there since #65)
- Uno blog — [Porting a custom-drawn Xamarin.Forms control to Uno Platform](https://platform.uno/blog/porting-a-custom-drawn-xamarin-forms-control-to-uno-platform/) (the same SkiaSharp-reuse scenario)
- Uno sample — [Uno.Samples / UI / ControlLibrary](https://github.com/unoplatform/Uno.Samples/tree/master/UI/ControlLibrary)
- Microsoft Learn — [Templated controls with C# (WinUI 3)](https://learn.microsoft.com/windows/apps/winui/winui3/xaml-templated-controls-csharp-winui-3), [Dependency properties overview](https://learn.microsoft.com/windows/uwp/xaml-platform/dependency-properties-overview)
