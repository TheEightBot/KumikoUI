using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>The built-in icon set (#119): every icon is drawn, centered in its slot, inside it and scaled with it.</summary>
public class KumikoIconProviderTests
{
    private static readonly KumikoColor Ink = new(12, 34, 56);

    private static RecordingDrawingContext Draw(KumikoIcon icon, KumikoRect bounds, float strokeWidth = 1.5f)
    {
        var ctx = new RecordingDrawingContext();
        KumikoIconProvider.Default.DrawIcon(ctx, icon, bounds,
            new KumikoPaint { Color = Ink, StrokeWidth = strokeWidth, IsAntiAlias = true });
        return ctx;
    }

    public static TheoryData<KumikoIcon> AllIcons()
    {
        var data = new TheoryData<KumikoIcon>();
        foreach (var icon in Enum.GetValues<KumikoIcon>())
            data.Add(icon);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllIcons))]
    public void EveryIcon_DrawsInThePaintsColor_InsideAndCenteredInItsSlot(KumikoIcon icon)
    {
        var bounds = new KumikoRect(100, 50, 24, 24);

        var ctx = Draw(icon, bounds);

        Assert.NotEmpty(ctx.Ops);
        Assert.All(ctx.Ops, o => Assert.Equal(Ink, o.Color));
        float left = ctx.Ops.Min(o => o.Rect.Left), right = ctx.Ops.Max(o => o.Rect.Right);
        float top = ctx.Ops.Min(o => o.Rect.Top), bottom = ctx.Ops.Max(o => o.Rect.Bottom);
        Assert.True(left >= bounds.Left && right <= bounds.Right && top >= bounds.Top && bottom <= bounds.Bottom,
            $"{icon} drawn at ({left}, {top})–({right}, {bottom}), outside its slot");
        Assert.Equal(112, (left + right) / 2, 1f);  // the collapsed group triangle sits 0.6 px right of center
        Assert.Equal(62, (top + bottom) / 2, 3.5f); // the funnel and the tick are not symmetric vertically
    }

    [Theory]
    [MemberData(nameof(AllIcons))]
    public void EveryIcon_ScalesWithItsSlot(KumikoIcon icon)
    {
        static float Width(RecordingDrawingContext c) => c.Ops.Max(o => o.Rect.Right) - c.Ops.Min(o => o.Rect.Left);

        float small = Width(Draw(icon, new KumikoRect(0, 0, 12, 12)));
        float large = Width(Draw(icon, new KumikoRect(0, 0, 24, 24)));

        Assert.Equal(small * 2, large, 0.01f);
    }

    [Fact]
    public void AnOblongSlot_CentersASquareGlyphOnItsShorterSide()
    {
        var ctx = Draw(KumikoIcon.SortAscending, new KumikoRect(0, 0, 40, 12));

        var arrow = Assert.Single(ctx.Polygons);
        Assert.Equal(8, arrow.Rect.Width, 3);   // two thirds of 12
        Assert.Equal(20, arrow.Rect.X + arrow.Rect.Width / 2, 3);
    }

    [Theory]
    [InlineData(KumikoIcon.GroupExpanded)]
    [InlineData(KumikoIcon.GroupCollapsed)]
    [InlineData(KumikoIcon.SortAscending)]
    [InlineData(KumikoIcon.SortDescending)]
    [InlineData(KumikoIcon.FilterActive)]
    [InlineData(KumikoIcon.Indeterminate)]
    [InlineData(KumikoIcon.DropDown)]
    [InlineData(KumikoIcon.DropDownOpen)]
    [InlineData(KumikoIcon.Increment)]
    [InlineData(KumikoIcon.Decrement)]
    public void SolidGlyphs_AreOneFilledPolygon(KumikoIcon icon)
    {
        var ctx = Draw(icon, new KumikoRect(0, 0, 16, 16));

        var only = Assert.Single(ctx.Ops);
        Assert.Equal(DrawOpKind.FillPolygon, only.Kind);
    }

    [Theory]
    [InlineData(KumikoIcon.Filter)]
    [InlineData(KumikoIcon.GroupSeparator)]
    [InlineData(KumikoIcon.Check)]
    [InlineData(KumikoIcon.CalendarPrevious)]
    [InlineData(KumikoIcon.CalendarNext)]
    [InlineData(KumikoIcon.SpinUp)]
    [InlineData(KumikoIcon.SpinDown)]
    public void LineGlyphs_AreOneJoinedPolyline_InThePaintsStrokeWidth(KumikoIcon icon)
    {
        var ctx = Draw(icon, new KumikoRect(0, 0, 16, 16), strokeWidth: 2.5f);

        var only = Assert.Single(ctx.Ops);
        Assert.Equal(DrawOpKind.Polyline, only.Kind);
        Assert.Equal(2.5f, only.StrokeWidth);
    }

    [Theory]
    [InlineData(KumikoIcon.SpinUp, true)]
    [InlineData(KumikoIcon.SpinDown, false)]
    [InlineData(KumikoIcon.SortAscending, true)]
    [InlineData(KumikoIcon.SortDescending, false)]
    [InlineData(KumikoIcon.DropDownOpen, true)]
    [InlineData(KumikoIcon.DropDown, false)]
    public void UpAndDownGlyphs_PointTheRightWay(KumikoIcon icon, bool up)
    {
        var op = Assert.Single(Draw(icon, new KumikoRect(0, 0, 16, 16)).Ops);

        // The lone vertex (the tip) is above the other two for an upward glyph
        var pts = op.Points!;
        var tip = pts.OrderBy(p => p.X).ElementAt(1);
        float others = pts.Where(p => !p.Equals(tip)).Average(p => p.Y);
        Assert.Equal(up, tip.Y < others);
    }

    [Fact]
    public void PlusAndMinus_UseThePaintsStrokeWidthAsTheirThickness()
    {
        var minus = Assert.Single(Draw(KumikoIcon.Decrement, new KumikoRect(0, 0, 20, 20), strokeWidth: 3).Polygons);
        var plus = Assert.Single(Draw(KumikoIcon.Increment, new KumikoRect(0, 0, 20, 20), strokeWidth: 3).Polygons);

        Assert.Equal(3, minus.Rect.Height, 3);
        Assert.Equal(plus.Rect.Width, minus.Rect.Width, 3);
        Assert.Equal(plus.Rect.Width, plus.Rect.Height, 3);
    }

    [Fact]
    public void AnEmptySlot_OrAnUnknownIcon_DrawsNothing()
    {
        Assert.Empty(Draw(KumikoIcon.Check, new KumikoRect(0, 0, 0, 10)).Ops);
        Assert.Empty(Draw((KumikoIcon)999, new KumikoRect(0, 0, 10, 10)).Ops);
    }

    private sealed class StarCheck : KumikoIconProvider
    {
        public override void DrawIcon(IDrawingContext ctx, KumikoIcon icon, KumikoRect bounds, KumikoPaint paint)
        {
            if (icon == KumikoIcon.Check)
                ctx.DrawText("*", bounds.X, bounds.Bottom, paint);
            else
                base.DrawIcon(ctx, icon, bounds, paint);
        }
    }

    [Fact]
    public void ADerivedProvider_ReplacesOneGlyph_AndKeepsTheOthers()
    {
        var provider = new StarCheck();
        var ctx = new RecordingDrawingContext();
        var paint = new KumikoPaint { Color = Ink };

        provider.DrawIcon(ctx, KumikoIcon.Check, new KumikoRect(0, 0, 16, 16), paint);
        provider.DrawIcon(ctx, KumikoIcon.SortAscending, new KumikoRect(0, 0, 16, 16), paint);

        Assert.Equal("*", Assert.Single(ctx.Texts).Text);
        Assert.Single(ctx.Polygons);
    }
}
