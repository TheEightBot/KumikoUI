# Rendering Pipeline

This document describes how KumikoUI renders each frame, from the invalidation trigger through to the final canvas flush. It is written from `KumikoRenderer.RenderFrame` and the two hosts' paint code; the pass names below are the real method names, and `dotnet run --project eng/DocsIdentifiers -c Release` fails when one of them stops existing.

## Frame Lifecycle

```
host trigger (data, scroll, selection, edit, resize, style, ...)
  ↓
RequestRedraw()  (coalesced to one paint per frame, see "Surfaces and Frame Loop")
  ↓
the surface paints                          MAUI: SKGLView / SKCanvasView PaintSurface
                                            Uno:  SKCanvasElement.RenderOverride / SKXamlCanvas.PaintSurface
  ↓
KumikoDataGrid.RenderFrame(canvas, w, h)    using var ctx = ResourceCache.CreateContext(canvas)
  ↓
KumikoRenderer.RenderFrame(ctx, source, scroll, selection, style, drag state, editSession, popupManager)
```

`KumikoRenderer.Render(...)` is the same without popups (the overload for a custom host); the hosts call the internal `RenderFrame`, which also draws the popups their input controller has open.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ A. Layout (no drawing)                                                      │
│    SyncCellTextCulture · GetVisibleRowRangeWithOffset · ComputeColumnWidths │
│    FrozenPanes.Resolve · content size · ScrollState.ClampOffset             │
│    PaintCache.Update (rebuilds only on a style change) · CaptureFeedback    │
│ B. FillRect(PaintCache.Background)                                          │
│ C. DrawGroupingPanel (when grouped), above everything else                  │
│    Save · Translate below the panel                                         │
│    CaptureTransitions · PinnedGroupHeaders.Compute · PrepareCellTextCache   │
│ D. Pass 1, scrolling columns (clipped to the pane)                          │
│      DrawPaneRows (DrawRowBand: DrawRows → DrawCurrentCellBorder →          │
│      DrawGridLines) → DrawHeader → DrawSortIndicators → DrawFilterIcons →   │
│      DrawFrozenRows                                                         │
│ E. Pass 2, left-frozen columns: FillRect(FrozenColumnBackground), the same  │
│      five calls in the pane's clip, then DrawFrozenColumnDivider            │
│ F. Pass 3, right-frozen columns: DrawRightFrozenBackground, the same five   │
│      calls, then DrawRightFrozenColumnDivider                               │
│ G. DrawFrozenRowDivider                                                     │
│ H. DrawTableSummaryRows: top summaries, and bottom ones that scroll         │
│ I. Full-width rows, clipped to the data area:                               │
│      DrawGroupHeaderRow / DrawGroupSummaryRow for each visible group row    │
│ J. Sticky group headers: DrawGroupHeaderRow for each PinnedGroupHeaders     │
│      entry, innermost first                                                 │
│ K. DrawDragHandleColumn (when the row drag handle is on)                    │
│ L. DrawTableSummaryRows again for the pinned bottom summary (the footer),   │
│      when PinBottomSummaryRows                                              │
│ M. DrawColumnDragOverlay (a lifted or dropping header), DrawRowDragOverlay  │
│    Restore                                                                  │
│ N. EditSession.PlaceEditor + DrawEditor (absolute coordinates)              │
│ O. PopupManager.DrawPopups (filter, combo box, date picker)                 │
│ P. KumikoFrameStats overlay (when ShowFrameStats)                           │
└─────────────────────────────────────────────────────────────────────────────┘
```

Hit testing mirrors this order. `KumikoHitTester` resolves a point against the same geometry (panes, frozen rows, sticky headers, the pinned footer), and `InteractionGeometry` holds the sizes both sides share (filter icon, resize grip, group chevron, chips). What a frame measured while drawing, the grouping panel's chips, goes to the hit tester through `RenderedHitRegions` (`BeginGroupPanel`, `AddChip`), so a hit lands on what was drawn. A change to where something is drawn changes its hit region too.

## Surfaces and Frame Loop

Both hosts draw into a private surface and share one frame-loop design: every redraw signal goes through `RequestRedraw`; `KumikoFrameScheduler` (a platform-free state machine, one copy per host, unit-tested in `tests/KumikoUI.Maui.Tests` and `tests/KumikoUI.Uno.Tests`) decides whether to invalidate the surface now or wait for the next frame callback, coalesces requests into one paint per frame, and measures the time between frames.

### MAUI

`KumikoDataGrid.RenderMode` picks the SkiaSharp view the MAUI grid draws on (#71):

| Platform | `Auto` | `Gpu` | Paints on |
|----------|--------|-------|-----------|
| Android | `SKGLView` (OpenGL ES, a texture view) | same | GL thread: the UI thread records an `SKPicture`, the GL thread plays it back |
| Mac Catalyst | `SKGLView` (Metal) | same | UI thread |
| iOS | `SKCanvasView` | `SKGLView` (OpenGL ES, deprecated by Apple) | UI thread |
| Windows | `SKCanvasView` | `SKGLView` (ANGLE swap chain) | UI thread, synchronously per invalidation |

`ActiveRenderMode` reports the surface in use; `Software` always uses `SKCanvasView`. Changing `RenderMode` at run time removes the old view, disconnects its handler (releasing its GPU context) and redraws on the new one. If a device's GPU driver misdraws, set `RenderMode="Software"`. The surfaces are `KumikoGpuViewSurface` and `KumikoCanvasViewSurface`, created by `KumikoSurfaceFactory` (all in `src/KumikoUI.Maui/Rendering/`).

On iOS, Mac Catalyst and Android's software surface the platform folds invalidations into its next frame, so the surface is invalidated directly. Where an invalidation paints synchronously (Windows) or records a frame (the Android GPU surface), requests wait for the next platform frame callback and paint once. The frame callback (`KumikoFrameClock`) is `CADisplayLink` on iOS and Mac Catalyst (up to 120 Hz on ProMotion displays), `Choreographer` on Android and `CompositionTarget.Rendering` on Windows.

### Uno

`KumikoDataGrid.RenderMode` (`Auto`, `Hardware`, `Software`; `ActiveRenderMode` reports the result) picks between two `IKumikoSurface`s, created by `KumikoSurfaceFactory` (`src/KumikoUI.Uno/Rendering/`):

| Surface | Used when | Paints |
|---------|-----------|--------|
| `KumikoCanvasElementSurface` (Uno's `SKCanvasElement`) | `Hardware`, and `Auto` where `SKCanvasElement.IsSupportedOnCurrentPlatform()`: the Skia-rendered heads (desktop, WebAssembly, iOS and Android with the Skia renderer) | Straight into the window's canvas in logical units, GPU-accelerated where Uno renders on the GPU. The surface coalesces invalidations into the next frame by itself |
| `KumikoXamlCanvasSurface` (`SKXamlCanvas`) | `Software`, and wherever the hardware surface is unavailable (the only surface on the Windows App SDK head) | Into a device-pixel bitmap that is scaled to logical units and copied to the screen, synchronously on every invalidation |

Because the software surface paints synchronously, its requests wait for the next `CompositionTarget.Rendering` callback and paint once. A change of rasterization scale (`XamlRoot.Changed`) redraws the grid.

### Both hosts

The frame callback runs only while there is work: a fling, a row drag's edge autoscroll, an editor animation, or a deferred redraw, and stops on the first idle frame (a running callback keeps the display pipeline producing frames, so an idle grid must not subscribe it). Each frame passes the measured time since the previous one (clamped to 1–50 ms by `KumikoFrameScheduler`) to `UpdateInertialScroll`, `UpdateAutoScroll` and `EditSession.UpdateAnimation`. `InertialScroller` integrates the decaying velocity over that interval, so a fling travels the same distance at 60 and 120 Hz.

**Caret blink.** While an editor or a filter popup's search box has focus, a one-shot timer redraws just after each blink transition (every 530 ms, in phase with `DrawnTextBox`; `KumikoCaretBlink` in each host). Neither SkiaSharp view nor `SKCanvasElement` can invalidate a sub-rectangle: every paint covers the whole surface. On MAUI's software surface the first blink after a change keeps a copy of the frame (`KumikoCaretFrame`); later blinks copy it back and redraw the grid clipped to the editor and popup area, so only that area is rasterized (the layout walk still runs). The GPU surfaces and Uno redraw the whole frame on a blink, rasterized by the GPU or the software bitmap. With `ShowFrameStats` on, blinks redraw the whole frame so the overlay stays current.

**View rebuilds.** For a source over `KumikoSource.BackgroundRebuildThreshold` rows (the hosts default to 10,000), a sort, filter, grouping or summary change is rebuilt on a worker (see [ARCHITECTURE.md](ARCHITECTURE.md#background-view-rebuilds)), so no frame waits for it: frames keep drawing the current view, and when the finished view is swapped in the source raises `ViewChanged` and then `ViewUpdated`, both of which request a redraw. Rendering therefore reads `KumikoSource` only on the UI thread and never sees a half-built view.

## Invalidation Triggers

The hosts call `RequestRedraw` (internally `InvalidateSurface`) in response to:

| Trigger | Source |
|---------|--------|
| Data change | `INotifyCollectionChanged` on ItemsSource, `KumikoSource.ViewChanged`, `ViewUpdated` |
| Scroll | `InertialScroller` velocity step, touch drag, wheel |
| Selection | User tap, keyboard navigation, `KumikoSelectionModel.SelectionChanged` |
| Editing | Cell editor open/close, commit, `DrawnComponent` `RedrawRequested` (`InvalidateVisual`), caret blink |
| Resize | Column resize drag, viewport size change, display scale change |
| Sort / Filter / Group | Header tap, filter popup close |
| Style change | A style property change on the host |
| Expand / Collapse | Group header chevron tap; the slide transition steps on the frame callback |
| Hover and press | Pointer feedback (`InteractionFeedback`): a hover or pressed overlay on a row, cell, header or chip |
| Drag & Drop | Row or column drag in progress |

## Column Width Computation

`KumikoLayoutEngine.ComputeColumnWidths()` resolves column widths in order:

0. **Header fit** — each visible column's `HeaderFitWidth` is updated: header text measured in the header font through the frame's drawing context, plus header padding on both sides, the sort-arrow slot (12) when sortable, the filter-icon slot (14) when filterable, and a 4 px margin. The text width is cached on the column per (header text, font instance and font properties), so a steady-state frame measures nothing (#182).
1. **Content fit (Auto)** — each visible Auto column's `ContentFitWidth` is the wider of its header-fit width and its widest sampled cell text plus the cell padding on both sides and a 16 px buffer. The sample is the rows on screen plus the first and last `KumikoLayoutEngine.AutoFitSampleRows` (100) rows of the view, so it costs the same on any row count. The text width is cached on the column per (source, source content version, cell font), so it is measured on the first frame and on the first frame after a data or view change (anything that raises `ViewChanged`) or a cell-font change; steady frames and scrolling measure nothing (#24).
2. **Fixed / Auto** — a measured Auto column is its `ContentFitWidth` within `MinWidth`/`MaxWidth`. Otherwise an explicit `Width` (`HasExplicitWidth`) is used directly (DIP); without one, the column is `DefaultWidth` (120) or its header-fit width when wider, within `MinWidth`/`MaxWidth`.
3. **Star** — the width Fixed and Auto columns leave is shared by `StarWeight`. A Star column whose share falls outside its minimum (`MinWidth`, and the header-fit width capped at `MaxWidth` when it has no explicit `Width`) or its `MaxWidth` is held at that bound and the rest is shared again among the others. When the minimums don't fit the viewport, the content is wider than the viewport and the grid scrolls horizontally.

The resolved width is written back to each `KumikoColumn.Width` (layout writes don't make it explicit) and consumed by all subsequent draw and hit-test operations. A resize drag or an auto-fit (`KumikoRenderer.AutoFitColumn`, which samples like Auto) of a Star or Auto column switches it to Fixed at the chosen width, so the next pass keeps it.

## 3-Pass Frozen Column Rendering

Each "column pane" (scrolling, left-frozen, right-frozen) is rendered with the same five calls, over its own list of columns (`PopulateVisibleScrollableColumns`, `PopulateVisibleFrozenColumns`, `PopulateVisibleRightFrozenColumns`):

```
[Left-Frozen]  [Scrolling (clipped)]  [Right-Frozen]
     ↓                  ↓                   ↓
  DrawPaneRows       DrawPaneRows        DrawPaneRows     (DrawRows, DrawCurrentCellBorder, DrawGridLines)
  DrawHeader         DrawHeader          DrawHeader
  DrawSortIndicators DrawSortIndicators  DrawSortIndicators
  DrawFilterIcons    DrawFilterIcons     DrawFilterIcons
  DrawFrozenRows     DrawFrozenRows      DrawFrozenRows   (only when there are frozen rows)
