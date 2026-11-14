# KumikoUI — Copilot Instructions Index

## What This Project Is

**KumikoUI DataGrid** is a high-performance, fully canvas-drawn DataGrid for **.NET MAUI** and the **Uno Platform**.
Every visual element (cells, headers, editors, popups, scrollbars) is rendered directly on a SkiaSharp `SKCanvas`.
There are **zero native UIKit / AppKit / WinUI controls**: everything is drawn in software or on the GPU through Skia,
giving the same output on iOS, Android, Mac Catalyst, Windows, desktop (Skia) and WebAssembly.

Four packages: `KumikoUI.Core` (engine) ← `KumikoUI.SkiaSharp` (drawing) ← `KumikoUI.Maui` and `KumikoUI.Uno` (hosts); the
`[KumikoGridItem]` source generator ships inside `KumikoUI.Core`.

These files are checked by `dotnet run --project eng/DocsIdentifiers -c Release`: every backticked type, member and file
name must exist in the code. Run it after you rename or remove something they mention.

---

## Scoped Instruction Files

Detailed instructions live in [`.github/instructions/`](instructions/) and are
automatically injected based on which files are open. When a task spans multiple areas,
read the relevant files directly; each is self-contained.

---

### [architecture.instructions.md](instructions/architecture.instructions.md)
**Applies to:** all files (`**`)

Always-active baseline. Covers: the four packages and three layers (`Core` → `SkiaSharp` → `Maui` and `Uno`), strict
dependency rules, project paths, the namespace map, and repo-wide C# code style rules (nullable, sealed leaf types,
`readonly struct`, no LINQ or allocation in hot paths, `KumikoColor` usage, `float` for coordinates, public type names).

---

### [rendering.instructions.md](instructions/rendering.instructions.md)
**Applies to:** `src/KumikoUI.Core/Rendering/**`, `src/KumikoUI.SkiaSharp/**`, `src/KumikoUI.Core/KumikoRenderer.cs`, `src/KumikoUI.Core/Layout/**`

Everything about drawing. Covers: all `IDrawingContext` members, `KumikoPaint`/`KumikoColor`/`KumikoRect` usage,
`ICellRenderer` with `CellRenderContext` and how to attach custom renderers to columns, the `KumikoRenderer.RenderFrame`
pass order and z-order, hit testing's mirror of the drawing, `PaintCache`, `SkiaDrawingContext` internals (the
long-lived `SkiaResourceCache`, `CreateContext`, font fallback), `KumikoLayoutEngine` methods, and the performance rules
(zero allocation per frame, no LINQ, virtual scrolling).

---

### [components-and-editing.instructions.md](instructions/components-and-editing.instructions.md)
**Applies to:** `src/KumikoUI.Core/Components/**`, `src/KumikoUI.Core/Editing/**`

Everything about canvas-drawn editors. Covers: `DrawnComponent` lifecycle overrides, the signals (`InvalidateVisual`,
`RaiseValueChanged`, `RaiseEditCompleted`), `IValueEditor`, the `ApplyTheme` pattern, `CellEditorFactory` wiring,
`EditorDescriptor` for XAML-declarable editor config, the `EditSession` pipeline (begin → draw → commit/cancel),
validation via `IDataErrorInfo` or `CellValidator`, and `InertialScroller` physics.

---

### [models-and-data.instructions.md](instructions/models-and-data.instructions.md)
**Applies to:** `src/KumikoUI.Core/Models/**`

Everything about the data layer. Covers: all `KumikoColumn` properties and column types, the `KumikoSource` API
(get/set values, `BeginUpdate` batches, `SetSort`/`ToggleSort`, `SetColumnFilter`, `AddGroupDescription`, summaries,
background view rebuilds), `KumikoStyle` theming (built-in themes, `CellStyleResolver`, `RowStyleResolver`),
`KumikoCellStyle`/`KumikoRowStyle` nullable merge semantics, and step-by-step recipes for adding a new style property
or column type.

---

### [maui-control.instructions.md](instructions/maui-control.instructions.md)
**Applies to:** `src/KumikoUI.Maui/**`, `samples/**`

Everything about the MAUI host. Covers: `MauiProgram.UseKumikoUI()` bootstrap, required XAML namespaces, the
`KumikoDataGrid` bindable properties, XAML examples for every column type and summary rows, and `KumikoDataGrid`
internals (surfaces and the frame loop, platform keyboard and text-input handling, gesture timings).

---

### [uno-control.instructions.md](instructions/uno-control.instructions.md)
**Applies to:** `src/KumikoUI.Uno/**`, `samples/SampleApp.Uno/**`, `tests/KumikoUI.Uno.Tests/**`, `docs/uno/**`

Everything about the Uno host. Covers: the rules that keep it in parity with the MAUI host, the app setup (Skia renderer,
no startup call, `KumikoFonts`), XAML namespaces and properties, and `KumikoDataGrid` internals (the `SKCanvasElement` and
`SKXamlCanvas` surfaces, the frame loop, the hidden `TextBox` proxy and `KumikoTextInputAdapter`, keyboard avoidance,
lifecycle).

---

### [testing.instructions.md](instructions/testing.instructions.md)
**Applies to:** `tests/**`

Everything about writing tests. Covers: the test projects and what each runs, xUnit-only rules (no NUnit/MSTest),
file/class naming convention, `[Fact]` and `[Theory]` + `[InlineData]` templates, a concrete `KumikoSource` sorting
example, the rendering test doubles (`RecordingDrawingContext`, `NoOpDrawingContext`, `RenderHarness`), the
`Performance` category, allocation tests and golden images.

---

## Skill Prompts

Reusable task prompts live in [`.github/prompts/`](prompts/):

| Prompt | Use when... |
|---|---|
| [`add-cell-renderer.prompt.md`](prompts/add-cell-renderer.prompt.md) | Creating a new `ICellRenderer` |
| [`add-drawn-component.prompt.md`](prompts/add-drawn-component.prompt.md) | Creating a new canvas-drawn inline editor |
| [`add-column-type.prompt.md`](prompts/add-column-type.prompt.md) | Adding a new `KumikoColumnType` end-to-end |
| [`add-theme-property.prompt.md`](prompts/add-theme-property.prompt.md) | Adding a new `KumikoStyle` visual property |
| [`add-unit-test.prompt.md`](prompts/add-unit-test.prompt.md) | Writing xUnit tests for Core classes |
| [`configure-datagrid-features.prompt.md`](prompts/configure-datagrid-features.prompt.md) | Wiring sorting, filtering, grouping, or summaries |
| [`rendering-pipeline.prompt.md`](prompts/rendering-pipeline.prompt.md) | Debugging or extending the render pipeline |

---
