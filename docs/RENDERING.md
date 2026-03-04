# Rendering Pipeline

This document describes how the Kumiko renders each frame, from the initial invalidation trigger through to the final canvas flush.

## Frame Lifecycle (18 steps)

```
MAUI Trigger
  ↓
SKCanvasView.InvalidateSurface()
  ↓
OnPaintSurface(SKPaintSurfaceEventArgs)
  ↓
DataGridRenderer.Render(IDrawingContext, width, height)
  ↓
┌──────────────────────────────────────────────────────┐
│ 1. Compute effective grouping & summary descriptors  │
│ 2. Compute row drag offsets (if dragging)            │
│ 3. ComputeColumnWidths (Auto/Star/Fixed)             │
│ 4. ClampOffset (scroll bounds)                       │
│ 5. PaintCache.Update (style → ~60 GridPaint objects) │
│ 6. Fill background                                   │
│ 7. Draw grouping panel (if grouped)                  │
│ 8. SCROLLABLE COLUMNS                                │
│    ├─ DrawColumnHeaders                              │
│    ├─ DrawRows (data + group headers + summaries)    │
│    ├─ DrawFrozenRowDividers                          │
│    ├─ DrawFrozenRows                                 │
│    ├─ DrawTableSummaries                             │
│    ├─ DrawGroupHeaders                               │
│    └─ DrawGroupSummaries                             │
│ 9. LEFT-FROZEN COLUMNS (same 7 draw calls)           │
│10. RIGHT-FROZEN COLUMNS (same 7 draw calls)          │
│11. Draw frozen row dividers                          │
│12. Draw table summaries                              │
│13. Draw group headers (overlays)                     │
│14. Draw group summaries (overlays)                   │
│15. Draw row drag handle column                       │
│16. Draw drag overlays                                │
│17. Draw active editor                                │
│18. Draw popups (filter, combobox, datepicker)        │
└──────────────────────────────────────────────────────┘
```

## Invalidation Triggers

The grid calls `InvalidateSurface()` in response to:

| Trigger | Source |
|---------|--------|
| Data change | `INotifyCollectionChanged` on ItemsSource |
| Scroll | `InertialScroller` velocity tick, touch drag |
| Selection | User tap, keyboard navigation |
| Editing | Cell editor open/close, commit |
| Resize | Column resize drag, viewport size change |
| Sort / Filter / Group | Header tap, filter popup close |
| Style change | BindableProperty change on DataGridView |
| Expand / Collapse | Group header chevron tap |
| Drag & Drop | Row drag in progress |

## Column Width Computation

`GridLayoutEngine.ComputeColumnWidths()` resolves column widths in order:

1. **Fixed** — Column's `Width` value is used directly (DIP).
2. **Auto** — Measures header text + padding + sort/filter icons. If data rows are available, samples up to the first N visible rows.
3. **Star** — Remaining width after Fixed + Auto columns is distributed proportionally by star factor.

The result is a `float[]` array indexed by column ordinal, consumed by all subsequent draw and hit-test operations.

## 3-Pass Frozen Column Rendering

Each "column pane" (scrollable, left-frozen, right-frozen) is rendered identically:

```
[Left-Frozen]  [Scrollable (clipped)]  [Right-Frozen]
     ↓                   ↓                    ↓
  DrawColumnHeaders   DrawColumnHeaders    DrawColumnHeaders
  DrawRows            DrawRows             DrawRows
  DrawFrozenRowDivs   DrawFrozenRowDivs    DrawFrozenRowDivs
  DrawFrozenRows      DrawFrozenRows       DrawFrozenRows
  DrawTableSummaries  DrawTableSummaries   DrawTableSummaries
  DrawGroupHeaders    DrawGroupHeaders     DrawGroupHeaders
  DrawGroupSummaries  DrawGroupSummaries   DrawGroupSummaries
```

**Clip regions** prevent scrollable content from bleeding into frozen areas. Left-frozen has `x: 0`, right-frozen is anchored to the right edge, and scrollable content is clipped to the region between them.

**Z-ordering**: By drawing scrollable first, then left-frozen, then right-frozen, frozen columns naturally overlay scrollable content at boundaries.

## DrawRows Detail

`DrawRows` iterates over the flat view within the visible row range:

