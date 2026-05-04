# KumikoUI — Copilot Instructions Index

## What This Project Is

**KumikoUI** is a high-performance, fully canvas-drawn DataGrid for .NET MAUI. Every visual
element — cells, headers, editors, popups, scrollbars — is rendered directly on a SkiaSharp
`SKCanvas`. There are **zero native UIKit / AppKit / WinUI controls**; everything is drawn
in software, giving pixel-perfect, identical output across iOS, Android, macOS Catalyst, and
Windows.

---

## Scoped Instruction Files

Detailed instructions live in [`.github/instructions/`](.github/instructions/) and are
automatically injected based on which files are open. When a task spans multiple areas,
read the relevant files directly — each is self-contained.

---

### [architecture.instructions.md](.github/instructions/architecture.instructions.md)
**Applies to:** all files (`**`)

Always-active baseline. Covers: three-layer project structure (`Core` → `SkiaSharp` → `Maui`),
strict dependency rules, full namespace map, and repo-wide C# code style rules (nullable,
`readonly struct`, no LINQ in hot paths, `GridColor` usage, `float` for coordinates).

---

### [rendering.instructions.md](.github/instructions/rendering.instructions.md)
**Applies to:** `src/KumikoUI.Core/Rendering/**`, `src/KumikoUI.SkiaSharp/**`, `src/KumikoUI.Core/DataGridRenderer.cs`

Everything about drawing. Covers: all `IDrawingContext` methods, `GridPaint`/`GridColor`/`GridRect`
usage, `ICellRenderer` interface + how to attach custom renderers to columns, `DataGridRenderer`
render-loop structure and z-order, `PaintCache`, `SkiaDrawingContext` internals (per-frame
paint/font cache, SkiaSharp 3.x API), `GridLayoutEngine` layout methods, and performance rules
(no allocation, no LINQ, virtual scrolling).

---

### [components-and-editing.instructions.md](.github/instructions/components-and-editing.instructions.md)
**Applies to:** `src/KumikoUI.Core/Components/**`, `src/KumikoUI.Core/Editing/**`

Everything about canvas-drawn editors. Covers: `DrawnComponent` lifecycle overrides, the three
signals (`InvalidateVisual`, `RaiseValueChanged`, `RaiseEditCompleted`), the `ApplyTheme` pattern,
`CellEditorFactory` wiring (three required cases per editor type), `EditorDescriptor` for XAML-
declarable editor config, `EditSession` pipeline (begin → draw → commit/cancel), validation via
`IDataErrorInfo` or delegate, and `InertialScroller` physics.

---

### [models-and-data.instructions.md](.github/instructions/models-and-data.instructions.md)
**Applies to:** `src/KumikoUI.Core/Models/**`

Everything about the data layer. Covers: all `DataGridColumn` properties and column types,
`DataGridSource` API (get/set values, bulk updates, sorting, filtering, grouping, summaries),
`DataGridStyle` theming (built-in themes, `CellStyleResolver`, `RowStyleResolver`), `CellStyle`/
`RowStyle` nullable merge semantics, and step-by-step recipes for adding a new style property
or column type.

---

### [maui-control.instructions.md](.github/instructions/maui-control.instructions.md)
**Applies to:** `src/KumikoUI.Maui/**`, `samples/**`

Everything about consuming the control. Covers: `MauiProgram.UseSkiaKumikoUI()` bootstrap,
required XAML namespaces, all `DataGridView` bindable properties, XAML examples for every column
type (Numeric, Text, ComboBox, Picker, Date, Boolean, Template with renderer, Template with
`EditorDescriptor`), summary row XAML, and `DataGridView` internals (keyboard proxy, platform-
specific gesture/IME handling, timer setup).

---

### [testing.instructions.md](.github/instructions/testing.instructions.md)
**Applies to:** `tests/**`

Everything about writing tests. Covers: xUnit-only rules (no NUnit/MSTest), file/class naming
convention, `[Fact]` and `[Theory]+[InlineData]` templates, a concrete `DataGridSource` sorting
example, and the `FakeDrawingContext` no-op test double needed to test renderers and components
without SkiaSharp.

---

## Skill Prompts

Reusable task prompts live in [`.github/prompts/`](.github/prompts/):

| Prompt | Use when... |
|---|---|
| [`add-cell-renderer.prompt.md`](.github/prompts/add-cell-renderer.prompt.md) | Creating a new `ICellRenderer` |
| [`add-drawn-component.prompt.md`](.github/prompts/add-drawn-component.prompt.md) | Creating a new canvas-drawn inline editor |
| [`add-column-type.prompt.md`](.github/prompts/add-column-type.prompt.md) | Adding a new `DataGridColumnType` end-to-end |
| [`add-theme-property.prompt.md`](.github/prompts/add-theme-property.prompt.md) | Adding a new `DataGridStyle` visual property |
| [`add-unit-test.prompt.md`](.github/prompts/add-unit-test.prompt.md) | Writing xUnit tests for Core classes |
| [`configure-datagrid-features.prompt.md`](.github/prompts/configure-datagrid-features.prompt.md) | Wiring sorting, filtering, grouping, or summaries |
| [`rendering-pipeline.prompt.md`](.github/prompts/rendering-pipeline.prompt.md) | Debugging or extending the render pipeline |

---
