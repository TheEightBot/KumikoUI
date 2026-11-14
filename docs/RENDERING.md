# Rendering Pipeline

This document describes how the Kumiko renders each frame, from the initial invalidation trigger through to the final canvas flush.

## Frame Lifecycle (18 steps)

```
MAUI Trigger
  ↓
RequestRedraw() (coalesced to one paint per frame, see "Surfaces and Frame Loop (MAUI)")
  ↓
SKGLView / SKCanvasView PaintSurface
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

## Surfaces and Frame Loop (MAUI)

`KumikoDataGrid.RenderMode` picks the SkiaSharp view the MAUI grid draws on (#71):

| Platform | `Auto` | `Gpu` | Paints on |
|----------|--------|-------|-----------|
| Android | `SKGLView` (OpenGL ES `TextureView`) | same | GL thread: the UI thread records an `SKPicture`, the GL thread plays it back |
| Mac Catalyst | `SKGLView` (Metal) | same | UI thread |
| iOS | `SKCanvasView` | `SKGLView` (OpenGL ES, deprecated by Apple) | UI thread |
| Windows | `SKCanvasView` | `SKGLView` (ANGLE swap chain) | UI thread, synchronously per invalidation |

`ActiveRenderMode` reports the surface in use; `Software` always uses `SKCanvasView`. Changing `RenderMode` at run time removes the old view, disconnects its handler (releasing its GPU context) and redraws on the new one. If a device's GPU driver misdraws, set `RenderMode="Software"`.

Every redraw signal goes through `RequestRedraw`. On iOS, Mac Catalyst and Android's software surface the platform folds invalidations into its next frame, so the surface is invalidated directly. Where an invalidation paints synchronously (Windows) or records a frame (the Android GPU surface), requests wait for the next platform frame callback and paint once.

The frame callback is `CADisplayLink` on iOS and Mac Catalyst (up to 120 Hz on ProMotion displays), `Choreographer` on Android and `CompositionTarget.Rendering` on Windows. It runs only while there is work — a fling, a row drag's edge autoscroll, an editor animation, or a deferred redraw — and stops on the first idle frame. Each frame passes the measured time since the previous one (clamped to 1–50 ms) to `UpdateInertialScroll`, `UpdateAutoScroll` and `EditSession.UpdateAnimation`. `InertialScroller` integrates the decaying velocity over that interval, so a fling travels the same distance at 60 and 120 Hz.

**Caret blink.** While an editor or a filter popup's search box has focus, a one-shot timer redraws just after each blink transition (every 530 ms, in phase with `DrawnTextBox`). Neither `SKCanvasView` nor `SKGLView` can invalidate a sub-rectangle: every paint covers the whole surface. On the software surface the first blink after a change keeps a copy of the frame; later blinks copy it back and redraw the grid clipped to the editor and popup area, so only that area is rasterized (the layout walk still runs). The GPU surfaces redraw the whole frame on a blink, rasterized by the GPU. With `ShowFrameStats` on, blinks redraw the whole frame so the overlay stays current.

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
1. **Content fit (Auto)** — each visible Auto column's `ContentFitWidth` is the wider of its header-fit width and its widest sampled cell text plus the cell padding on both sides and a 16 px buffer. The sample is the rows on screen plus the first and last `KumikoLayoutEngine.AutoFitSampleRows` (100) rows of the view, so it costs the same on any row count. The text width is cached on the column per (source, source content version, cell font), so it is measured on the first frame and on the first frame after a data or view change (anything that raises `ViewChanged`) or a cell-font change; steady frames and scrolling measure nothing (#24).
2. **Fixed / Auto** — a measured Auto column is its `ContentFitWidth` within `MinWidth`/`MaxWidth`. Otherwise an explicit `Width` (`HasExplicitWidth`) is used directly (DIP); without one, the column is `DefaultWidth` (120) or its header-fit width when wider, within `MinWidth`/`MaxWidth`.
3. **Star** — the width Fixed and Auto columns leave is shared by `StarWeight`. A Star column whose share falls outside its minimum (`MinWidth`, and the header-fit width capped at `MaxWidth` when it has no explicit `Width`) or its `MaxWidth` is held at that bound and the rest is shared again among the others. When the minimums don't fit the viewport, the content is wider than the viewport and the grid scrolls horizontally.

The resolved width is written back to each `KumikoColumn.Width` (layout writes don't make it explicit) and consumed by all subsequent draw and hit-test operations. A resize drag or an auto-fit (`KumikoRenderer.AutoFitColumn`, which samples like Auto) of a Star or Auto column switches it to Fixed at the chosen width, so the next pass keeps it.

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

**Panes that do not fit** (#268): before the passes, `FrozenPanes.Resolve` decides which frozen columns stay pinned in this viewport. When the visible columns are wider than the column area (the viewport less the row drag handle) and the left- and right-frozen columns together take more than `KumikoStyle.MaxFrozenColumnsWidthRatio` of it (two thirds by default), columns are unpinned until they fit: right-frozen ones first, from the inner edge of their pane, then left-frozen ones from the rightmost. An unpinned column is drawn in the scrolling pass, at the start of the scrolling columns when it was left-frozen and at their end when it was right-frozen, so the on-screen order (`KumikoSource.DisplayColumns`) does not change. The layout engine's pane lists, `KumikoScroller`, `KumikoHitTester` and the input controller all read the same per-column pane (`KumikoColumn.Pane`), so the panes never overlap and what is hit is what is drawn. The decision is made again every frame, so the columns are pinned again when the viewport widens.

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
one per control, start every frame with `cache.CreateContext(canvas)`, and dispose it when the
MAUI handler disconnects or the Uno control unloads). `CreateContext` reuses one context object, so a
steady-state frame allocates no managed memory at all (`new SkiaDrawingContext(canvas, cache)` allocates a
context object per frame, a few dozen bytes). The cache has 3 tiers:

1. **SKTypeface cache** — Keyed by `(family, bold, italic)`. Resolved from `SkiaFontRegistrar` first (the registered face closest to the style), then the font manager (falling back to the default family in the same style). Font-manager typefaces are reference counted across caches; `SKTypeface.Default` and registered typefaces are never disposed by the cache.
2. **SKFont cache** — Keyed by `(family, size, bold, italic)`; each font belongs to its typeface entry and is dropped with it. A bold font on a face lighter than semibold gets `SKFont.Embolden`, an italic font on an upright face gets `SKFont.SkewX = -0.25` (synthetic styles).
3. **SKPaint cache** — Keyed by `(color, strokeWidth, style, isAntialias)`.

Each `IDrawingContext` method (e.g., `FillRect`, `DrawText`) resolves its `KumikoPaint` to an `SKPaint`
and `SKFont` via the cache, so steady-state frames create no native objects. The cache also keeps what text
and polygons are drawn from: one `SKTextBlob` per cached text run (building a blob allocates, drawing one does
not) and one `SKPath` per polygon shape (a path that is changed and drawn again allocates a snapshot per
draw). `SkiaAllocationTests` renders a full-featured grid through the context into an `SKSurface` and
asserts 0 managed bytes per steady-state frame. Each tier is a
least-recently-used map with a capacity (defaults 512 paints, 128 fonts, 32 typefaces), so a renderer that
keeps producing new colors or font sizes cannot grow it without bound. Objects evicted during a frame are
disposed when the frame ends (`SkiaDrawingContext.Dispose`), never in the middle of it.

`SkiaFontRegistrar` keeps every live cache (weakly). Registering, replacing, unregistering or clearing a
family increments `SkiaFontRegistrar.Version` and makes each cache drop that family's typefaces and fonts
*before* the registrar disposes anything it owned, so a cache never holds a disposed typeface and the next
frame resolves the family again. `new SkiaDrawingContext(canvas)` (no cache) still works: it uses a private
cache that is disposed with the context, which recreates everything every frame.

### Text measurement cache

The same `SkiaResourceCache` also keeps text measurements across frames (#23), so redrawing unchanged text measures nothing with Skia:

- **Widths** — keyed by `(text, family, size, bold, italic)`: the advance width of the text, used by `MeasureText` and by `DrawTextInRect` to decide whether the text fits.
- **Layouts** — keyed by `(text, font, width, wrap mode, line count)`: the immutable `KumikoTextLines` a text is drawn as when it does not fit as is (cut with an ellipsis, wrapped, or holding line breaks or tabs).

Each is a least-recently-used map with `MaxTextMeasurements` entries (default 4096). Keys hold the font's values, not its instance, so a style change that picks another font simply stops using the old entries; a changed cell value is a new text and a new key. Any `SkiaFontRegistrar` change and `Clear()` drop every text entry. A hit allocates nothing; a miss measures, and lays out, once. (`SKCanvas.DrawText` still allocates a small managed wrapper inside SkiaSharp per call.)

### Ellipsis and wrapping

`KumikoTextLayout` (Core) lays text out for any `IDrawingContext`. Every cut falls between two text elements (`StringInfo.GetNextTextElementLength`), so emoji, surrogate pairs and combining sequences are never split; the cut point is found by a binary search over those boundaries. Text wider than its rectangle ends with "…" at any width (there is no minimum width anymore); just the ellipsis is drawn when no character fits beside it. On one line, line breaks and tabs are drawn as spaces and other control characters are dropped, instead of as missing-glyph boxes.

`IDrawingContext.DrawTextInRect(…, KumikoTextWrap wrap)` and `MeasureText(text, paint, maxWidth, wrap)` add wrapping (`None`, `Word`, `Character`): line breaks start new lines, as many lines as fit the rectangle's height are drawn, and the last ends with an ellipsis when there is more. Both are default interface members, so other implementations keep compiling (the defaults lay out through `MeasureText` and allocate per call). Text cells wrap when `KumikoColumn.TextWrap` is set; rows keep their fixed height (#102 adds variable row heights).

### Font fallback

A typeface rarely has every glyph a grid shows: Latin fonts have no CJK or emoji, and Android's default font manager surfaces neither through the family name. `SkiaDrawingContext` therefore draws each line run by run (#74):

- **Segmentation** — the line is walked one text element (grapheme cluster) at a time, so an emoji ZWJ sequence, a flag, a skin tone or a letter with combining marks always stays in one run. An element goes to the requested typeface when it has the element's glyphs (`SKFont.ContainsGlyph`), else to the first of `SkiaFontRegistrar.FallbackFamilies` that has them (registered or installed families, in order), else to `SKFontManager.MatchCharacter(family, style, [UI language], codepoint)`. Controls and default-ignorable characters (joiners, variation selectors) stay in the run around them. A character no typeface has stays in the requested typeface (drawn as its missing glyph).
- **Fast path** — text the requested typeface covers entirely is one `ContainsGlyphs` check the first time it is measured, and is drawn with one `DrawText` as before.
- **Caching** — the face chosen for each `(family, bold, italic, codepoint)` is cached, the fallback typefaces live in a small per-cache table (at most 64; font-manager typefaces are leased from the same pool as primary ones), and the runs of a text (their ends, faces and widths) are stored with its width and with each line of its layout. A steady-state frame segments and measures nothing; a run's font is a cache hit. Fallback fonts use the requested size and synthesize bold or italic like primary fonts.
- **Measuring** — widths are the sum of the runs' widths, and `KumikoTextLayout` measures through the same run-aware measurer, so ellipsis cuts and wrapping work across runs.
- **Invalidation** — any `SkiaFontRegistrar` change (including `SetFallbackFamilies`) and `Clear()` empty the fallback table together with the text entries whose runs refer to it.

Runs are drawn from the font's character map, one glyph per code point, so ligatures, Arabic joining and Indic reordering are not applied (full shaping with `SkiaSharp.HarfBuzz` is tracked in #111). The exception is emoji sequences (#259): a run that holds a skin-tone modifier, a ZWJ sequence (a family), a flag (two regional indicators, or a tag sequence), a variation selector or a keycap is shaped with HarfBuzz (`SKShaper`) so the emoji font's one glyph for the sequence is drawn, where the character map alone would draw the base emoji and the modifier (a color swatch) side by side. Only those runs are shaped, once: the shaped width is cached with the run's measurement and the glyphs with its blob. If HarfBuzz's native library cannot be loaded, the process falls back to the glyph by glyph drawing and logs a trace warning. Each run is drawn from an `SKTextBlob` built the first time it is drawn and kept with the cached text, so drawing it again allocates nothing.

### Registered styles

`SkiaFontRegistrar.RegisterTypeface(family, typeface, SKFontStyle)` and `RegisterTypefaceFromStream(family, stream, SKFontStyle)` register one face per weight, width and slant under a family (for example NotoSansJP regular and bold). A font resolves to the closest registered face, matching width, then slant, then weight (lighter first up to 500, heavier first above), and synthesizes a weight or slant the face lacks. The two-argument overloads keep their behavior: they replace every face of the family with one typeface, used in its own style.

## Glyphs: Polygons and Polylines

Small icons (group expand/collapse triangles, sort arrows, dropdown and picker arrows) are not built from separate `DrawLine` strokes, whose butt caps leave notches at the corners. `IDrawingContext.FillPolygon(ReadOnlySpan<KumikoPoint>, KumikoPaint)` fills a closed, anti-aliased polygon, and `DrawPolyline` strokes an open path with round joins and caps. Disclosure triangles and sort arrows use `FillPolygon`; open chevrons, checkmarks and the closed filter funnel outline use `DrawPolyline`. Strokes that are genuinely separate (the "x" remove button, plus and minus, separators) stay `DrawLine`. Call sites build their points in a `stackalloc` span, and `SkiaDrawingContext` reuses a single `SKPath`, so glyphs add no per-frame allocation. Both members are default interface members: an implementer that does not override them still renders the glyphs, as outlines drawn with `DrawLine`.

## Scroll & Viewport

### Coordinate System
- Origin `(0, 0)` is the top-left of the grid viewport.
- `ScrollState.OffsetX/Y` represent the logical scroll position (positive = scrolled right/down).
- All row/column coordinates are computed relative to the viewport, not the data extent.

### Right-to-Left Limitation (1.0)
Right-to-left layout is not supported in 1.0. The grid always lays out and draws left to right, also inside a right-to-left page or container, and Hebrew or Arabic cell text is drawn without bidi reordering or shaping. Each host pins its canvas element (`SKCanvasView` on MAUI, `SKXamlCanvas` on Uno) to `FlowDirection.LeftToRight`, so an RTL ancestor does not mirror the drawing, and Uno takes pointer coordinates relative to that canvas element so hit-testing matches what is drawn. Mirroring is planned for 1.x (see the RTL spec, #80).

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
