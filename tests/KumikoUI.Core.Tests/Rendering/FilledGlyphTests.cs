using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.TestHelpers.RenderHarness;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// Expand/collapse chevrons and sort arrows are each one filled polygon, not three unjoined strokes (#118).
/// </summary>
public class FilledGlyphTests
{
    private static RenderHarness Grouped()
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));
        return h;
    }

    [Fact]
    public void ExpandedGroupHeader_ChevronIsOneFilledTriangle_PointingDown()
    {
        var h = Grouped();

        var ctx = h.Render();
        var polygons = ctx.Polygons.Where(o => o.Color == h.Style.GroupChevronColor).ToList();

        // Only the first group's header is on screen (its ten rows fill the viewport): one filled triangle.
        Assert.Single(polygons);
        Assert.DoesNotContain(ctx.Lines, l => l.Color == h.Style.GroupChevronColor);
        foreach (var poly in polygons)
        {
            Assert.Equal(PaintStyle.Fill, poly.Style);
            var pts = poly.Points!;
            Assert.Equal(3, pts.Length);
            // Two vertices share the top edge; the apex is alone below it.
            float topY = pts.Min(p => p.Y);
            Assert.Equal(2, pts.Count(p => Near(p.Y, topY)));
            Assert.Single(pts, p => p.Y > topY + 0.01f);
        }
    }

    [Fact]
    public void CollapsedGroupHeader_ChevronIsOneFilledTriangle_PointingRight()
    {
        var h = Grouped();
        h.Source.ToggleGroupExpansion(0);

        var ctx = h.Render();
        var collapsed = ctx.Polygons
            .Where(o => o.Color == h.Style.GroupChevronColor)
            .Where(o => o.Points!.Count(p => Near(p.X, o.Points!.Min(q => q.X))) == 2)
            .ToList();
        var poly = Assert.Single(collapsed);

        Assert.Equal(PaintStyle.Fill, poly.Style);
        var pts = poly.Points!;
        float maxX = pts.Max(p => p.X);
        Assert.Single(pts, p => Near(p.X, maxX));
        Assert.DoesNotContain(ctx.Lines, l => l.Color == h.Style.GroupChevronColor);
    }

    [Theory]
    [InlineData(KumikoSortDirection.Ascending)]
    [InlineData(KumikoSortDirection.Descending)]
    public void GroupingPanelChip_SortArrowIsOneFilledTriangle(KumikoSortDirection direction)
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City") { GroupSortDirection = direction });

        var ctx = h.Render();
        var arrow = Assert.Single(ctx.Polygons, o => o.Color == h.Style.GroupPanelChipAccentColor);

        Assert.Equal(PaintStyle.Fill, arrow.Style);
        Assert.DoesNotContain(ctx.Lines, l => l.Color == h.Style.GroupPanelChipAccentColor);
        var pts = arrow.Points!;
        Assert.Equal(3, pts.Length);
        bool up = direction == KumikoSortDirection.Ascending;
        float baseY = up ? pts.Max(p => p.Y) : pts.Min(p => p.Y);
        Assert.Equal(2, pts.Count(p => Near(p.Y, baseY)));
        var apex = Assert.Single(pts, p => !Near(p.Y, baseY));
        Assert.Equal(up, apex.Y < baseY);
    }

    [Fact]
    public void GroupingPanelSeparator_IsOneJoinedPolyline_NotLooseLines()
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));
        h.Source.AddGroupDescription(new GroupDescription("Name"));

        var ctx = h.Render();
        var chevron = Assert.Single(ctx.Polylines, o => o.Color == h.Style.GroupPanelLabelColor);

        Assert.Equal(3, chevron.Points!.Length);
        Assert.DoesNotContain(ctx.Lines, l => l.Color == h.Style.GroupPanelLabelColor);
    }

    [Fact]
    public void DefaultInterfaceMembers_FallBackToLines_ForImplementersWithoutPolygonSupport()
    {
        var lines = new LineOnlyContext();
        IDrawingContext ctx = lines;
        var pts = new[] { new KumikoPoint(0, 0), new KumikoPoint(4, 0), new KumikoPoint(2, 3) };

        ctx.FillPolygon(pts, new KumikoPaint());
        Assert.Equal(3, lines.LineCount);

        ctx.DrawPolyline(pts, new KumikoPaint());
        Assert.Equal(5, lines.LineCount);
    }

    private sealed class LineOnlyContext : IDrawingContext
    {
        public int LineCount;
        public void DrawLine(float x1, float y1, float x2, float y2, KumikoPaint paint) => LineCount++;
        public void DrawRect(KumikoRect rect, KumikoPaint paint) { }
        public void FillRect(KumikoRect rect, KumikoPaint paint) { }
        public void DrawRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
        public void FillRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
        public void DrawText(string text, float x, float y, KumikoPaint paint) { }
        public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
            KumikoTextAlignment hAlign = KumikoTextAlignment.Left,
            KumikoVerticalAlignment vAlign = KumikoVerticalAlignment.Center, bool clip = true) { }
        public KumikoSize MeasureText(string text, KumikoPaint paint) => new(0, 0);
        public KumikoFontMetrics GetFontMetrics(KumikoPaint paint) => new(0, 0, 0);
        public void ClipRect(KumikoRect rect) { }
        public void Save() { }
        public void Restore() { }
        public void Translate(float dx, float dy) { }
        public void DrawImage(object image, KumikoRect destRect) { }
    }
}
