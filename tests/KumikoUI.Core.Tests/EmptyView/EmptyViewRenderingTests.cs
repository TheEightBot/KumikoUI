using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.EmptyView;

/// <summary>The empty view (#326): when it is drawn, what it says, where it goes and how an app replaces it.</summary>
public class EmptyViewRenderingTests
{
    private const string NoItems = "No items to show";
    private const string NoMatches = "No rows match the current filters";

    private static RenderHarness Empty(Action<RenderHarness>? configure = null) =>
        RenderHarness.Create(rows: 0, configure: configure);

    private static List<DrawOp> EmptyTexts(RecordingDrawingContext ctx, KumikoStyle style) =>
        ctx.Ops.Where(o => o.Kind == DrawOpKind.TextInRect && o.Color == style.EmptyViewTextColor).ToList();

    private static void AssertRect(KumikoRect expected, KumikoRect actual)
    {
        Assert.Equal(expected.X, actual.X, 2);
        Assert.Equal(expected.Y, actual.Y, 2);
        Assert.Equal(expected.Width, actual.Width, 2);
        Assert.Equal(expected.Height, actual.Height, 2);
    }

    /// <summary>The text is laid out in the padded data area: the same columns, and its line centered vertically.</summary>
    private static void AssertCentered(KumikoRect area, DrawOp op)
    {
        Assert.Equal(area.X, op.Rect.X, 2);
        Assert.Equal(area.Width, op.Rect.Width, 2);
        Assert.Equal(area.Y + area.Height / 2, op.Rect.Y + op.Rect.Height / 2, 1);
    }

    private sealed class RecordingEmptyViewRenderer : IKumikoEmptyViewRenderer
    {
        public List<(KumikoRect Bounds, KumikoEmptyViewKind Kind, string Text)> Calls { get; } = new();

        public void Render(IDrawingContext ctx, KumikoRect bounds, KumikoEmptyViewKind kind, string text, KumikoStyle style) =>
            Calls.Add((bounds, kind, text));
    }

    [Fact]
    public void NoItems_DrawsTheNoItemsText()
    {
        var h = Empty();

        var op = Assert.Single(EmptyTexts(h.Render(), h.Style));

        Assert.Equal(NoItems, op.Text);
        Assert.Equal(KumikoTextAlignment.Center, op.HAlign);
    }

    [Fact]
    public void EveryRowFilteredOut_DrawsTheNoMatchesText()
    {
        var h = RenderHarness.Create(rows: 5, configure: x => x.Source.SetFilter(_ => false));

        var op = Assert.Single(EmptyTexts(h.Render(), h.Style));

        Assert.Equal(NoMatches, op.Text);
    }

    [Fact]
    public void ColumnFilterThatHidesEverything_DrawsTheNoMatchesText()
    {
        var h = RenderHarness.Create(rows: 5);
        var city = h.Column("City");
        h.Source.SetColumnFilter(city, new FilterDescription(city) { SelectedValues = new HashSet<string> { "Nowhere" } });

        Assert.Equal(NoMatches, Assert.Single(EmptyTexts(h.Render(), h.Style)).Text);
    }

    [Fact]
    public void SearchThatFiltersEveryRow_DrawsTheNoMatchesText()
    {
        var h = RenderHarness.Create(rows: 5, configure: x =>
        {
            x.Source.SearchFiltersRows = true;
            x.Source.SearchText = "zzz";
        });

        Assert.Equal(NoMatches, Assert.Single(EmptyTexts(h.Render(), h.Style)).Text);
    }

    [Fact]
    public void FilterOnAnEmptyCollection_StaysNoItems()
    {
        var h = Empty(x => x.Source.SetFilter(_ => true));

        Assert.Equal(NoItems, Assert.Single(EmptyTexts(h.Render(), h.Style)).Text);
    }

    [Fact]
    public void Strings_AreLocalizable()
    {
        var h = Empty(x => x.Source.Strings = x.Source.Strings with { EmptyViewNoItems = "Rien" });

        Assert.Equal("Rien", Assert.Single(EmptyTexts(h.Render(), h.Style)).Text);
    }

