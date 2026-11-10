using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// <see cref="PaintCache"/>: the per-frame paints rebuilt from a style (every color distinct, so a
/// paint wired to the wrong style field shows), and the per-cell text paint overrides.
/// </summary>
public class PaintCacheTests
{
    // The harness gives every KumikoColor in its style a unique value
    private static KumikoStyle DistinctStyle() => RenderHarness.Create(1).Style;

    private static PaintCache Built(KumikoStyle style)
    {
        var cache = new PaintCache();
        cache.Update(style);
        return cache;
    }

    [Fact]
    public void Update_TakesEachPaintFromItsStyleField()
    {
        var s = DistinctStyle();

        var c = Built(s);

        Assert.Equal(s.BackgroundColor, c.Background.Color);
        Assert.Equal(s.HeaderBackgroundColor, c.HeaderBackground.Color);
        Assert.Equal(s.FrozenColumnBackgroundColor, c.FrozenColumnBackground.Color);
        Assert.Equal(s.RightFrozenColumnBackgroundColor, c.RightFrozenColumnBackground.Color);
        Assert.Equal(s.SelectionColor, c.SelectionBackground.Color);
        Assert.Equal(s.FocusedRowColor, c.FocusedRowBackground.Color);
        Assert.Equal(s.AlternateRowColor, c.AlternateRowBackground.Color);
        Assert.Equal(s.FrozenRowBackgroundColor, c.FrozenRowBackground.Color);
        Assert.Equal(s.HeaderTextColor, c.HeaderText.Color);
        Assert.Equal(s.CellTextColor, c.CellText.Color);
        Assert.Equal(s.GridLineColor, c.GridLine.Color);
        Assert.Equal(s.HeaderBorderColor, c.HeaderBorder.Color);
        Assert.Equal(s.FrozenColumnDividerColor, c.FrozenColumnDivider.Color);
        Assert.Equal(s.RightFrozenColumnDividerColor, c.RightFrozenColumnDivider.Color);
        Assert.Equal(s.FrozenRowDividerColor, c.FrozenRowDivider.Color);
        Assert.Equal(s.CurrentCellBorderColor, c.CurrentCellBorder.Color);
        Assert.Equal(s.AccentColor, c.AccentStroke.Color);
        Assert.Equal(s.SortIndicatorColor, c.SortIndicator.Color);
        Assert.Equal(s.FilterIconColor, c.FilterIcon.Color);
        Assert.Equal(s.FilterActiveIconColor, c.FilterActiveIcon.Color);
        Assert.Equal(s.RowDragHandleBackgroundColor, c.DragHandleBackground.Color);
        Assert.Equal(s.RowDragHandleColor, c.DragHandleIcon.Color);
        Assert.Equal(s.GroupPanelBackgroundColor, c.GroupPanelBackground.Color);
        Assert.Equal(s.GroupPanelTextColor, c.GroupPanelText.Color);
        Assert.Equal(s.GroupPanelChipBackgroundColor, c.GroupChipBackground.Color);
        Assert.Equal(s.GroupPanelChipBorderColor, c.GroupChipBorder.Color);
        Assert.Equal(s.SummaryRowBackgroundColor, c.SummaryBackground.Color);
        Assert.Equal(s.SummaryRowTextColor, c.SummaryText.Color);
    }

    [Fact]
    public void Update_TakesFontsAndStrokeWidthsFromTheStyle()
    {
        var s = DistinctStyle();
        s.CellFont = new KumikoFont("Cell", 11);
        s.HeaderFont = new KumikoFont("Head", 12, bold: true);
        s.GroupPanelFont = new KumikoFont("Group", 13);
        s.SummaryRowFont = new KumikoFont("Sum", 14);
        s.GridLineWidth = 2.5f;
        s.FrozenColumnDividerWidth = 3.5f;
        s.RightFrozenColumnDividerWidth = 4.5f;
        s.FrozenRowDividerWidth = 5.5f;
        s.CurrentCellBorderWidth = 6.5f;

        var c = Built(s);

        Assert.Same(s.CellFont, c.CellText.Font);
        Assert.Same(s.HeaderFont, c.HeaderText.Font);
        Assert.Same(s.GroupPanelFont, c.GroupPanelText.Font);
        Assert.Same(s.SummaryRowFont, c.SummaryText.Font);
        Assert.Equal(2.5f, c.GridLine.StrokeWidth);
        Assert.Equal(2.5f, c.HeaderBorder.StrokeWidth);
        Assert.Equal(3.5f, c.FrozenColumnDivider.StrokeWidth);
        Assert.Equal(4.5f, c.RightFrozenColumnDivider.StrokeWidth);
        Assert.Equal(5.5f, c.FrozenRowDivider.StrokeWidth);
        Assert.Equal(6.5f, c.CurrentCellBorder.StrokeWidth);
    }

