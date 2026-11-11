using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Input;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// A column without an explicit width is at least wide enough for its whole header (#182); an explicit width, a
/// resize drag or a smaller MaxWidth lets the header be truncated.
/// </summary>
/// <remarks>
/// The recording context measures 8 px per character, and headers have 8 px of padding on each side. A sortable,
/// filterable column reserves 12 px for the sort arrow and 14 px for the filter icon, plus a 4 px margin, so its
/// header-fit width is 8 n + 46 for an n-character header.
/// </remarks>
public class HeaderFitWidthTests
{
    private const string LongHeader = "Customer Account Number"; // 23 characters: 184 px of text
    private const float LongHeaderFit = 23 * 8 + 16 + 12 + 14 + 4; // 230

    private static DrawOp HeaderText(RecordingDrawingContext ctx, string header) =>
        Assert.Single(ctx.Ops, o => o.Kind == DrawOpKind.TextInRect && o.Text == header && Math.Abs(o.Rect.Y) < 0.01f);

    private static RenderHarness GridWith(float width, params KumikoColumn[] columns) =>
        RenderHarness.Create(width: width, configure: h => h.Source.SetColumns(columns));

    // ── Fixed columns ──

    [Fact]
    public void WithoutAnExplicitWidth_ALongHeaderIsShownWhole()
    {
        var column = new KumikoColumn { Header = LongHeader, PropertyName = "Name" };
        var h = GridWith(600, column);

        var ctx = h.Render();

        Assert.False(column.HasExplicitWidth);
        Assert.Equal(LongHeaderFit, column.HeaderFitWidth);
        Assert.Equal(LongHeaderFit, column.Width);
        Assert.Equal(LongHeader, RecordingDrawingContext.ShownText(HeaderText(ctx, LongHeader)));
    }

    [Fact]
    public void WithoutAnExplicitWidth_AShortHeaderKeepsTheDefaultWidth()
    {
        var column = new KumikoColumn { Header = "Age", PropertyName = "Age" };
        var h = GridWith(600, column);

        h.Render();

        Assert.Equal(3 * 8 + 46, column.HeaderFitWidth);
        Assert.Equal(KumikoColumn.DefaultWidth, column.Width);
    }

    [Fact]
    public void TheHeaderFitWidth_ReservesOnlyTheGlyphsTheColumnCanShow()
    {
        var plain = new KumikoColumn { Header = LongHeader, PropertyName = "Name", AllowSorting = false, AllowFiltering = false };
        var h = GridWith(600, plain);

        h.Render();

        Assert.Equal(23 * 8 + 16 + 4, plain.Width);
        Assert.Equal(LongHeader, RecordingDrawingContext.ShownText(HeaderText(h.Render(), LongHeader)));
    }

    [Fact]
    public void SortingAColumnLater_DoesNotTruncateItsHeader()
    {
        var column = new KumikoColumn { Header = LongHeader, PropertyName = "Name" };
        var h = GridWith(600, column);
        h.Render();

        h.Source.SetSort(column, KumikoSortDirection.Ascending);
        var ctx = h.Render();

        Assert.Equal(LongHeaderFit, column.Width);
        Assert.Equal(LongHeader, RecordingDrawingContext.ShownText(HeaderText(ctx, LongHeader)));
    }

    [Fact]
    public void WithAnExplicitWidth_TheHeaderIsTruncated()
    {
        var column = new KumikoColumn { Header = LongHeader, PropertyName = "Name", Width = 100 };
        var h = GridWith(600, column);

        var ctx = h.Render();

        Assert.True(column.HasExplicitWidth);
        Assert.Equal(100, column.Width);
        string shown = RecordingDrawingContext.ShownText(HeaderText(ctx, LongHeader));
        Assert.NotEqual(LongHeader, shown);
        Assert.EndsWith("…", shown);
    }

    [Fact]
    public void ASmallerMaxWidth_WinsOverTheHeaderFit_AndTruncates()
    {
        var column = new KumikoColumn { Header = LongHeader, PropertyName = "Name", MaxWidth = 100 };
        var h = GridWith(600, column);

        var ctx = h.Render();

        Assert.False(column.HasExplicitWidth);
        Assert.Equal(100, column.Width);
        Assert.EndsWith("…", RecordingDrawingContext.ShownText(HeaderText(ctx, LongHeader)));
    }