```csharp
for each visible FlatViewRow:
    switch row.Kind:
        case Data:
            for each visible column:
                → Resolve CellStyle (cascade: cell → column → grid)
                → PaintCache.CellBackgroundPaint(style, isAlternate, isSelected)
                → context.FillRect(cellRect, backgroundPaint)
                → cellRenderer.Draw(context, cellRect, value, paint)
                → context.DrawLine(gridLinePaint)  // if grid lines enabled

        case GroupHeader:
            → Draw expand chevron + group key text + item count

        case GroupSummary:
            → Draw per-column summary values (sum, avg, count, etc.)
```

### Cell Renderers

Each column type selects an `ICellRenderer`:

| Column Type | Renderer | Behavior |
|------------|----------|----------|
| TextColumn | TextCellRenderer | Left/center/right aligned text with truncation |
| BooleanColumn | BooleanCellRenderer | Draws `DrawnCheckBox` inline |
| ImageColumn | ImageCellRenderer | Centered image with aspect ratio preservation |
| ProgressBarColumn | ProgressBarCellRenderer | Draws `DrawnProgressBar` inline |
| TemplateColumn | (custom) | User-supplied ICellRenderer |

## PaintCache Optimization

`PaintCache` eliminates per-frame allocation of paint objects:

```
DataGridStyle (immutable input)
       ↓
PaintCache.Update()
       ↓
~60 pre-computed GridPaint objects:
  ├─ BackgroundPaint
  ├─ HeaderBackgroundPaint
  ├─ HeaderTextPaint
  ├─ CellTextPaint
  ├─ SelectedRowPaint
  ├─ GridLinePaint
  ├─ FrozenDividerPaint
  ├─ GroupHeaderPaint
  ├─ SummaryPaint
  └─ ... (filter icons, drag handles, etc.)
```

Factory methods like `CellBackgroundPaint(style, isAlternate, isSelected)` and `CellTextPaint(style)` handle dynamic per-cell overrides by constructing a `GridPaint` from cached base values with minimal property mutation.

## SkiaDrawingContext Caching

`SkiaDrawingContext` maintains 3 tiers of native object caches:

1. **SKTypeface cache** — Keyed by `(familyName, bold, italic)`. Created on first use, disposed at context disposal.
2. **SKFont cache** — Keyed by `(typeface, size)`. Created per frame, disposed at frame end.
3. **SKPaint cache** — Keyed by `(color, strokeWidth, style, isAntialias)`. Created per frame, disposed at frame end.

Each `IDrawingContext` method (e.g., `FillRect`, `DrawText`) resolves its `GridPaint` to an `SKPaint` via the cache, avoiding per-call native allocations.

## Scroll & Viewport

### Coordinate System
- Origin `(0, 0)` is the top-left of the grid viewport.
- `ScrollState.OffsetX/Y` represent the logical scroll position (positive = scrolled right/down).
- All row/column coordinates are computed relative to the viewport, not the data extent.

### Visible Range Computation
```
firstVisibleRow = (int)(offsetY / rowHeight)
lastVisibleRow  = firstVisibleRow + (int)(viewportHeight / rowHeight) + 1

Column range computed by GridLayoutEngine, walking column widths from
the scroll offset until cumulative width exceeds viewport width.
```

### Scroll Clamping (ClampOffset)
```
MaxOffsetX = totalColumnsWidth - viewportWidth + frozenLeftWidth + frozenRightWidth
MaxOffsetY = totalRowsHeight - viewportHeight + headerHeight + frozenRowsHeight

OffsetX = clamp(OffsetX, 0, max(0, MaxOffsetX))
OffsetY = clamp(OffsetY, 0, max(0, MaxOffsetY))
```

## Editing Overlay

When a cell enters edit mode:

1. `EditSession.BeginEdit()` captures the cell position, original value, and column type.
2. `CellEditorFactory` creates the appropriate `DrawnComponent` editor (e.g., `DrawnTextBox` for text columns).
3. The editor is positioned at the cell's viewport rect and registered with `FocusManager`.
4. The editor draws **on top of** the cell in step 17 of the pipeline.
5. On commit: `EditSession.CommitEdit()` validates, writes back via compiled property setter, and fires `CellEditEnded`.
6. On cancel: `EditSession.CancelEdit()` restores original value.

## Popup Rendering

Popups (filter, combobox dropdown, datepicker calendar) draw in step 18, the final step:

- `PopupManager` maintains a stack of active popups.
- Each popup provides a `Draw(context, rect)` method.
- Popups are drawn last to always appear above all grid content.
- Input is routed through `PopupManager` first — hits inside a popup are consumed; hits outside dismiss the popup.
