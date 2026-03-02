# Architecture Overview

This document describes the architecture of the .NET MAUI SkiaSharp Kumiko for contributors and maintainers.

## Project Structure

```
MauiKumiko/
├── src/
│   ├── Kumiko.Core/           # Platform-independent core (net9.0)
│   │   ├── Components/          # Custom-drawn UI controls (10 types)
│   │   ├── Data/                # Data source management (future)
│   │   ├── Editing/             # Cell editing pipeline
│   │   ├── Input/               # Input routing & hit testing
│   │   ├── Layout/              # Column width & viewport computation
│   │   ├── Models/              # Data models, columns, styles, selection
│   │   └── Rendering/           # Drawing abstractions, cell renderers, paint cache
│   ├── Kumiko.SkiaSharp/      # SkiaSharp backend (net9.0)
│   └── Kumiko.Maui/           # .NET MAUI integration (net10.0 multi-target)
│       ├── Hosting/             # Service registration extensions
│       └── Platforms/           # Per-platform keyboard handling
├── samples/
│   └── SampleApp.Maui/         # Demo application
├── tests/
│   └── Kumiko.Core.Tests/    # Unit tests
└── docs/                        # This documentation
```

## Dependency Graph

```
Kumiko.Core (net9.0, zero external dependencies)
       ↑
Kumiko.SkiaSharp (net9.0 → SkiaSharp 3.119.2)
       ↑
Kumiko.Maui (net10.0-ios/android/maccatalyst → MAUI + SkiaSharp.Views.Maui.Controls)
```

**Kumiko.Core** has no dependency on any rendering or UI framework. All rendering operations go through the `IDrawingContext` abstraction, making the core portable to any backend (SkiaSharp, Direct2D, CoreGraphics, HTML Canvas, etc.).

## Key Classes

### Core Infrastructure

| Class | Responsibility |
|-------|---------------|
| `KumikoRenderer` | Orchestrates the entire drawing pipeline — layout, painting, overlays |
| `KumikoSource` | Data management — filtering, sorting, grouping, summaries, change observation |
| `GridLayoutEngine` | Column width computation (Auto/Star/Fixed), visible row/column ranges |
| `HitTesting` | Resolves pointer coordinates to one of 14 `HitRegion` types |
| `PaintCache` | Pre-computes ~60 `GridPaint` objects per frame from `KumikoStyle` |

### Input & Selection

| Class | Responsibility |
|-------|---------------|
| `GridInputController` | Routes pointer and keyboard events to appropriate handlers |
| `SelectionModel` | Row/cell selection state, Single/Multiple/Extended modes, navigation |
| `FocusManager` | Single-component focus model with Tab cycling |
| `PopupManager` | Popup lifecycle, z-order, input priority routing |
| `InertialScroller` | Physics-based scroll deceleration |

### Editing

| Class | Responsibility |
|-------|---------------|
| `EditSession` | Cell edit lifecycle — begin, commit, cancel, validation |
| `CellEditorFactory` | Creates appropriate `DrawnComponent` editor for each column type |
| `EditorDescriptor` | XAML-declarable editor configuration (e.g., `NumericUpDownEditorDescriptor`) |

### Rendering Abstractions

| Class | Responsibility |
|-------|---------------|
| `IDrawingContext` | Platform-independent API: rectangles, lines, text, images, clipping |
| `SkiaDrawingContext` | `SKCanvas` implementation with 3-tier native object caching |
| `ICellRenderer` | Per-column-type cell content renderer (Text, Boolean, Image, ProgressBar) |
| `GridColor` / `GridFont` / `GridPaint` / `GridRect` | Platform-independent value types |

### MAUI Integration

| Class | Responsibility |
|-------|---------------|
| `KumikoView` | MAUI control — hosts `SKCanvasView`, translates touch/scroll/keyboard events |
| `KumikoHostingExtensions` | `builder.UseSkiaKumiko()` service registration |

## Drawn Components

Every visual control inside the grid is custom-drawn on the SkiaSharp canvas — no native UIKit, AppKit, or WinUI controls are used.

