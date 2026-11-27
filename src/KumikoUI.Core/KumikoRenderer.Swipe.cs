using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

/// <summary>
/// Draws the row a finger swipes sideways (#329, <see cref="KumikoInputController.AllowSwiping"/>): after the three
/// panes drew their rows, the swiped row is drawn again over itself, moved by its offset as one piece (each pane's cells
/// keep their pane, and the pane moves with the row, so the frozen columns slide too), and the items it uncovers are
/// drawn in the space it left. All of it is clipped to the row's band, before the full-width rows and the sticky group
/// headers. Allocation-free.
/// </summary>
public sealed partial class KumikoRenderer
{
    // Reused for every swipe item (only the colors vary)
    private readonly KumikoPaint _swipeItemPaint = new() { IsAntiAlias = true };
    private readonly KumikoPaint _swipeLabelPaint = new() { Font = new KumikoFont("Default", 13, bold: true), IsAntiAlias = true };

    /// <summary>The swiped row and its revealed items, when a connected controller has one on screen.</summary>
    private void DrawSwipedRow(IDrawingContext ctx, KumikoSource dataSource, ScrollState scroll,
        KumikoSelectionModel selection, KumikoStyle style, EditSession? editSession, float topSummaryHeight,
        int frozenRowCount, float frozenWidth)
    {
        if (_inputController is not { } input || !input.TryGetSwipeFrame(dataSource, out int row, out float offset,
                out var swipe))
            return;
        if (!RowSwipe.TryGetBand(row, scroll, style, dataSource, out var rowRect, out var visible)) return;

        ctx.Save();
        ctx.ClipRect(visible);

        // The row's cells, each pane moved as a whole
        DrawSwipedPane(ctx, dataSource, _scrollableCols, _scrollPane, row, offset, scroll, selection, style,
            ColumnFreezeMode.None, frozenWidth, editSession, topSummaryHeight, frozenRowCount);
        if (_leftPane.Width > 0)
            DrawSwipedPane(ctx, dataSource, _frozenCols, _leftPane, row, offset, scroll, selection, style,
                ColumnFreezeMode.Left, frozenWidth, editSession, topSummaryHeight, frozenRowCount);
        if (_rightPane.Width > 0)
            DrawSwipedPane(ctx, dataSource, _rightFrozenCols, _rightPane, row, offset, scroll, selection, style,
                ColumnFreezeMode.Right, frozenWidth, editSession, topSummaryHeight, frozenRowCount);

        DrawSwipeItems(ctx, swipe, rowRect, offset, style);
        ctx.Restore();
    }

    /// <summary>One pane's part of the swiped row, moved by <paramref name="offset"/> with the pane's clip.</summary>
    private void DrawSwipedPane(IDrawingContext ctx, KumikoSource dataSource,
        List<(KumikoColumn Column, float X, int OriginalIndex)> cols, (float Left, float Width) pane, int row, float offset,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, ColumnFreezeMode mode, float frozenWidth,
        EditSession? editSession, float topSummaryHeight, int frozenRowCount)
    {
        if (pane.Width <= 0) return;
        ctx.Save();
        ctx.Translate(offset, 0);
        ctx.ClipRect(new KumikoRect(pane.Left, 0, pane.Width, scroll.ViewportHeight));
        DrawRows(ctx, dataSource, cols, row, row, scroll, selection, style, mode, frozenWidth, editSession,
            topSummaryHeight, frozenRowCount);
        if (selection.CurrentCell.IsValid && selection.CurrentCell.Row == row)
            DrawCurrentCellBorder(ctx, cols, scroll, selection, style, mode, frozenWidth, dataSource, topSummaryHeight,
                frozenRowCount);
        bool right = mode == ColumnFreezeMode.Right;
        DrawGridLines(ctx, cols, row, row, scroll, style, dataSource.RowCount, mode, frozenWidth, dataSource,
            topSummaryHeight, frozenRowCount, right ? pane.Left : 0, right ? pane.Width : 0);
        ctx.Restore();
    }

    /// <summary>
    /// The items the row uncovers: flat buttons sharing the space, the outermost against the edge (all of it past the
    /// full-swipe point), each in its colors (the disabled colors when its command cannot run), pressed while held.
    /// </summary>
    private void DrawSwipeItems(IDrawingContext ctx, RowSwipe swipe, KumikoRect rowRect, float offset, KumikoStyle style)
    {
        var items = swipe.Items;
        int count = items.Count;
        for (int i = 0; i < count; i++)
        {
            var rect = swipe.ItemRect(i, count, rowRect, offset);
            if (rect.Width <= 0) continue;
            var item = items[i];
            bool enabled = ActionButtonPainter.IsEnabled(item, swipe.Item);
            _swipeItemPaint.Color = enabled ? item.BackgroundColor : style.ActionButtonDisabledBackgroundColor;
            ctx.FillRect(rect, _swipeItemPaint);
            if (enabled && i == swipe.PressedItem) ctx.FillRect(rect, _paintCache.PressedOverlay);

            _swipeLabelPaint.Color = enabled ? item.TextColor : style.ActionButtonDisabledTextColor;
            ctx.DrawTextInRect(item.Label, rect, _swipeLabelPaint, KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);
        }
    }
}
