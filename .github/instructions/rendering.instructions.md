---
applyTo: "src/KumikoUI.Core/Rendering/**,src/KumikoUI.SkiaSharp/**,src/KumikoUI.Core/KumikoRenderer.cs,src/KumikoUI.Core/Layout/**"
---

# KumikoUI — Rendering Pipeline

The long form, with diagrams, is [docs/RENDERING.md](../../docs/RENDERING.md). This file is the rule set.

## `IDrawingContext` — The Only Drawing API

All drawing goes through `IDrawingContext`. **Never call SkiaSharp APIs from Core.**
`SkiaDrawingContext` (in `KumikoUI.SkiaSharp`) maps these to `SKCanvas`.

```csharp
void DrawRect(KumikoRect rect, KumikoPaint paint);
void FillRect(KumikoRect rect, KumikoPaint paint);
void DrawRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint);
void FillRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint);
void DrawLine(float x1, float y1, float x2, float y2, KumikoPaint paint);
void DrawText(string text, float x, float y, KumikoPaint paint);
void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
    KumikoTextAlignment hAlign = KumikoTextAlignment.Left,
    KumikoVerticalAlignment vAlign = KumikoVerticalAlignment.Center,
    bool clip = true);
KumikoSize MeasureText(string text, KumikoPaint paint);
KumikoFontMetrics GetFontMetrics(KumikoPaint paint);
void ClipRect(KumikoRect rect);
void Save();
void Restore();
void Translate(float dx, float dy);
void DrawImage(object image, KumikoRect destRect);

// Default interface members (an implementer need not override them):
void FillPolygon(ReadOnlySpan<KumikoPoint> points, KumikoPaint paint);      // closed, anti-aliased glyphs
void DrawPolyline(ReadOnlySpan<KumikoPoint> points, KumikoPaint paint);     // open path, round joins and caps
void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint, KumikoTextAlignment hAlign,
    KumikoVerticalAlignment vAlign, bool clip, KumikoTextWrap wrap);        // wrapping draw
KumikoSize MeasureText(string text, KumikoPaint paint, float maxWidth, KumikoTextWrap wrap);
```

Icons and arrows are polygons or polylines (`FillPolygon`, `DrawPolyline`), built in a `stackalloc` span, not
separate `DrawLine` strokes. Text that must be cut or wrapped goes through `KumikoTextLayout`, which never splits a
text element (emoji, combining sequence).

---

## `KumikoPaint` — Describe how to draw

`KumikoPaint` is a sealed class with `Color`, `StrokeWidth`, `IsAntiAlias` and `Font`. Whether a call fills or
strokes is the `IDrawingContext` method (`FillRect` or `DrawRect`), not a paint property.

```csharp
var paint = new KumikoPaint
{
    Color       = new KumikoColor(r, g, b),   // or (r, g, b, a)
    StrokeWidth = 1.5f,
    IsAntiAlias = true,
    Font        = new KumikoFont("Default", 13, bold: false)   // KumikoFont is a readonly record struct
};
```

Never construct `SKPaint`, `SKFont`, or `SKColor` in Core. `SkiaDrawingContext` draws with Skia objects from the
host's long-lived `SkiaResourceCache`; they are reused across frames. In the render loop never create a
`KumikoPaint` either: take one from `PaintCache` (below), or from a `PaintMemo` in a drawn component (internal; it hands
back the same instance for the same values), or reuse a field.

## `KumikoColor`

```csharp
new KumikoColor(r, g, b)      // alpha = 255
new KumikoColor(r, g, b, a)   // explicit alpha
KumikoColor.White / KumikoColor.Black / KumikoColor.Transparent
```

## `KumikoRect`

```csharp
new KumikoRect(x, y, width, height)
rect.Inflate(dx, dy)    // expanded copy
rect.Offset(dx, dy)     // shifted copy
rect.Contains(px, py)   // hit test
rect.Left / .Top / .Right / .Bottom
```

---

## `ICellRenderer` — Custom cell display

A renderer receives one `CellRenderContext` per cell (an `in` readonly struct), so adding data to a cell never
changes the interface.