| Component | Purpose |
|-----------|---------|
| `DrawnTextBox` | Single-line text editor with cursor, selection, copy/paste |
| `DrawnCheckBox` | Three-state checkbox (checked/unchecked/indeterminate) |
| `DrawnComboBox` | Filterable dropdown with keyboard navigation |
| `DrawnDatePicker` | Calendar popup with month navigation and optional time picker |
| `DrawnNumericUpDown` | [−] value [+] with auto-repeat and keyboard shortcuts |
| `DrawnProgressBar` | Read-only bar or interactive slider with drag thumb |
| `DrawnScrollPicker` | Mobile-style scroll wheel with physics-based snapping |
| `DrawnFilterPopup` | Excel-style filter with search, checkboxes, and sort options |

## Architectural Patterns

### Zero Native Controls
Every UI element is drawn directly on the SkiaSharp canvas. This ensures pixel-perfect consistency across platforms and avoids the complexity of native control lifecycle management.

### Clean Rendering Abstraction
`IDrawingContext` isolates all drawing from SkiaSharp. Adding a new backend requires only implementing this interface and providing a host view.

### 3-Pass Frozen Column Rendering
Scrollable, left-frozen, and right-frozen columns are each rendered in separate clip regions with identical draw call sequences. This maintains correct z-ordering while keeping the code DRY.

### PaintCache Allocation Elimination
~60 `GridPaint` objects are pre-computed once per frame from `KumikoStyle`. Factory methods (`BackgroundPaint`, `CellTextPaint`) handle dynamic per-cell overrides without heap allocation during rendering.

### 3-Tier Native Object Caching (SkiaDrawingContext)
`SKPaint`, `SKFont`, and `SKTypeface` instances are cached by value-type keys (struct with color/size/style) and disposed at frame end.

### Compiled Expression Tree Property Accessors
`KumikoSource.BuildAccessor` compiles `Func<object, object?>` from dotted property paths (e.g., `"Address.City"`) via `Expression.Property` chains. Compiled delegates are cached by `"TypeName.PropertyPath"`.

### Popup Input Priority System
`PopupManager` intercepts all input before the grid. A hit on a popup is consumed. A press outside a popup dismisses all popups but does **not** consume the event, so the grid still processes the click.

### Cascading Style Resolution
`CellStyle` has nullable properties with `Merge(primary, fallback)`. Resolution order:
1. Per-cell dynamic style (`CellStyleResolver`)
2. Column `CellStyle`
3. Grid defaults from `KumikoStyle`

### O(1) Incremental Updates
`KumikoSource.TryIncrementalUpdate` handles simple `INotifyCollectionChanged` Add/Remove operations without a full `RebuildView()` when no sort, filter, group, or summary transforms are active.

### Physics-Based Scrolling
`InertialScroller` uses exponential moving average velocity tracking with configurable friction and frame-normalized deceleration for smooth momentum scrolling.

### Platform Keyboard Convergence
Three different platform input mechanisms converge to the same `GridKeyEventArgs` → `HandleKey` path:
- **iOS/macOS** — `KeyInputResponder` (UIView with `UIKeyCommand` + `IUIKeyInput`)
- **Android** — Hidden `Entry` with text content diffing and IME configuration
- **Windows** — Hidden `Entry` with text content diffing

### Virtual Scrolling
Only visible rows and columns are computed and drawn. Row range: `scrollOffset / rowHeight` to `(scrollOffset + viewportHeight) / rowHeight`. Column range computed by `GridLayoutEngine`.

### Grouped Flat View
Hierarchical group data is flattened into `List<FlatViewRow>` with `FlatRowKind` (Data/GroupHeader/GroupSummary). Group paths use pipe-delimited composite keys. Expansion state tracked via dictionary.

### HitTest Region System
14 `HitRegion` types (Cell, Header, ColumnResizeHandle, FilterIcon, SortIndicator, FrozenDivider, GroupPanel, GroupChip, RowDragHandle, TableSummary, GroupHeader, GroupSummary, FrozenRow, None) are resolved across 3 column panes by `HitTesting`.
