---
applyTo: "**"
---

# KumikoUI — Architecture & Project Structure

## What This Is

**KumikoUI** is a high-performance, fully canvas-drawn DataGrid for .NET MAUI. Every visual
element is rendered on a SkiaSharp `SKCanvas` — zero native UIKit / AppKit / WinUI controls,
pixel-perfect across iOS, Android, macOS Catalyst, and Windows.

---

## Three-Layer Architecture

```
KumikoUI.Core         net9.0          — platform-agnostic models, layout, rendering pipeline
KumikoUI.SkiaSharp    net9.0          — IDrawingContext → SKCanvas implementation
KumikoUI.Maui         net10.0-*maui*  — DataGridView control, SKCanvasView host
```

**Dependency direction — strict, never violate:**
```
KumikoUI.Core  ←  KumikoUI.SkiaSharp  ←  KumikoUI.Maui
```

| Layer | May reference | Must NOT reference |
|---|---|---|
| `KumikoUI.Core` | BCL only | SkiaSharp, MAUI, any platform SDK |
| `KumikoUI.SkiaSharp` | Core + `SkiaSharp` NuGet | MAUI, platform SDKs |
| `KumikoUI.Maui` | Core, SkiaSharp, MAUI | (no restriction) |

---

## Project Paths

| Purpose | Path |
|---|---|
| Core models, layout, rendering abstractions | `src/KumikoUI.Core/` |
| SkiaSharp drawing context | `src/KumikoUI.SkiaSharp/` |
| MAUI control + hosting | `src/KumikoUI.Maui/` |
| Sample app | `samples/SampleApp.Maui/` |
| xUnit tests | `tests/KumikoUI.Core.Tests/` |

---

## Namespace Map

| Namespace | Key types |
|---|---|
| `KumikoUI.Core.Models` | `DataGridColumn`, `DataGridStyle`, `DataGridSource`, `SelectionModel`, `ScrollState`, `CellStyle`, `RowStyle`, `FilterDescription`, `GroupDescription`, `TableSummaryRow`, `SummaryColumnDescription` |
| `KumikoUI.Core.Rendering` | `IDrawingContext`, `ICellRenderer`, `GridRect`, `GridColor`, `GridPaint`, `GridFont`, `GridSize`, `GridFontMetrics`, `PaintStyle`, `GridTextAlignment`, `GridVerticalAlignment` |
| `KumikoUI.Core.Layout` | `GridLayoutEngine` |
| `KumikoUI.Core.Input` | `GridInputController`, `InertialScroller`, `ScrollSettings`, `GridPointerEventArgs`, `GridKeyEventArgs`, `HitTesting` |
| `KumikoUI.Core.Editing` | `EditSession`, `CellEditorFactory`, `EditorDescriptor`, `EditTrigger`, `CellValidationResult` |
| `KumikoUI.Core.Components` | `DrawnComponent`, `DrawnTextBox`, `DrawnComboBox`, `DrawnDatePicker`, `DrawnNumericUpDown`, `DrawnScrollPicker`, `DrawnCheckBox`, `DrawnFilterPopup`, `FocusManager`, `PopupManager` |
| `KumikoUI.SkiaSharp` | `SkiaDrawingContext` |
| `KumikoUI.Maui` | `DataGridView`, `DataGridHostingExtensions` |

---

## Code Style (applies everywhere)

- **C# 12+**, nullable reference types enabled project-wide.
- All public API members have `/// <summary>` XML doc comments.
- `readonly struct` for value types (`GridRect`, `GridSize`, `CellPosition`).
- `record struct` for cache keys in `SkiaDrawingContext`.
- Prefer `switch` expressions over `if/else` chains for type dispatch.
- **No LINQ** inside render-loop hot paths — use `for` loops.
- Use `float` for all coordinate/dimension math (not `double`).
- Use `GridColor(r, g, b[, a])` everywhere — never hex strings, never `SKColor`, never `Color` from MAUI.