```csharp
public class StarRatingRenderer : ICellRenderer
{
    public void Render(IDrawingContext ctx, in CellRenderContext cell)
    {
        // cell.Bounds        padded cell area, stay inside it
        // cell.Value         raw value from KumikoSource
        // cell.DisplayText   formatted via KumikoColumn.Format (cached by the source)
        // cell.Column, cell.Style, cell.CellStyle, cell.IsSelected
        // cell.Paints        the frame's PaintCache; cell.TextPaint the resolved text paint
        // cell.ImageResolver turns image cell values into drawable images
        // Use ctx.* only. No SkiaSharp. No allocation.
    }
}
```

Built-in renderers: `TextCellRenderer` (Text, Numeric, Date, Template, ComboBox, Picker), `BooleanCellRenderer`,
`ImageCellRenderer`, `ProgressBarCellRenderer`, `ActionButtonsCellRenderer`. A renderer that animates or reacts to
the pointer implements `IInteractiveCellRenderer`; `KumikoRenderer` listens to its `RedrawRequested`.

Attach to a column:
```csharp
column.CustomCellRenderer = new StarRatingRenderer();   // code
```
```xml
<core:KumikoColumn.CustomCellRenderer>
    <render:StarRatingRenderer />
</core:KumikoColumn.CustomCellRenderer>
```

`KumikoRenderer.SetCellRenderer(columnType, renderer)` replaces the renderer of a whole column type.

---

## `KumikoRenderer` — Render loop structure

Hosts call `KumikoRenderer.RenderFrame(...)` (internal; the public `Render(...)` is the same without popups) once per
paint with a context from `SkiaResourceCache.CreateContext(canvas)`. The passes, in z-order (read `RenderFrame` for
the exact code before changing it):

```
RenderFrame
  ├─ layout           SyncCellTextCulture, GetVisibleRowRangeWithOffset, ComputeColumnWidths, FrozenPanes.Resolve,
  │                   content size, ScrollState.ClampOffset, PaintCache.Update, CaptureFeedback
  ├─ background       FillRect(PaintCache.Background), DrawGroupingPanel (when grouped)
  ├─ pass 1  scrolling columns    DrawPaneRows → DrawHeader → DrawSortIndicators → DrawFilterIcons → DrawFrozenRows
  ├─ pass 2  left-frozen columns  the same calls, clipped to the pane, then DrawFrozenColumnDivider
  ├─ pass 3  right-frozen columns DrawRightFrozenBackground, the same calls, then DrawRightFrozenColumnDivider
  │            (DrawPaneRows = DrawRowBand: DrawRows, DrawCurrentCellBorder, DrawGridLines)
  ├─ DrawFrozenRowDivider
  ├─ DrawTableSummaryRows          top summaries, and bottom ones that scroll with the rows
  ├─ DrawGroupHeaderRow / DrawGroupSummaryRow   full-width rows, outside the column clips
  ├─ sticky group headers          DrawGroupHeaderRow for each PinnedGroupHeaders entry
  ├─ DrawDragHandleColumn
  ├─ DrawTableSummaryRows          the pinned bottom summary (footer), when PinBottomSummaryRows
  ├─ DrawColumnDragOverlay, DrawRowDragOverlay
  └─ EditSession.PlaceEditor + DrawEditor, PopupManager.DrawPopups, KumikoFrameStats overlay
```

Adding a new draw pass: add a `private void Draw{Feature}(IDrawingContext ctx, ...)` method and call it from
`RenderFrame` at the correct z-order. Wrap every `ClipRect` and `Translate` in `Save()`/`Restore()`.
Overlays that must sit above the grid (editor, popups) are drawn after the last `Restore()`, in absolute coordinates.

Hit testing mirrors the drawing: `KumikoHitTester` resolves a point against the same geometry, and what a
frame measured (the grouping chips) is handed over through `RenderedHitRegions`; shared sizes live in
`InteractionGeometry`. A change to where something is drawn changes its hit region too.

---

## `PaintCache`

