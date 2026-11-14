---
mode: agent
description: Debug, optimize, or extend the KumikoUI rendering pipeline
---

# Rendering Pipeline — Debug and Extend

This prompt helps with understanding, debugging, or extending the KumikoUI canvas
rendering pipeline. Read `.github/instructions/rendering.instructions.md` and `docs/RENDERING.md` too; the code in
`src/KumikoUI.Core/KumikoRenderer.cs` (`RenderFrame`) is the truth when they differ.

---

## Pipeline overview

```
host paint (MAUI: KumikoDataGrid.PaintSurface; Uno: the surface's render callback)
  │   ResourceCache.CreateContext(canvas)        — a SkiaDrawingContext for this frame
  │
  └─ KumikoRenderer.RenderFrame(ctx, source, scroll, selection, style, drag state, editSession, popupManager)
        │
        ├─ layout        — GetVisibleRowRangeWithOffset, ComputeColumnWidths, FrozenPanes.Resolve,
        │                  ScrollState.ClampOffset, PaintCache.Update, CaptureFeedback
        ├─ FillRect(background), DrawGroupingPanel
        ├─ pass 1  scrolling columns:   DrawPaneRows → DrawHeader → DrawSortIndicators → DrawFilterIcons → DrawFrozenRows
        │            (DrawPaneRows = DrawRowBand: DrawRows → DrawCurrentCellBorder → DrawGridLines)
        ├─ pass 2  left-frozen columns:  the same, in its clip; DrawFrozenColumnDivider
        ├─ pass 3  right-frozen columns: DrawRightFrozenBackground, the same; DrawRightFrozenColumnDivider
        ├─ DrawFrozenRowDivider
        ├─ DrawTableSummaryRows        — top, and bottom summaries that scroll
        ├─ DrawGroupHeaderRow / DrawGroupSummaryRow — full-width rows
        ├─ sticky group headers        — DrawGroupHeaderRow for each pinned header
        ├─ DrawDragHandleColumn
        ├─ DrawTableSummaryRows        — the pinned footer (PinBottomSummaryRows)
        ├─ DrawColumnDragOverlay, DrawRowDragOverlay
        └─ EditSession.PlaceEditor + DrawEditor, PopupManager.DrawPopups, the frame stats overlay
```

All draw calls go through `IDrawingContext` → `SkiaDrawingContext` → `SKCanvas`. Cells are drawn by an `ICellRenderer` that
receives a `CellRenderContext`.

---

## Performance checklist

### Allocations in the hot path

KumikoUI avoids per-frame heap allocations (`RenderAllocationTests` and `SkiaAllocationTests` assert 0 bytes). When adding
render code:

- **Use `for` loops** over `foreach` on `List<>` (avoids enumerator boxing).
- **Use `PopulateVisible*` methods** on `KumikoLayoutEngine` (write into pre-allocated `List<>`).
- **Do NOT use LINQ** (`.Where(...)`, `.Select(...)`) inside `RenderFrame` or any method it calls.
- **Do NOT `new` SkiaSharp objects** (`SKPaint`, `SKFont`, `SKPath`) in Core, or inside the render loop. `SkiaDrawingContext`
  takes them from the host's `SkiaResourceCache`, keyed by value structs.
- **Do NOT format strings or box values per frame**: cells read `KumikoSource.GetCachedCellText`.
- **Do NOT create a closure or a `new KumikoPaint`** in a draw method; reuse a field or a `PaintCache` paint.

### `PaintCache`

`KumikoUI.Core.Rendering.PaintCache` pre-builds `KumikoPaint` instances from `KumikoStyle` and rebuilds them only when the
style instance or its `KumikoStyle.Version` changes. Prefer `PaintCache.CellText`, `PaintCache.GridLine` and the others, or
`PaintCache.CellTextPaint(cellStyle, style)` for a per-cell override, over constructing paints inline.

---

## Adding a new draw pass

1. Add a private `Draw{FeatureName}(IDrawingContext ctx, ...)` method to `KumikoRenderer`.
2. Call it from `RenderFrame` at the correct z-order position (before the editor overlay and popups).
3. Use `ctx.Save()` / `ctx.Restore()` around any `ctx.ClipRect()` or `ctx.Translate()`.
4. Use `style.*` (through `PaintCache`) for all colors and sizes: never hardcode.
5. If the pass draws something the pointer can hit, give it the same geometry in `KumikoHitTester` (share the numbers through
   `InteractionGeometry`; what the frame measured goes to `RenderedHitRegions`) and add a test with `RenderHarness`.