    [Fact]
    public void AMinWidth_AboveTheHeaderFit_IsKept()
    {
        var column = new KumikoColumn { Header = "Age", PropertyName = "Age", MinWidth = 150 };
        var h = GridWith(600, column);

        h.Render();

        Assert.Equal(150, column.Width);
    }

    [Fact]
    public void AutoColumnsWithoutAnExplicitWidth_GetTheHeaderFitToo()
    {
        var column = new KumikoColumn { Header = LongHeader, PropertyName = "Name", SizeMode = ColumnSizeMode.Auto };
        var h = GridWith(600, column);

        h.Render();

        Assert.Equal(LongHeaderFit, column.Width);
    }

    // ── Star columns ──

    [Fact]
    public void StarColumns_NeverShrinkBelowTheHeaderFit_AndTheOthersShareTheRest()
    {
        var a = new KumikoColumn { Header = "A", PropertyName = "Name", SizeMode = ColumnSizeMode.Star };
        var b = new KumikoColumn { Header = "B", PropertyName = "City", SizeMode = ColumnSizeMode.Star };
        var wide = new KumikoColumn { Header = LongHeader, PropertyName = "Age", SizeMode = ColumnSizeMode.Star };
        var h = GridWith(400, a, b, wide);

        var ctx = h.Render();

        // An even split would be 133 each; the long header needs 230, so A and B share the other 170.
        Assert.Equal(LongHeaderFit, wide.Width, 0.01f);
        Assert.Equal(85, a.Width, 0.01f);
        Assert.Equal(85, b.Width, 0.01f);
        Assert.Equal(400, h.Scroll.ContentWidth, 0.01f);
        Assert.Equal(0, h.Scroll.MaxOffsetX, 0.01f);
        Assert.Equal(LongHeader, RecordingDrawingContext.ShownText(HeaderText(ctx, LongHeader)));
    }

    [Fact]
    public void StarColumns_InAViewportTooNarrowForTheirHeaders_KeepTheirMinimums_AndScrollHorizontally()
    {
        var first = new KumikoColumn { Header = LongHeader, PropertyName = "Name", SizeMode = ColumnSizeMode.Star };
        var second = new KumikoColumn { Header = "Monthly Salary (USD)", PropertyName = "Salary", SizeMode = ColumnSizeMode.Star };
        var h = GridWith(300, first, second);

        var ctx = h.Render();

        Assert.Equal(LongHeaderFit, first.Width, 0.01f);
        Assert.Equal(20 * 8 + 46, second.Width, 0.01f);
        Assert.Equal(LongHeaderFit + 206, h.Scroll.ContentWidth, 0.01f);
        Assert.True(h.Scroll.MaxOffsetX > 0, "the content is wider than the viewport, so the grid scrolls");
        Assert.Equal(LongHeader, RecordingDrawingContext.ShownText(HeaderText(ctx, LongHeader)));

        h.Scroll.OffsetX = h.Scroll.MaxOffsetX;
        ctx = h.Render();
        Assert.Equal("Monthly Salary (USD)", RecordingDrawingContext.ShownText(HeaderText(ctx, "Monthly Salary (USD)")));
    }

    [Fact]
    public void AStarColumnWithAnExplicitWidth_IsHeldOnlyToItsMinWidth()
    {
        var column = new KumikoColumn { Header = LongHeader, PropertyName = "Name", SizeMode = ColumnSizeMode.Star, Width = 200 };
        var h = GridWith(100, column);

        h.Render();

        Assert.Equal(100, column.Width, 0.01f);
        Assert.True(column.HasExplicitWidth);
    }

    // ── Resize ──

    [Fact]
    public void AResizeDrag_MakesTheWidthExplicit_AndMayTruncateTheHeader()
    {
        var fitted = new KumikoColumn { Header = "Age", PropertyName = "Age" };
        var g = new InputGrid(configure: x => x.Source.SetColumns(new[] { x.Name, fitted }));
        Assert.Equal(KumikoColumn.DefaultWidth, fitted.Width);
        Assert.False(fitted.HasExplicitWidth);

        // fitted spans 120..240, so its resize grip starts at x 236.
        g.Press(236, InputGrid.HeaderY);
        g.Move(236 - 100, InputGrid.HeaderY);
        g.Release(236 - 100, InputGrid.HeaderY);
        g.Render();

        Assert.True(fitted.HasExplicitWidth);
        Assert.Equal(fitted.MinWidth, fitted.Width); // narrower than the 70 px header fit
    }

    // ── Cache ──