    [Fact]
    public void Update_LinePaintsStrokeAndAreaPaintsFill()
    {
        var c = Built(DistinctStyle());

        Assert.All(new[] { c.GridLine, c.HeaderBorder, c.FrozenColumnDivider, c.RightFrozenColumnDivider,
            c.FrozenRowDivider, c.CurrentCellBorder, c.AccentStroke, c.DragHandleIcon, c.GroupChipBorder },
            p => Assert.Equal(PaintStyle.Stroke, p.Style));
        Assert.All(new[] { c.Background, c.HeaderBackground, c.SelectionBackground, c.CellText, c.SortIndicator,
            c.FilterIcon, c.FilterActiveIcon, c.SummaryBackground },
            p => Assert.Equal(PaintStyle.Fill, p.Style));
        Assert.False(c.GridLine.IsAntiAlias);
        Assert.True(c.CellText.IsAntiAlias);
        Assert.Equal(2f, c.AccentStroke.StrokeWidth);
    }

    [Fact]
    public void Update_Again_RebuildsFromTheChangedStyle_WithoutMutatingEarlierPaints()
    {
        var s = DistinctStyle();
        var cache = Built(s);
        var before = cache.CellText;
        var oldColor = before.Color;

        s.CellTextColor = new KumikoColor(200, 201, 202);
        s.BackgroundColor = new KumikoColor(1, 1, 1);
        cache.Update(s);

        Assert.Equal(new KumikoColor(200, 201, 202), cache.CellText.Color);
        Assert.Equal(new KumikoColor(1, 1, 1), cache.Background.Color);
        Assert.NotSame(before, cache.CellText);
        Assert.Equal(oldColor, before.Color);
    }

    [Fact]
    public void BeforeTheFirstUpdate_PaintsAreUsableDefaults()
    {
        var cache = new PaintCache();

        Assert.NotNull(cache.Background);
        Assert.NotNull(cache.CellText);
        Assert.NotNull(cache.SummaryText);
    }

    // ── CellTextPaint ─────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CellTextPaint_WithoutOverrides_ReturnsTheSharedCellPaint(bool emptyStyle)
    {
        var s = DistinctStyle();
        var cache = Built(s);

        var paint = cache.CellTextPaint(emptyStyle ? new KumikoCellStyle { BackgroundColor = new KumikoColor(5, 5, 5) } : null, s);

        Assert.Same(cache.CellText, paint);
    }

    [Fact]
    public void CellTextPaint_ColorOverride_KeepsTheStyleFont()
    {
        var s = DistinctStyle();
        var cache = Built(s);

        var paint = cache.CellTextPaint(new KumikoCellStyle { TextColor = new KumikoColor(10, 20, 30) }, s);

        Assert.NotSame(cache.CellText, paint);
        Assert.Equal(new KumikoColor(10, 20, 30), paint.Color);
        Assert.Same(s.CellFont, paint.Font);
        Assert.True(paint.IsAntiAlias);
    }

    [Fact]
    public void CellTextPaint_FontOverride_KeepsTheStyleColor()
    {
        var s = DistinctStyle();
        var cache = Built(s);
        var font = new KumikoFont("Fancy", 20, bold: true);

        var paint = cache.CellTextPaint(new KumikoCellStyle { Font = font }, s);

        Assert.Equal(s.CellTextColor, paint.Color);
        Assert.Same(font, paint.Font);
    }

    [Fact]
    public void CellTextPaint_BothOverrides_AreApplied_AndTheSharedPaintIsLeftAlone()
    {
        var s = DistinctStyle();
        var cache = Built(s);
        var font = new KumikoFont("Fancy", 20);

        var paint = cache.CellTextPaint(new KumikoCellStyle { TextColor = new KumikoColor(1, 2, 3), Font = font }, s);

        Assert.Equal(new KumikoColor(1, 2, 3), paint.Color);
        Assert.Same(font, paint.Font);
        Assert.Equal(s.CellTextColor, cache.CellText.Color);
        Assert.Same(s.CellFont, cache.CellText.Font);
    }

