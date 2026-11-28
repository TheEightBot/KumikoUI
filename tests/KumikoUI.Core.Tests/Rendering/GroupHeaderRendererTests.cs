using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// The group header renderer hook (#332): resolution order, the default renderer drawing the same runs as before, and
/// a custom renderer's context (bounds, level, expanded state, caption) for normal and sticky headers.
/// </summary>
public class GroupHeaderRendererTests
{
    private const float Row = 36f;

    private sealed class Seen
    {
        public KumikoRect Bounds, RowBounds;
        public int Row, Level;
        public bool IsPinned, IsExpanded;
        public string Title = "", CountText = "";
        public string? CaptionText;
        public GroupDescription? Description;
        public object? CaptionSum;
    }

    private sealed class SpyRenderer : IGroupHeaderRenderer
    {
        public List<Seen> Calls { get; } = new();
        public bool Overdraw { get; set; }

        public void Render(IDrawingContext ctx, in GroupHeaderRenderContext context)
        {
            Calls.Add(new Seen
            {
                Bounds = context.Bounds, RowBounds = context.RowBounds, Row = context.Row,
                Level = context.Group.Level, IsPinned = context.IsPinned, IsExpanded = context.Group.IsExpanded,
                Title = context.Title, CountText = context.CountText, CaptionText = context.CaptionText,
                Description = context.Description, CaptionSum = context.Group.GetCaptionValue("Salary", SummaryType.Sum),
            });
            if (Overdraw)
                ctx.FillRect(context.RowBounds, new KumikoPaint { Color = new KumikoColor(1, 2, 3) });
        }
    }

    private static RenderHarness GroupedByCity(Action<RenderHarness>? configure = null) =>
        RenderHarness.Create(rows: 20, height: 700, configure: h =>
        {
            h.Source.AddGroupDescription(new GroupDescription("City"));
            configure?.Invoke(h);
        });

    // ── Resolution order ──

    [Fact]
    public void NoRenderer_DrawsTheTextRuns()
    {
        var h = GroupedByCity();
        var ctx = h.Render();

        Assert.Single(ctx.TextsEqual("City: Oslo"));
        Assert.Single(ctx.TextsEqual(" (10)").Where(o => o.Color == h.Style.GroupHeaderCountColor).Take(1));
    }

    [Fact]
    public void GridRenderer_ReplacesTheDefault()
    {
        var spy = new SpyRenderer();
        var h = GroupedByCity(x => x.Renderer.GroupHeaderRenderer = spy);

        var ctx = h.Render();

        Assert.Equal(2, spy.Calls.Count); // Oslo and Rome
        Assert.Empty(ctx.TextsEqual("City: Oslo"));
    }

    [Fact]
    public void DescriptionRenderer_WinsOverTheGridRenderer()
    {
        var grid = new SpyRenderer();
        var level = new SpyRenderer();
        var h = GroupedByCity(x =>
        {
            x.Renderer.GroupHeaderRenderer = grid;
            x.Source.GroupDescriptions[0].CustomHeaderRenderer = level;
        });

        h.Render();

        Assert.Empty(grid.Calls);
        Assert.Equal(2, level.Calls.Count);
    }

    [Fact]
    public void EachLevel_UsesItsOwnRenderer_AndOthersFallBackToTheGridRenderer()
    {
        var grid = new SpyRenderer();
        var outer = new SpyRenderer();
        var h = RenderHarness.Create(rows: 20, configure: x =>
        {
            x.Source.AddGroupDescription(new GroupDescription("City") { CustomHeaderRenderer = outer });
            x.Source.AddGroupDescription(new GroupDescription("Age"));
            x.Renderer.GroupHeaderRenderer = grid;
        });

        h.Render();

        Assert.NotEmpty(outer.Calls);
        Assert.All(outer.Calls, c => Assert.Equal(0, c.Level));
        Assert.NotEmpty(grid.Calls);
        Assert.All(grid.Calls, c => Assert.Equal(1, c.Level));
    }

    [Fact]
    public void ClearingTheGridRenderer_RestoresTheDefault()
    {
        var spy = new SpyRenderer();
        var h = GroupedByCity(x => x.Renderer.GroupHeaderRenderer = spy);
        h.Renderer.GroupHeaderRenderer = null;

        Assert.NotEmpty(h.Render().TextsEqual("City: Oslo"));
    }

