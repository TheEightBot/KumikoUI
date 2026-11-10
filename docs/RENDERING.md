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
KumikoRenderer.Render(IDrawingContext, width, height)
  ↓
┌──────────────────────────────────────────────────────┐
│ 1. Compute effective grouping & summary descriptors  │
│ 2. Compute row drag offsets (if dragging)            │
│ 3. ComputeColumnWidths (Auto/Star/Fixed)             │
│ 4. ClampOffset (scroll bounds)                       │
│ 5. PaintCache.Update (rebuilt only on a style change) │
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
│    Sticky group headers (pinned at the data top)     │
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
| Style change | BindableProperty change on KumikoDataGrid |
| Expand / Collapse | Group header chevron tap |
| Drag & Drop | Row drag in progress |

## Column Width Computation

`KumikoLayoutEngine.ComputeColumnWidths()` resolves column widths in order:

0. **Header fit** — each visible column's `HeaderFitWidth` is updated: header text measured in the header font through the frame's drawing context, plus header padding on both sides, the sort-arrow slot (12) when sortable, the filter-icon slot (14) when filterable, and a 4 px margin. The text width is cached on the column per (header text, font instance and font properties), so a steady-state frame measures nothing (#182).
1. **Fixed / Auto** — an explicit `Width` (`HasExplicitWidth`) is used directly (DIP). Without one, the column is `DefaultWidth` (120) or its header-fit width when wider, within `MinWidth`/`MaxWidth`. Auto is laid out like Fixed for now (#24).
2. **Star** — the width Fixed and Auto columns leave is shared by `StarWeight`. A Star column whose share falls outside its minimum (`MinWidth`, and the header-fit width capped at `MaxWidth` when it has no explicit `Width`) or its `MaxWidth` is held at that bound and the rest is shared again among the others. When the minimums don't fit the viewport, the content is wider than the viewport and the grid scrolls horizontally.

The resolved width is written back to each `KumikoColumn.Width` (layout writes don't make it explicit) and consumed by all subsequent draw and hit-test operations.

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
                → Resolve KumikoCellStyle (cascade: cell → column → grid)
                → PaintCache.CellBackgroundPaint(style, isAlternate, isSelected)
                → context.FillRect(cellRect, backgroundPaint)
                → source.GetCachedCellText(row, item, column, out value)  // display text cache
                → cellRenderer.Render(context, in CellRenderContext)    // bounds, value, text, shared paints
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

`PaintCache` eliminates per-frame allocation of paint objects. `PaintCache.Update(style)` runs every frame but rebuilds the paints only when it is given another style instance or `KumikoStyle.Version` (incremented by every property change) changed, so a steady-state frame allocates none (#68):

```
KumikoStyle (+ Version)
       ↓
PaintCache.Update()  — rebuilds only on a new style instance or version
       ↓
~60 pre-computed KumikoPaint objects:
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

Per-row and per-cell overrides (resolver backgrounds, cell style text color or font, per-column header styles) are drawn with a few paints the renderer reuses, setting their color before each draw; `PaintCache.CellTextPaint(cellStyle, style)` returns the shared cell text paint, or its one reused override paint. Cell renderers receive the cache as `CellRenderContext.Paints`.

## Display Text Cache

`KumikoSource` caches the value and formatted text of each drawn cell, per column and visible row (frozen rows have their own slots; scrolling rows share a ring twice the visible row count). An entry is reused while its row shows the same item and its stamp is current, so redrawing unchanged data reads, boxes and formats nothing. It is invalidated per item by `PropertyChanged` and `SetCellValue`, and entirely by a view rebuild, `Refresh()`, a collection reset or a culture change; a change to a column's `Format`, `PropertyName` or `ValueAccessor` replaces that column's cache. Value types are still boxed once when a cell is first read: the accessors return `object?`.

## SkiaDrawingContext Caching

`SkiaDrawingContext` is a thin per-frame wrapper over the `SKCanvas`. The native objects it draws with
live in a `SkiaResourceCache` that the host control owns for its lifetime (the MAUI and Uno grids create
one per control, pass it to `new SkiaDrawingContext(canvas, cache)` every frame, and dispose it when the
MAUI handler disconnects or the Uno control unloads). The cache has 3 tiers:

1. **SKTypeface cache** — Keyed by `(family, bold, italic)`. Resolved from `SkiaFontRegistrar` first, then the font manager (falling back to the default family in the same style). Font-manager typefaces are reference counted across caches; `SKTypeface.Default` and registered typefaces are never disposed by the cache.
2. **SKFont cache** — Keyed by `(family, size, bold, italic)`; each font belongs to its typeface entry and is dropped with it.
3. **SKPaint cache** — Keyed by `(color, strokeWidth, style, isAntialias)`.

Each `IDrawingContext` method (e.g., `FillRect`, `DrawText`) resolves its `KumikoPaint` to an `SKPaint`
and `SKFont` via the cache, so steady-state frames create no native objects. Each tier is a
least-recently-used map with a capacity (defaults 512 paints, 128 fonts, 32 typefaces), so a renderer that
keeps producing new colors or font sizes cannot grow it without bound. Objects evicted during a frame are
disposed when the frame ends (`SkiaDrawingContext.Dispose`), never in the middle of it.

`SkiaFontRegistrar` keeps every live cache (weakly). Registering, replacing, unregistering or clearing a
family increments `SkiaFontRegistrar.Version` and makes each cache drop that family's typefaces and fonts
*before* the registrar disposes anything it owned, so a cache never holds a disposed typeface and the next
frame resolves the family again. `new SkiaDrawingContext(canvas)` (no cache) still works: it uses a private
cache that is disposed with the context, which recreates everything every frame.

## Scroll & Viewport

### Coordinate System
- Origin `(0, 0)` is the top-left of the grid viewport.
- `ScrollState.OffsetX/Y` represent the logical scroll position (positive = scrolled right/down).
- All row/column coordinates are computed relative to the viewport, not the data extent.

### Visible Range Computation
```
firstVisibleRow = (int)(offsetY / rowHeight)
lastVisibleRow  = firstVisibleRow + (int)(viewportHeight / rowHeight) + 1

Column range computed by KumikoLayoutEngine, walking column widths from
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
3. The editor is positioned at the cell's viewport rect and registered with `DrawnFocusManager`.
4. The editor draws **on top of** the cell in step 17 of the pipeline.
5. On commit: `EditSession.CommitEdit()` validates, writes back via compiled property setter, and fires `CellEditEnded`.
6. On cancel: `EditSession.CancelEdit()` restores original value.

## Popup Rendering

Popups (filter, combobox dropdown, datepicker calendar) draw in step 18, the final step:

- `PopupManager` maintains a stack of active popups.
- Each popup provides a `Draw(context, rect)` method.
- Popups are drawn last to always appear above all grid content.
- Input is routed through `PopupManager` first — hits inside a popup are consumed; hits outside dismiss the popup.