    [Fact]
    public void CellTextPaint_ReadsTheStyleItIsGiven_NotTheOneTheCacheWasBuiltFrom()
    {
        var built = DistinctStyle();
        var cache = Built(built);
        var other = new KumikoStyle { CellTextColor = new KumikoColor(77, 77, 77), CellFont = new KumikoFont("Other", 9) };

        var paint = cache.CellTextPaint(new KumikoCellStyle { TextColor = new KumikoColor(1, 1, 1) }, other);

        Assert.Same(other.CellFont, paint.Font);
    }

    [Fact]
    public void BackgroundPaint_IsAFreshFillPaintOfThatColor()
    {
        var a = PaintCache.BackgroundPaint(new KumikoColor(3, 4, 5));
        var b = PaintCache.BackgroundPaint(new KumikoColor(3, 4, 5));

        Assert.Equal(new KumikoColor(3, 4, 5), a.Color);
        Assert.Equal(PaintStyle.Fill, a.Style);
        Assert.NotSame(a, b);
    }

    // ── Rebuilding only on style changes (#68) ──────────────────────

    [Fact]
    public void Update_SameStyleUnchanged_KeepsThePaints()
    {
        var s = DistinctStyle();
        var cache = Built(s);
        var cellText = cache.CellText;
        var gridLine = cache.GridLine;

        cache.Update(s);

        Assert.Same(cellText, cache.CellText);
        Assert.Same(gridLine, cache.GridLine);
    }

    [Fact]
    public void Update_AfterAStyleProperty_Changes_RebuildsThePaints()
    {
        var s = DistinctStyle();
        var cache = Built(s);
        var gridLine = cache.GridLine;

        s.GridLineWidth = 3f;
        cache.Update(s);

        Assert.NotSame(gridLine, cache.GridLine);
        Assert.Equal(3f, cache.GridLine.StrokeWidth);
    }

    [Fact]
    public void Update_WithAnotherStyleInstance_RebuildsThePaints()
    {
        var cache = Built(DistinctStyle());
        var other = new KumikoStyle { CellTextColor = new KumikoColor(9, 8, 7) };

        cache.Update(other);

        Assert.Equal(new KumikoColor(9, 8, 7), cache.CellText.Color);
    }

    [Fact]
    public void StyleVersion_ChangesOnlyWhenAPropertyGetsADifferentValue()
    {
        var s = new KumikoStyle();
        int v0 = s.Version;

        s.RowHeight = s.RowHeight;
        s.CellTextColor = s.CellTextColor;
        Assert.Equal(v0, s.Version);

        s.RowHeight = 50;
        Assert.Equal(v0 + 1, s.Version);
        s.CellFont = new KumikoFont("Other", 10);
        s.CellStyleResolver = (_, _) => null;
        Assert.Equal(v0 + 3, s.Version);
    }

    [Fact]
    public void HeaderFontResizedInPlace_ResizesTheMultiSortBadges()
    {
        var h = RenderHarness.Create();
        h.Source.SetSort(h.Column("City"), KumikoSortDirection.Ascending, 1);
        h.Source.SetSort(h.Column("Age"), KumikoSortDirection.Descending, 2);
        static float BadgeSize(RenderHarness h, RecordingDrawingContext ctx) =>
            ctx.Ops.First(o => o.Kind == DrawOpKind.Text && o.Color == h.Style.SortIndicatorColor).Font!.Size;
        Assert.Equal(h.Style.HeaderFont.Size * 0.65f, BadgeSize(h, h.Render()), 3);

        h.Style.HeaderFont.Size = 20; // a font is mutable, and changing it does not change the style's version

        Assert.Equal(13f, BadgeSize(h, h.Render()), 3);
    }

    [Fact]
    public void Update_TakesTheCellContentPaintsFromTheStyle()
    {
        var s = DistinctStyle();
        var c = Built(s);

        Assert.Equal(s.GridLineColor, c.CheckBoxBorder.Color);
        Assert.Equal(PaintStyle.Stroke, c.CheckBoxBorder.Style);
        Assert.Equal(1.5f, c.CheckBoxBorder.StrokeWidth);
        Assert.Equal(s.AccentColor, c.CheckMark.Color);
        Assert.Equal(2f, c.CheckMark.StrokeWidth);
    }

    [Fact]
    public void CellTextPaint_Overrides_ReuseOnePaint()
    {
        var s = DistinctStyle();
        var cache = Built(s);

        var first = cache.CellTextPaint(new KumikoCellStyle { TextColor = new KumikoColor(1, 1, 1) }, s);
        var second = cache.CellTextPaint(new KumikoCellStyle { TextColor = new KumikoColor(2, 2, 2) }, s);

        Assert.Same(first, second);
        Assert.Equal(new KumikoColor(2, 2, 2), second.Color);
    }
}