    // ── The default renderer ──

    [Fact]
    public void TextRenderer_DrawsTheSameRunsAsTheDefault()
    {
        var plain = GroupedByCity(h => h.Source.CaptionSummary =
            new CaptionSummaryDescription { Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } });
        var explicitText = GroupedByCity(h =>
        {
            h.Source.CaptionSummary =
                new CaptionSummaryDescription { Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } };
            h.Renderer.GroupHeaderRenderer = new TextGroupHeaderRenderer();
        });

        var a = plain.Render().Texts.ToList();
        var b = explicitText.Render().Texts.ToList();

        Assert.Equal(a.Select(o => (o.Text, o.Rect, o.Color)), b.Select(o => (o.Text, o.Rect, o.Color)));
        Assert.Contains(a, o => o.Color == plain.Style.CaptionSummaryTextColor);
    }

    [Fact]
    public void TextRenderer_RunsStartRightOfTheChevron_AndFollowEachOther()
    {
        var h = GroupedByCity();
        var texts = h.Render().Texts;
        var title = texts.First(t => t.Text == "City: Oslo");
        var count = texts.First(t => t.Text == " (10)" && t.Rect.Y.Equals(title.Rect.Y));

        float chevronRight = InteractionGeometry.GroupChevronX(0, h.Style, 0) + h.Style.GroupChevronSize;
        Assert.Equal(chevronRight + h.Style.CellPadding, title.Rect.X, 0.01f);
        Assert.Equal(title.Rect.X + title.Text!.Length * RecordingDrawingContext.CharWidth, count.Rect.X, 0.01f);
    }

    [Fact]
    public void TextRenderer_AccessibleText_IsTheTitleAndTheCount()
    {
        var h = GroupedByCity();
        var info = h.Source.GetGroupHeaderInfo(0)!;
        var context = new GroupHeaderRenderContext(default, default, 0, false, info, h.Source.GroupDescriptions[0],
            info.GetTitle(), info.GetCountText(), null, h.Style, new PaintCache());

        Assert.Equal("City: Oslo (10)", new TextGroupHeaderRenderer().GetAccessibleText(in context));
        Assert.Null(((IGroupHeaderRenderer)new SpyRenderer()).GetAccessibleText(in context));
    }

    // ── The context ──

    [Fact]
    public void Context_CarriesBoundsLevelStateAndCaption()
    {
        var spy = new SpyRenderer();
        var h = GroupedByCity(x =>
        {
            x.Source.CaptionSummary =
                new CaptionSummaryDescription { Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } };
            x.Renderer.GroupHeaderRenderer = spy;
        });
        float rowsTop = h.Style.HeaderHeight; // the grouping panel is above the translated data area

        h.Render();

        var oslo = spy.Calls[0];
        Assert.Equal(0, oslo.Row);
        Assert.Equal(0, oslo.Level);
        Assert.True(oslo.IsExpanded);
        Assert.False(oslo.IsPinned);
        Assert.Equal("City: Oslo", oslo.Title);
        Assert.Equal(" (10)", oslo.CountText);
        Assert.Equal(h.Source.GetCaptionSummaryText(h.Source.GetGroupHeaderInfo(0)!.GroupPath), oslo.CaptionText);
        Assert.Equal("City", oslo.Description!.PropertyName);
        Assert.Equal(rowsTop, oslo.RowBounds.Y, 0.01f);
        Assert.Equal(Row, oslo.RowBounds.Height, 0.01f);
        Assert.Equal(h.Scroll.ViewportWidth, oslo.RowBounds.Width, 0.01f);

        // The content starts right of the chevron and ends at the right padding
        float chevronRight = InteractionGeometry.GroupChevronX(0, h.Style, 0) + h.Style.GroupChevronSize;
        Assert.Equal(chevronRight + h.Style.CellPadding, oslo.Bounds.X, 0.01f);
        Assert.Equal(h.Scroll.ViewportWidth - h.Style.CellPadding, oslo.Bounds.Right, 0.01f);
        Assert.Equal(rowsTop, oslo.Bounds.Y, 0.01f);
        Assert.Equal(Row, oslo.Bounds.Height, 0.01f);

        // The raw aggregate: the Oslo salaries 1000, 1020, ... 1180
        Assert.Equal(10_900m, Convert.ToDecimal(oslo.CaptionSum));
    }

    [Fact]
    public void Context_CaptionText_IsNullWithoutACaptionSummary_AndTheCaptionValueToo()
    {
        var spy = new SpyRenderer();
        var h = GroupedByCity(x => x.Renderer.GroupHeaderRenderer = spy);

        h.Render();

        Assert.All(spy.Calls, c =>
        {
            Assert.Null(c.CaptionText);
            Assert.Null(c.CaptionSum);
        });
    }

    [Fact]
    public void GetCaptionValue_IsNullForAColumnOrTypeTheCaptionDoesNotHave()
    {
        var h = GroupedByCity(x => x.Source.CaptionSummary =
            new CaptionSummaryDescription { Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } });
        var info = h.Source.GetGroupHeaderInfo(0)!;

        Assert.NotNull(info.GetCaptionValue("salary", SummaryType.Sum)); // ignores case
        Assert.Null(info.GetCaptionValue("Salary", SummaryType.Max));
        Assert.Null(info.GetCaptionValue("Age", SummaryType.Sum));
    }

    [Fact]
    public void Context_ReportsACollapsedGroup()
    {
        var spy = new SpyRenderer();
        var h = GroupedByCity(x => x.Renderer.GroupHeaderRenderer = spy);
        h.Source.ToggleGroupExpansion(0);

        h.Render();

        Assert.False(spy.Calls[0].IsExpanded);
        Assert.True(spy.Calls[1].Row < 5); // Rome follows Oslo's header directly
    }

    [Fact]
    public void NestedLevels_GetTheirOwnDescription()
    {
        var spy = new SpyRenderer();
        var h = RenderHarness.Create(rows: 20, configure: x =>
        {
            x.Source.AddGroupDescription(new GroupDescription("City"));
            x.Source.AddGroupDescription(new GroupDescription("Age"));
            x.Renderer.GroupHeaderRenderer = spy;
        });

        h.Render();

        Assert.Contains(spy.Calls, c => c.Level == 1 && c.Description!.PropertyName == "Age");
        var inner = spy.Calls.First(c => c.Level == 1);
        var outer = spy.Calls.First(c => c.Level == 0);
        Assert.Equal(outer.Bounds.X + h.Style.GroupIndentWidth, inner.Bounds.X, 0.01f);
    }

    [Fact]
    public void StickyHeader_IsDrawnThroughTheRenderer_AsPinned()
    {
        var spy = new SpyRenderer();
        var h = GroupedByCity(x =>
        {
            x.Style.StickyGroupHeaders = true;
            x.Renderer.GroupHeaderRenderer = spy;
            x.Scroll.OffsetY = 3 * Row + 10; // Oslo's own header is scrolled away
        });

        h.Render();

        var pinned = spy.Calls.Single(c => c.IsPinned);
        Assert.Equal("City: Oslo", pinned.Title);
        Assert.Equal(h.Style.HeaderHeight, pinned.Bounds.Y, 0.01f);
        Assert.Equal(0, pinned.Level);
    }

    // ── Clipping ──

    [Fact]
    public void Renderer_IsClippedToItsBounds_SoItCannotDrawOverTheChevron()
    {
        var spy = new SpyRenderer { Overdraw = true };
        var h = GroupedByCity(x => x.Renderer.GroupHeaderRenderer = spy);

        var ctx = h.Render();

        var fill = ctx.Fills.First(o => o.Color == new KumikoColor(1, 2, 3));
        Assert.NotNull(fill.Clip);
        var bounds = spy.Calls[0].Bounds;
        Assert.Equal(bounds.X, fill.Clip!.Value.X, 0.01f);
        Assert.Equal(bounds.Y + h.Style.GroupPanelHeight, fill.Clip.Value.Y, 0.01f);
        Assert.Equal(bounds.Width, fill.Clip.Value.Width, 0.01f);
        Assert.Equal(bounds.Height, fill.Clip.Value.Height, 0.01f);
        Assert.Equal(0, ctx.UnbalancedRestores);
        Assert.Equal(0, ctx.SaveDepth);
    }
}
