---
mode: agent
description: Debug, optimize, or extend the KumikoUI rendering pipeline
---

# Rendering Pipeline — Debug and Extend

This prompt helps with understanding, debugging, or extending the KumikoUI canvas
rendering pipeline.

---

## Pipeline overview

```
KumikoDataGrid.OnPaintSurface
  │
  └─ KumikoRenderer.Render(IDrawingContext, KumikoSource, KumikoLayoutEngine, ...)
        │
        ├─ DrawHeaderRow(...)         — column headers, sort indicators, filter icons
        ├─ DrawFrozenRows(...)         — frozen-top data rows
        ├─ DrawScrollableArea(...)     — virtual row loop (only visible rows rendered)
        │     └─ DrawDataRow(row, ...)
        │           └─ DrawCell(cell, ICellRenderer, ...)
        ├─ DrawFrozenColumns(...)      — left and right frozen column overlays
        ├─ DrawGroupHeaders(...)       — expandable group header rows
        ├─ DrawSummaryRows(...)        — table-level summary rows
        ├─ DrawGroupSummaryRows(...)   — per-group summary rows
        ├─ DrawActiveEditor(...)       — overlays the active DrawnComponent editor
        ├─ DrawScrollbars(...)         — canvas-drawn scrollbars
        └─ DrawRowDragOverlay(...)     — drag-reorder ghost row
```

All draw calls go through `IDrawingContext` → `SkiaDrawingContext` → `SKCanvas`.

---

## Performance checklist

### Allocations in the hot path

KumikoUI avoids per-frame heap allocations. When adding render code:

- **Use `for` loops** over `foreach` on `List<>` (avoids enumerator boxing).
- **Use `PopulateVisible*` methods** on `KumikoLayoutEngine` (write into pre-allocated `List<>`).
- **Do NOT use LINQ** (`.Where(...)`, `.Select(...)`) inside `Render(...)` or any method
  it calls.
- **Do NOT `new` SkiaSharp objects** (`SKPaint`, `SKFont`, `SKPath`) inside the render loop.
  `SkiaDrawingContext` caches them keyed by `PaintKey` / `FontKey` structs.

### `PaintCache`

`KumikoUI.Core.Rendering.PaintCache` pre-builds `KumikoPaint` instances from `KumikoStyle`
at theme-change time. Prefer `paintCache.CellPaint(isSelected, style)` over
constructing new `KumikoPaint` objects inline.

---

## Adding a new draw pass

1. Add a private `Draw{FeatureName}(IDrawingContext ctx, ...)` method to `KumikoRenderer`.
2. Call it from `Render(...)` at the correct z-order position (before editor overlay).
3. Use `ctx.Save()` / `ctx.Restore()` around any `ctx.ClipRect()` or `ctx.Translate()`.
4. Use `style.*` for all colors and sizes — never hardcode.

Template:
```csharp
private void Draw{FeatureName}(
    IDrawingContext ctx,
    KumikoSource source,
    KumikoLayoutEngine layout,
    KumikoStyle style,
    ScrollState scroll)
{
    ctx.Save();
    try
    {
        // ... drawing using ctx.Fill/DrawRect, ctx.DrawText, etc.
    }
    finally
    {
        ctx.Restore();
    }
}
```

---

## `SkiaDrawingContext` — when you need native Skia

`SkiaDrawingContext` is in `KumikoUI.SkiaSharp`. It wraps `SKCanvas` and adds:

- **Per-frame paint cache** — `GetOrCreatePaint(PaintKey key)` returns a cached `SKPaint`
- **Per-frame font cache** — `GetOrCreateFont(FontKey key)` returns a cached `SKFont`
- **Frame disposal** — All cached objects are `Dispose()`d when the context is disposed

If you need a SkiaSharp feature not exposed by `IDrawingContext`, **add it to the
`IDrawingContext` interface first** (in `KumikoUI.Core`), then implement it in
`SkiaDrawingContext` (in `KumikoUI.SkiaSharp`).

Never reach directly into `SkiaDrawingContext` from Core renderer code.

---

## Hit testing

`HitTesting` (in `KumikoUI.Core.Input`) converts a pointer position to a semantic
grid location:

```csharp
var hit = HitTesting.HitTest(
    pointerX, pointerY,
    layout, source, style, scroll,
    viewportWidth, viewportHeight);

switch (hit.Region)
{
    case HitRegion.Cell:
        // hit.Row, hit.Column
        break;
    case HitRegion.Header:
        // hit.Column
        break;
    case HitRegion.GroupHeader:
        // hit.FlatRowIndex
        break;
    case HitRegion.ColumnResizeHandle:
        // hit.Column
        break;
}
```

---

## Common rendering bugs

| Symptom | Likely cause | Fix |
|---|---|---|
| Frozen columns show wrong content | Scrollable x-offset applied to frozen draw pass | Ensure frozen pass does NOT offset by `scroll.HorizontalOffset` |
| Clipping bleeds outside cell | Missing `ctx.ClipRect(cellRect)` before text draw | Wrap text draw in `Save`/`ClipRect(cellRect)`/`Restore` |
| Editor flickers or paints behind data | `DrawActiveEditor` called before data rows | Move `DrawActiveEditor` call to after all data/group passes |
| Selection highlight not updating | `InvalidateSurface()` not called after `KumikoSelectionModel` change | Subscribe to `KumikoSelectionModel.Changed` and call `InvalidateSurface()` |
| Font size not scaling with device | DPI scaling not applied | Multiply `KumikoFont.Size` by `SKCanvasView.CanvasSize / DeviceSize` ratio |