```

The passes run in the order scrolling, left-frozen, right-frozen. Table summaries, group header and summary rows, sticky group headers, the drag handle column and the drag overlays are drawn once across the full width afterwards (see the lifecycle above), outside the pane clips.

**Clip regions** prevent scrollable content from bleeding into frozen areas. The left-frozen pane starts after the row drag handle (when on the left), the right-frozen pane is anchored to the right edge, and the scrolling pane is clipped to the region between them. Rows are further clipped to the data area (below the header and top summaries, above a pinned footer); the header is drawn after the rows with only the pane clip.

**Z-ordering**: By drawing scrolling first, then left-frozen, then right-frozen, frozen columns naturally overlay scrolling content at boundaries. Each frozen pane first fills its own background (`FrozenColumnBackground`, `RightFrozenColumnBackground`), and a divider line follows it.

**Panes that do not fit** (#268): before the passes, `FrozenPanes.Resolve` decides which frozen columns stay pinned in this viewport. When the visible columns are wider than the column area (the viewport less the row drag handle) and the left- and right-frozen columns together take more than `KumikoStyle.MaxFrozenColumnsWidthRatio` of it (two thirds by default), columns are unpinned until they fit: right-frozen ones first, from the inner edge of their pane, then left-frozen ones from the rightmost. An unpinned column is drawn in the scrolling pass, at the start of the scrolling columns when it was left-frozen and at their end when it was right-frozen, so the on-screen order (`KumikoSource.DisplayColumns`) does not change. The layout engine's pane lists, `KumikoScroller`, `KumikoHitTester` and the input controller all read the same per-column pane (`KumikoColumn.Pane`), so the panes never overlap and what is hit is what is drawn. The decision is made again every frame, so the columns are pinned again when the viewport widens.

**Group transitions** (#101): while a group slides open or shut, `DrawPaneRows` draws two bands: the rows up to the group's header as usual, and the rows after it moved by the slide and clipped below the header; the frame draws rows from below the viewport too while they slide up into it.

## DrawRows Detail

`DrawRows` iterates over the flat view within the visible row range of one pane, skipping group header and group summary rows (those are the full-width pass):

```csharp
for each visible row:
    skip if source.IsGroupHeaderRow(row) or source.IsGroupSummaryRow(row)
    dynamicRowStyle = style.RowStyleResolver?.Invoke(item)
    background paint: resolver color → alternate row → frozen pane background → PaintCache.Background
                      (selected: SelectionBackground; current row: FocusedRowBackground)
    ctx.FillRect(rowRect, background)                  // + the fade-in of a newly selected row

    DrawRowCells: for each column in the pane:
        cellStyle = ResolveCellStyle(...)              // cascade: cell resolver → column → grid
        per-column CellStyle background, if any
        skip the cell being edited                     // the editor draws on top
        renderer = column.CustomCellRenderer ?? GetCellRenderer(column.ColumnType)
        text = source.GetCachedCellText(row, item, column, out value)   // display text cache
        renderer.Render(ctx, in CellRenderContext)     // bounds, value, text, paints, resolver