    [Fact]
    public void EmptyViewText_ReplacesBothTexts()
    {
        var h = Empty(x => x.Renderer.EmptyViewText = "Nothing here");
        Assert.Equal("Nothing here", Assert.Single(EmptyTexts(h.Render(), h.Style)).Text);

        h.Source.SetItems(new List<RenderHarness.Person> { new() });
        h.Source.SetFilter(_ => false);
        Assert.Equal("Nothing here", Assert.Single(EmptyTexts(h.Render(), h.Style)).Text);
    }

    [Fact]
    public void RowsPresent_DrawsNothing()
    {
        var h = RenderHarness.Create(rows: 3);

        Assert.Empty(EmptyTexts(h.Render(), h.Style));
    }

    [Fact]
    public void ShowEmptyViewFalse_DrawsNothing()
    {
        var h = Empty(x => x.Style.ShowEmptyView = false);

        Assert.Empty(EmptyTexts(h.Render(), h.Style));
    }

    [Fact]
    public void Text_IsCenteredInTheDataArea_BelowTheHeader()
    {
        var h = Empty();

        var op = Assert.Single(EmptyTexts(h.Render(), h.Style));

        // 600 x 400 viewport, 40 px header, 8 px padding twice on each side
        AssertCentered(new KumikoRect(16, 40, 568, 360), op);
    }

    [Fact]
    public void Text_UsesTheStyleFont_OrTheCellFont()
    {
        var h = Empty();
        Assert.Equal(h.Style.CellFont, Assert.Single(EmptyTexts(h.Render(), h.Style)).Font);

        var big = new KumikoFont("Default", 22, bold: true);
        h.Style.EmptyViewFont = big;
        Assert.Equal(big, Assert.Single(EmptyTexts(h.Render(), h.Style)).Font);
    }

    [Fact]
    public void Text_IsNotScrolledWithOffsetX()
    {
        var h = Empty();
        h.Scroll.OffsetX = 50;

        AssertCentered(new KumikoRect(16, 40, 568, 360), Assert.Single(EmptyTexts(h.Render(), h.Style)));
    }

    [Fact]
    public void Placement_StartsBelowTheGroupingPanel()
    {
        var h = Empty(x => x.Source.AddGroupDescription(new GroupDescription("City")));

        var op = Assert.Single(EmptyTexts(h.Render(), h.Style));

        // 40 px grouping panel + 40 px header, 320 px left
        AssertCentered(new KumikoRect(16, 80, 568, 320), op);
    }

    [Fact]
    public void Placement_StartsBelowTheTopSummaries()
    {
        var h = Empty(x => x.Source.AddTableSummaryRow(Summary(SummaryPosition.Top)));

        var op = Assert.Single(EmptyTexts(h.Render(), h.Style));

        AssertCentered(new KumikoRect(16, 76, 568, 324), op); // 40 header + 36 summary
    }

