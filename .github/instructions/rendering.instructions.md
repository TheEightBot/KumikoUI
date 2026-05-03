---
applyTo: "src/KumikoUI.Core/Rendering/**,src/KumikoUI.SkiaSharp/**,src/KumikoUI.Core/DataGridRenderer.cs"
---

# KumikoUI — Rendering Pipeline

## `IDrawingContext` — The Only Drawing API

All drawing goes through `IDrawingContext`. **Never call SkiaSharp APIs from Core.**
`SkiaDrawingContext` (in `KumikoUI.SkiaSharp`) maps these to `SKCanvas`.

```csharp
void DrawRect(GridRect rect, GridPaint paint);
void FillRect(GridRect rect, GridPaint paint);
void DrawRoundRect(GridRect rect, float cornerRadius, GridPaint paint);
void FillRoundRect(GridRect rect, float cornerRadius, GridPaint paint);
void DrawLine(float x1, float y1, float x2, float y2, GridPaint paint);
void DrawText(string text, float x, float y, GridPaint paint);
void DrawTextInRect(string text, GridRect rect, GridPaint paint,
    GridTextAlignment hAlign = GridTextAlignment.Left,
    GridVerticalAlignment vAlign = GridVerticalAlignment.Center,
    bool clip = true);
GridSize MeasureText(string text, GridPaint paint);
GridFontMetrics GetFontMetrics(GridPaint paint);
void ClipRect(GridRect rect);
void Save();
void Restore();
void Translate(float dx, float dy);
void DrawImage(object image, GridRect destRect);
```

---

## `GridPaint` — Describe how to draw

```csharp
var paint = new GridPaint
{
    Color       = new GridColor(r, g, b),   // or (r, g, b, a)
    StrokeWidth = 1.5f,
    Style       = PaintStyle.Fill,           // Fill | Stroke | FillAndStroke
    IsAntiAlias = true,
    Font        = new GridFont("Default", 13, bold: false)
};
```

Never construct `SKPaint`, `SKFont`, or `SKColor` in Core. `SkiaDrawingContext` caches
all Skia objects per frame; they are disposed at end-of-frame.

## `GridColor`

```csharp
new GridColor(r, g, b)      // alpha = 255
new GridColor(r, g, b, a)   // explicit alpha
GridColor.White / GridColor.Black
```

## `GridRect`

```csharp
new GridRect(x, y, width, height)
rect.Inflate(dx, dy)    // expanded copy
rect.Offset(dx, dy)     // shifted copy
rect.Contains(px, py)   // hit test
rect.Left / .Top / .Right / .Bottom
```

---

## `ICellRenderer` — Custom cell display

```csharp
public class MyRenderer : ICellRenderer
{
    public void Render(
        IDrawingContext ctx,
        GridRect cellRect,       // padded cell area — stay inside this
        object? value,           // raw value from DataGridSource
        string displayText,      // formatted via DataGridColumn.Format
        DataGridColumn column,
        DataGridStyle style,
        bool isSelected,
        CellStyle? cellStyle = null)
    {
        // Use ctx.* only. No SkiaSharp. No allocation.
    }
}
```

Built-in renderers: `TextCellRenderer`, `BooleanCellRenderer`, `ImageCellRenderer`, `ProgressBarCellRenderer`.

Attach to column:
```csharp
column.CustomCellRenderer = new MyRenderer();   // code
```
```xml
<core:DataGridColumn.CustomCellRenderer>
    <render:MyRenderer />
</core:DataGridColumn.CustomCellRenderer>
```

---

## `DataGridRenderer` — Render loop structure

```
DataGridView.OnPaintSurface
  └─ DataGridRenderer.Render(IDrawingContext, ...)
       ├─ DrawHeaderRow          — headers, sort indicator, filter icon, resize handle
       ├─ DrawFrozenRows         — pinned-top data rows
       ├─ DrawScrollableArea     — virtual row loop (visible rows only)
       │    └─ DrawDataRow → DrawCell → ICellRenderer.Render
       ├─ DrawFrozenColumns      — left/right frozen column overlays
       ├─ DrawGroupHeaders       — expandable group header rows
       ├─ DrawSummaryRows        — table-level summary rows (top/bottom)
       ├─ DrawGroupSummaryRows   — per-group summary rows
       ├─ DrawActiveEditor       — DrawnComponent overlay (always last data layer)
       ├─ DrawScrollbars         — canvas-drawn scrollbars
       └─ DrawRowDragOverlay     — drag-reorder ghost
```

Adding a new draw pass: add a `private void Draw{Feature}(IDrawingContext ctx, ...)` method
and call it from `Render(...)` at the correct z-order. Always wrap clip/translate in `Save()`/`Restore()`.

---

## `PaintCache`

`KumikoUI.Core.Rendering.PaintCache` holds pre-built `GridPaint` instances derived from
`DataGridStyle`. When adding new style-driven paints, add them to `PaintCache` and rebuild
it on theme change.

---

## `SkiaDrawingContext` — SkiaSharp internals

- Per-frame cache: `GetOrCreatePaint(PaintKey)` and `GetOrCreateFont(FontKey)` using `record struct` keys.
- `IDisposable` — MAUI host disposes at end of every `PaintSurface` frame.
- Text rendering uses modern SkiaSharp 3.x `SKFont`-based API (not deprecated `SKPaint.TextSize`).
- Binary-search ellipsis truncation for text overflow.
- If a new `IDrawingContext` method is needed: **add to the interface in Core first**, then implement in `SkiaDrawingContext`.

---

## Performance Rules

1. **No per-frame heap allocations** — no `new` inside `Render(...)` or methods it calls.
2. Use `Populate*` variants on `GridLayoutEngine` (write into pre-allocated `List<>`).
3. No LINQ in any method called from the render loop.
4. Virtual scrolling: only rows in `GetVisibleRowRange()` and columns in `GetVisibleScrollableColumns()` are drawn.
5. Frozen column draw passes must **not** apply `scroll.HorizontalOffset`.

---

## `GridLayoutEngine` — Key methods

```csharp
engine.ComputeColumnWidths(columns, viewportWidth);
engine.GetVisibleRowRange(scroll, style, totalRows);
engine.GetVisibleScrollableColumns(columns, scroll, frozenWidth, rightFrozenWidth);
engine.GetVisibleFrozenColumns(columns);
engine.GetVisibleRightFrozenColumns(columns, viewportWidth);
engine.HitTestColumn(columns, contentX);
engine.HitTestRow(contentY, style, totalRows);
engine.CalculateAutoFitWidth(column, dataSource, ctx, style);
// Prefer Populate* variants in the render loop:
engine.PopulateVisibleScrollableColumns(columns, scroll, frozenWidth, rightFrozenWidth, list);
```