Template:
```csharp
private void Draw{FeatureName}(
    IDrawingContext ctx,
    KumikoSource source,
    KumikoStyle style,
    ScrollState scroll)
{
    ctx.Save();
    // ... drawing using ctx.FillRect / DrawRect / DrawText, with paints from _paintCache
    ctx.Restore();
}
```

---

## `SkiaDrawingContext` — when you need native Skia

`SkiaDrawingContext` is in `KumikoUI.SkiaSharp`. It wraps `SKCanvas` and adds:

- **Long-lived resource cache**: paints, fonts and typefaces come from a `SkiaResourceCache` the host
  control owns; a host starts every frame with `cache.CreateContext(canvas)` (bounded LRU, allocation-free)
- **Text caches and font fallback**: measurements, laid-out lines and per-run typefaces are cached; emoji sequences are shaped with HarfBuzz
- **Frame end**: disposing the context ends the frame and keeps the cached objects; the host disposes
  the cache on teardown

If you need a SkiaSharp feature not exposed by `IDrawingContext`, **add it to the
`IDrawingContext` interface first** (in `KumikoUI.Core`), then implement it in
`SkiaDrawingContext` (in `KumikoUI.SkiaSharp`), and in the two test doubles (`NoOpDrawingContext`, `RecordingDrawingContext`).

Never reach directly into `SkiaDrawingContext` from Core renderer code.

---

## Hit testing

`KumikoHitTester` (in `KumikoUI.Core.Input`) converts a pointer position to a semantic
grid location, with the same pane and offset logic the renderer uses:

```csharp
var hit = hitTester.HitTest(
    viewportX, viewportY,
    scroll, style, source.DisplayColumns, source.RowCount, source, deviceType);

switch (hit.Region)
{
    case HitRegion.Cell:
        // hit.RowIndex, hit.ColumnIndex, hit.Column
        break;
    case HitRegion.HeaderCell:
        // hit.Column
        break;
    case HitRegion.GroupHeaderRow:
        // hit.RowIndex
        break;
    case HitRegion.HeaderResizeGrip:
        // hit.Column
        break;
}
```

The device type (`KumikoPointerDeviceType`) widens the targets for a finger. Regions: `HitRegion.None`, `Cell`, `HeaderCell`,
`HeaderResizeGrip`, `HeaderFilterIcon`, `RowDragHandle`, `EmptyArea`, `GroupHeaderRow`, `GroupChevron`, `GroupPanelChip`,
`GroupPanelChipRemove`, `GroupSummaryRow`, `TableSummaryRow`.

---

## Common rendering bugs

| Symptom | Likely cause | Fix |
|---|---|---|
| Frozen columns show wrong content | The scrolling x-offset applied to a frozen pane | Frozen passes position columns with `ColumnScreenX` and the pane; do not add `ScrollState.OffsetX` |
| Clipping bleeds outside cell | Missing `ctx.ClipRect(cellRect)` before text draw | Wrap the draw in `Save`/`ClipRect(cellRect)`/`Restore`, or use `DrawTextInRect` with `clip: true` |
| Editor flickers or paints behind data | The editor drawn before the data passes | The editor is drawn by `EditSession.DrawEditor` after the last pass; keep new passes before it |
| A feature draws in the wrong place but clicks hit elsewhere | Draw and hit-test geometry diverged | Share the numbers through `InteractionGeometry`; test with `ExactHitRegionTests` |
| Selection highlight not updating | A redraw was not requested after a `KumikoSelectionModel` change | The host repaints on the model's `SelectionChanged` and `CurrentCellChanged`; a new state needs the same signal (`RequestRedraw` in the host) |
| Frame allocates | A `new`, LINQ, closure, boxing or string in a draw path | Find it with `RenderAllocationTests`; use a field, a `PaintCache` paint or the source's text cache |
| Font size not scaling with device | Scale not applied | The host draws in logical units and scales the canvas by the display scale (`KumikoDataGrid.Rendering.cs`); do not scale in Core |