`KumikoUI.Core.Rendering.PaintCache` holds pre-built `KumikoPaint` instances derived from `KumikoStyle` (for example
`PaintCache.Background`, `HeaderText`, `GridLine`, `SelectionBackground`). `PaintCache.Update(style)` runs every
frame and rebuilds only when the style instance or `KumikoStyle.Version` changed. When adding a style-driven paint,
add it to `PaintCache.Update`. Per-cell overrides use `PaintCache.CellTextPaint(cellStyle, style)`, which reuses one
override paint.

---

## `SkiaDrawingContext` — SkiaSharp internals

- Thin per-frame wrapper over the canvas; paints, fonts and typefaces come from a `SkiaResourceCache`
  (`record struct` keys, bounded LRU) that each host control owns and disposes on teardown (MAUI handler disconnect,
  Uno `Unloaded`).
- A host starts every frame with `cache.CreateContext(canvas)`, which reuses one context object, so a steady-state
  frame allocates nothing. `IDisposable`: disposing at the end of the frame releases what the frame evicted and
  leaves the cached objects alive.
- `SkiaFontRegistrar` changes invalidate every live cache before the registrar disposes a typeface.
- Text rendering uses the `SKFont`-based SkiaSharp API (not the obsolete `SKPaint` text members).
- Text widths and laid-out lines are cached in the `SkiaResourceCache` across frames, keyed by text and font values;
  a steady-state frame measures no text. Ellipsis cuts and wraps use `KumikoTextLayout` (Core).
- Glyphs the requested typeface lacks are drawn run by run with fallback typefaces (`SkiaFontRegistrar.FallbackFamilies`,
  then `SKFontManager.MatchCharacter`); emoji sequences are shaped with HarfBuzz (`GlyphShaper`). Runs are cached with
  the text's measurement, so measure through the cache (never `SKFont.MeasureText` directly) to get widths that match
  what is drawn. See docs/RENDERING.md "Font fallback".
- If a new `IDrawingContext` method is needed: **add to the interface in Core first** (with a default implementation
  when others can implement it), then implement it in `SkiaDrawingContext`, and add it to `NoOpDrawingContext` and
  `RecordingDrawingContext` in the Core tests.

---

## Performance Rules

`RenderAllocationTests` (Core, through a no-op context) and `SkiaAllocationTests` (SkiaSharp, through a real
`SKSurface`) assert 0 managed bytes per steady-state frame; `PerformanceBudgetTests` holds the timing budgets.

1. **No per-frame heap allocations**: no `new` inside `RenderFrame` or the methods it calls, no closures, no
   `params`, no boxing, no string building (the source's `GetCachedCellText` caches display text).
2. Use the `Populate*` variants on `KumikoLayoutEngine` (they write into pre-allocated `List<>`s).
3. No LINQ in any method called from the render loop.
4. Virtual scrolling: only the rows `GetVisibleRowRangeWithOffset` returns and the columns the pane lists hold are drawn.
5. Frozen pane passes must **not** apply the scroll offset (`ScrollState.OffsetX`): `ColumnScreenX` handles each pane.

---

## `KumikoLayoutEngine` — Key methods

```csharp
engine.ComputeColumnWidths(columns, viewportWidth, ctx, style, dataSource, firstRow, lastRow);
engine.GetVisibleRowRangeWithOffset(scroll, style, totalRows, topSummaryHeight, frozenRowCount);
engine.GetContentWidth(columns);
engine.GetContentHeight(rowCount, style);
engine.GetFrozenColumnWidth(columns);
engine.GetRightFrozenColumnWidth(columns);
engine.GetFrozenRowHeight(frozenRowCount, style);
engine.CalculateAutoFitWidth(column, dataSource, ctx, style);
// The render loop uses the Populate* variants (the Get* ones allocate a list):
engine.PopulateVisibleFrozenColumns(columns, list);
engine.PopulateVisibleRightFrozenColumns(columns, viewportWidth, list);
engine.PopulateVisibleScrollableColumns(columns, scroll, frozenWidth, rightFrozenWidth, list);
```

`FrozenPanes.Resolve` decides per frame which frozen columns stay pinned; `KumikoScroller` (scroll-to-row,
column offsets) and `PinnedGroupHeaders` (sticky group headers) live next to the engine in `src/KumikoUI.Core/Layout/`.