```

After the rows, `DrawCurrentCellBorder` draws the current cell's border and `DrawGridLines` the lines. Group header rows (`DrawGroupHeaderRow`: chevron, key text, item count, caption summary) and group summary rows (`DrawGroupSummaryRow`) are drawn in the later full-width pass; table summaries by `DrawTableSummaryRows`, using `SummaryText` and `SummaryBackground`.

### Cell Renderers

Each column type selects an `ICellRenderer` (registered in the `KumikoRenderer` constructor, replaceable with `SetCellRenderer`); a column's `CustomCellRenderer` takes precedence:

| `KumikoColumnType` | Renderer | Behavior |
|------------|----------|----------|
| `Text`, `Numeric`, `Date`, `Template`, `ComboBox`, `Picker` | `TextCellRenderer` | Left/center/right aligned text with ellipsis, or wrapped when `KumikoColumn.TextWrap` is set |
| `Boolean` | `BooleanCellRenderer` | Draws a check box (checked, unchecked, indeterminate) inline |
| `Image` | `ImageCellRenderer` | Centered image with aspect ratio preservation, resolved through `IKumikoImageResolver` |
| `ProgressBar` | `ProgressBarCellRenderer` | A bar from the column's progress options |
| (action button columns) | `ActionButtonsCellRenderer` | Buttons laid out by `ActionButtonLayout`, an `IInteractiveCellRenderer` |
| `Template` with a custom renderer | user's `ICellRenderer` | `column.CustomCellRenderer` |

A renderer receives one `CellRenderContext`: `Bounds` (the padded cell), `Row`, `Value`, `DisplayText`, `Column`, `Style`, `CellStyle`, `IsSelected`, `Paints` (the frame's `PaintCache`), `TextPaint`, `TextAlignment`, `ImageResolver`, and, for interactive renderers, `PointerPosition` and `IsPointerPressed`. Its signature is `ICellRenderer.Render(IDrawingContext ctx, in CellRenderContext cell)`.

## PaintCache Optimization

`PaintCache` eliminates per-frame allocation of paint objects. `PaintCache.Update(style)` runs every frame but rebuilds the paints only when it is given another style instance or `KumikoStyle.Version` (incremented by every property change) changed, so a steady-state frame allocates none (#68):

```
KumikoStyle (+ Version)
       ↓