    [Fact]
    public void Placement_EndsAboveAPinnedFooter()
    {
        var h = Empty(x =>
        {
            x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom));
            x.Style.PinBottomSummaryRows = true;
        });

        var op = Assert.Single(EmptyTexts(h.Render(), h.Style));

        AssertCentered(new KumikoRect(16, 40, 568, 324), op); // 400 - 36 footer - 40 header
    }

    [Fact]
    public void Placement_CombinesGroupingPanelTopSummaryAndPinnedFooter()
    {
        var h = Empty(x =>
        {
            x.Source.AddGroupDescription(new GroupDescription("City"));
            x.Source.AddTableSummaryRow(Summary(SummaryPosition.Top));
            x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom));
            x.Style.PinBottomSummaryRows = true;
        });

        var op = Assert.Single(EmptyTexts(h.Render(), h.Style));

        AssertCentered(new KumikoRect(16, 40 + 40 + 36, 568, 400 - 40 - 40 - 36 - 36), op);
    }

    [Fact]
    public void Text_IsDrawnAfterThePanes_AndBeforeThePopups()
    {
        var h = Empty();
        var popups = new KumikoUI.Core.Components.PopupManager();
        popups.Show(new ProbeComponent { Bounds = new KumikoRect(0, 0, 50, 50) }, new KumikoRect(0, 0, 50, 50));

        var ctx = h.Render(popupManager: popups);

        var text = Assert.Single(EmptyTexts(ctx, h.Style));
        var probe = Assert.Single(ctx.Ops, o => o.Kind == DrawOpKind.FillRect && o.Color == ProbeComponent.ProbeColor);
        Assert.True(probe.Index > text.Index);
        Assert.Contains(ctx.Ops, o => o.Kind == DrawOpKind.TextInRect && o.Text == "Name" && o.Index < text.Index); // the header is drawn too
    }

    [Fact]
    public void CustomRenderer_GetsTheDataAreaKindAndText()
    {
        var custom = new RecordingEmptyViewRenderer();
        var h = Empty(x => x.Renderer.EmptyViewRenderer = custom);

        var ctx = h.Render();

        var call = Assert.Single(custom.Calls);
        AssertRect(new KumikoRect(0, 40, 600, 360), call.Bounds);
        Assert.Equal(KumikoEmptyViewKind.NoItems, call.Kind);
        Assert.Equal(NoItems, call.Text);
        Assert.Empty(EmptyTexts(ctx, h.Style)); // the built-in text is replaced
    }

    [Fact]
    public void CustomRenderer_ReceivesNoMatchesAndTheTextOverride()
    {
        var custom = new RecordingEmptyViewRenderer();
        var h = RenderHarness.Create(rows: 4, configure: x =>
        {
            x.Renderer.EmptyViewRenderer = custom;
            x.Renderer.EmptyViewText = "Custom";
            x.Source.SetFilter(_ => false);
        });

        h.Render();

        var call = Assert.Single(custom.Calls);
        Assert.Equal(KumikoEmptyViewKind.NoMatches, call.Kind);
        Assert.Equal("Custom", call.Text);
    }

    [Fact]
    public void CustomRenderer_IsNotCalledWithRows_OrWhenDisabled()
    {
        var custom = new RecordingEmptyViewRenderer();
        var withRows = RenderHarness.Create(rows: 2, configure: x => x.Renderer.EmptyViewRenderer = custom);
        withRows.Render();
        var disabled = Empty(x =>
        {
            x.Renderer.EmptyViewRenderer = custom;
            x.Style.ShowEmptyView = false;
        });
        disabled.Render();

        Assert.Empty(custom.Calls);
    }

    [Fact]
    public void Text_ComesBack_WhenRowsAreAddedThenRemoved()
    {
        var h = Empty();
        Assert.Single(EmptyTexts(h.Render(), h.Style));

        h.Source.SetItems(new List<RenderHarness.Person> { new() { Name = "A" } });
        Assert.Empty(EmptyTexts(h.Render(), h.Style));

        h.Source.SetItems(new List<RenderHarness.Person>());
        Assert.Single(EmptyTexts(h.Render(), h.Style));
    }

    [Fact]
    public void Themes_GiveTheEmptyViewTextAReadableColor()
    {
        foreach (var mode in new[] { KumikoThemeMode.Light, KumikoThemeMode.Dark, KumikoThemeMode.HighContrast })
        {
            var style = KumikoTheme.Create(mode);
            Assert.True(style.ShowEmptyView);
            Assert.NotEqual(style.BackgroundColor, style.EmptyViewTextColor);
        }
    }

    [Fact]
    public void StyleOptions_ShowEmptyView_OverridesTheStyle()
    {
        var style = new KumikoStyle();
        var options = new KumikoStyleOptions { ShowEmptyView = false };
        Assert.True(options.HasOverrides);

        options.Apply(style);

        Assert.False(style.ShowEmptyView);
    }

    private static TableSummaryRow Summary(SummaryPosition position) => new("Totals", position)
    {
        Columns = { new SummaryColumnDescription { PropertyName = "Salary", SummaryType = SummaryType.Sum } },
    };
}
