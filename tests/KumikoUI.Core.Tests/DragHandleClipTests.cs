using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// The row drag-handle glyph of a row that scrolls under a frozen row is clipped to the scrolling rows, like the
/// cells, so it never paints over the frozen row's own handle.
/// </summary>
public class DragHandleClipTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(10f)]
    [InlineData(25.5f)]
    public void ScrollingGrip_DoesNotReachTheFrozenRows(float offsetY)
    {
        var h = RenderHarness.Create(rows: 60, width: 600, height: 400, configure: x =>
        {
            x.Style.ShowRowDragHandle = true;
            x.Source.FrozenRowCount = 2;
        });
        h.Render();
        h.Scroll.OffsetY = offsetY;
        var ctx = h.Render();

        float handleWidth = h.Style.RowDragHandleWidth;
        float scrollingTop = h.Style.HeaderHeight + 2 * h.Style.RowHeight;
        var grips = ctx.Ops
            .Where(o => o.Kind == DrawOpKind.FillRoundRect && o.Rect.Right <= handleWidth + 0.01f)
            .ToList();
        Assert.NotEmpty(grips);

        // Each frozen row draws one grip of six dots (3 x 2) inside its own band. A scrolled row's dot that
        // reaches into the frozen rows' band is clipped away, so the band shows exactly the frozen rows' dots.
        int dotsInFrozenBand = grips.Count(o => o.Visible is { } v && v.Top < scrollingTop - 0.01f);
        Assert.Equal(2 * 6, dotsInFrozenBand);
    }
}