    [Fact]
    public void HeadersAreMeasuredOnce_ThenOnlyWhenTheirTextChanges()
    {
        var column = new KumikoColumn { Header = "Short", PropertyName = "Name" };
        var h = GridWith(600, column);
        var ctx = new CountingContext();

        h.Renderer.Render(ctx, h.Source, h.Scroll, h.Selection, h.Style);
        h.Renderer.Render(ctx, h.Source, h.Scroll, h.Selection, h.Style);
        Assert.Equal(1, ctx.HeaderMeasures("Short"));

        column.Header = LongHeader;
        h.Renderer.Render(ctx, h.Source, h.Scroll, h.Selection, h.Style);
        h.Renderer.Render(ctx, h.Source, h.Scroll, h.Selection, h.Style);

        Assert.Equal(1, ctx.HeaderMeasures(LongHeader));
        Assert.Equal(LongHeader.Length * 10f + 46f, column.Width);
    }

    [Fact]
    public void ChangingTheHeaderFont_MeasuresAgain()
    {
        var column = new KumikoColumn { Header = LongHeader, PropertyName = "Name" };
        var h = GridWith(600, column);
        var ctx = new CountingContext();
        h.Renderer.Render(ctx, h.Source, h.Scroll, h.Selection, h.Style);
        float before = column.Width;

        h.Style.HeaderFont = new KumikoFont("Default", 28, bold: true);
        h.Renderer.Render(ctx, h.Source, h.Scroll, h.Selection, h.Style);

        Assert.Equal(2, ctx.HeaderMeasures(LongHeader));
        Assert.True(column.Width > before);
    }

    [Fact]
    public void ChangingAPropertyOfTheHeaderFontInPlace_MeasuresAgain()
    {
        var column = new KumikoColumn { Header = LongHeader, PropertyName = "Name" };
        var h = GridWith(600, column);
        var ctx = new CountingContext();
        h.Renderer.Render(ctx, h.Source, h.Scroll, h.Selection, h.Style);
        float before = column.Width;

        h.Style.HeaderFont.Size = 28;
        h.Renderer.Render(ctx, h.Source, h.Scroll, h.Selection, h.Style);

        Assert.Equal(2, ctx.HeaderMeasures(LongHeader));
        Assert.True(column.Width > before);
    }

    [Fact]
    public void APerColumnHeaderFont_IsMeasuredInsteadOfTheGridFont()
    {
        var column = new KumikoColumn
        {
            Header = LongHeader,
            PropertyName = "Name",
            HeaderCellStyle = new KumikoCellStyle { Font = new KumikoFont("Default", 28), Padding = 2 },
        };
        var h = GridWith(600, column);

        h.Renderer.Render(new CountingContext(), h.Source, h.Scroll, h.Selection, h.Style);

        // 28 px font: 20 px per character; 2 px padding on each side.
        Assert.Equal(LongHeader.Length * 20f + 4 + 12 + 14 + 4, column.Width);
    }

    /// <summary>Measures text at (font size × 5/7) px per character and counts measurements of each text.</summary>
    private sealed class CountingContext : IDrawingContext
    {
        private readonly Dictionary<string, int> _measures = new();

        public int HeaderMeasures(string text) => _measures.GetValueOrDefault(text);

        public KumikoSize MeasureText(string text, KumikoPaint paint)
        {
            _measures[text] = HeaderMeasures(text) + 1;
            return new KumikoSize(text.Length * (paint.Font?.Size ?? 14f) * 5f / 7f, 14f);
        }

        public void DrawRect(KumikoRect rect, KumikoPaint paint) { }
        public void FillRect(KumikoRect rect, KumikoPaint paint) { }
        public void DrawRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
        public void FillRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint) { }
        public void DrawLine(float x1, float y1, float x2, float y2, KumikoPaint paint) { }
        public void DrawText(string text, float x, float y, KumikoPaint paint) { }
        public void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
            KumikoTextAlignment hAlign = KumikoTextAlignment.Left,
            KumikoVerticalAlignment vAlign = KumikoVerticalAlignment.Center, bool clip = true) { }
        public KumikoFontMetrics GetFontMetrics(KumikoPaint paint) => new(-11, 3, 2);
        public void ClipRect(KumikoRect rect) { }
        public void Save() { }
        public void Restore() { }
        public void Translate(float dx, float dy) { }
        public void DrawImage(object image, KumikoRect destRect) { }
    }
}