PaintCache.Update()  — rebuilds only on a new style instance or version
       ↓
the pre-computed KumikoPaint objects, each a property of the cache:
  ├─ Background, HeaderBackground, FrozenColumnBackground, RightFrozenColumnBackground
  ├─ SelectionBackground, FocusedRowBackground, AlternateRowBackground, FrozenRowBackground
  ├─ HeaderText, CellText
  ├─ GridLine, HeaderBorder, CurrentCellBorder, AccentStroke
  ├─ FrozenColumnDivider, RightFrozenColumnDivider, FrozenRowDivider
  ├─ SortIndicator, FilterIcon, FilterActiveIcon
  ├─ DragHandleBackground, DragHandleIcon
  ├─ GroupPanelBackground, GroupPanelText, GroupChipBackground, GroupChipBorder
  ├─ SummaryBackground, SummaryText
  ├─ CheckBoxBorder, CheckMark, CheckIndeterminate
  └─ HoverOverlay, PressedOverlay
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

1. `EditSession.BeginEdit()` captures the cell position, original value, and column type (and commits an edit that is already open first).
2. `CellEditorFactory.CreateEditor` creates the appropriate `DrawnComponent` editor (for example `DrawnTextBox` for text columns); `ApplyThemeToEditor` and `ApplyFontToEditor` style it.
3. The editor is positioned at the cell's viewport rect and registered with `DrawnFocusManager`. While the frame runs, `EditSession.PlaceEditor` keeps it on its cell (it moves when the grid scrolls, sorts or regroups) and hides it while the cell is scrolled out of its pane or band.
4. The cell's own content is skipped by `DrawRowCells`; the editor draws **on top of** the grid, in absolute coordinates, in step N of the lifecycle (`EditSession.DrawEditor`).
5. On commit: `EditSession.CommitEdit()` validates (`CellValidator`, `IDataErrorInfo`, `INotifyDataErrorInfo`), converts the value and writes it through `KumikoSource.SetCellValue`, which uses the column's `ValueSetter` or the property accessor from `KumikoAccessorRegistry`, and raises `CellEndEdit`.
6. On cancel: `EditSession.CancelEdit()` restores the original value.

An editor that animates is stepped by `EditSession.UpdateAnimation` from the host's frame callback.

## Popup Rendering

Popups (filter, combobox dropdown, datepicker calendar) draw in step O, the last one before the frame stats:

- `PopupManager` (internal, owned by `KumikoInputController`) maintains a stack of active popups.
- `PopupManager.DrawPopups` calls each popup component's `OnDraw(ctx)`; popups are placed by `PopupLayout` inside the component's `PopupViewport`.
- Popups are drawn last so they always appear above all grid content.
- Input is routed through `PopupManager` first: hits inside a popup are consumed; hits outside dismiss the popup (and still reach the grid).
