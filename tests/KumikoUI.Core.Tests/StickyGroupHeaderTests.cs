using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.Input;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Sticky group headers (<see cref="KumikoStyle.StickyGroupHeaders"/>, #174): the header of the group the top rows
/// belong to stays pinned at the top of the data area, levels stack, the next header pushes a pinned one up, and
/// hit testing and keyboard ensure-visible respect the pinned band.
/// </summary>
/// <remarks>
/// Render geometry (<see cref="RenderHarness"/>): grouping panel 40 px, header 40 px, rows 36 px, so the scrolling
/// rows start at viewport y 80. Twenty people grouped by City: row 0 "City: Oslo", rows 1–10 Oslo, row 11
/// "City: Rome", rows 12–21 Rome.
/// </remarks>
public class StickyGroupHeaderTests
{
    private const float RowsTop = 80f;   // grouping panel + header
    private const float Row = 36f;
    private const string Oslo = "City: Oslo";
    private const string Rome = "City: Rome";

    private static RenderHarness GroupedByCity(bool sticky = true, Action<RenderHarness>? configure = null) =>
        RenderHarness.Create(rows: 20, configure: h =>
        {
            h.Style.StickyGroupHeaders = sticky;
            h.Source.AddGroupDescription(new GroupDescription("City"));
            configure?.Invoke(h);
        });

    /// <summary>Group header labels drawn with this text, top to bottom.</summary>
    private static List<DrawOp> Labels(RecordingDrawingContext ctx, string text) =>
        ctx.TextsEqual(text).OrderBy(o => o.Rect.Y).ToList();

    private static List<DrawOp> LabelsStartingWith(RecordingDrawingContext ctx, string prefix) =>
        ctx.Texts.Where(o => o.Text != null && o.Text.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(o => o.Rect.Y).ToList();

    // ── Style ──

    [Fact]
    public void StickyGroupHeaders_IsOnByDefault_AndChangingItBumpsTheStyleVersion()
    {
        var style = new KumikoStyle();
        int version = style.Version;

        Assert.True(style.StickyGroupHeaders);
        style.StickyGroupHeaders = false;
        Assert.True(style.Version > version);
    }

    // ── Rendering ──

    [Fact]
    public void MidGroup_TheGroupHeaderIsPinnedAtTheTopOfTheDataArea_OverTheRows()
    {
        var h = GroupedByCity();
        h.Scroll.OffsetY = 3 * Row + 10; // Oslo's own header row is scrolled away

        var ctx = h.Render();

        var label = Assert.Single(Labels(ctx, Oslo));
        Assert.Equal(RowsTop, label.Rect.Y, 2);
        Assert.Equal(RowsTop, label.Clip!.Value.Y, 2); // inside the data area, below the header

        // Drawn after every row it covers (z-order: on top).
        var background = ctx.Fills.Single(o => o.Color == h.Style.GroupHeaderBackgroundColor && RenderHarness.Near(o.Rect.Y, RowsTop));
        Assert.True(ctx.Fills.Where(o => o.Index < background.Index && o.Rect.Y < RowsTop + Row && o.Rect.Bottom > RowsTop)
            .All(o => o.Color != h.Style.GroupHeaderBackgroundColor));
        // Row 3 (P04) is partly under the pinned header, which is drawn after it.
        Assert.True(background.Index > ctx.TextsEqual("P04").Max(o => o.Index), "pinned header drawn over the rows beneath it");
    }

    [Fact]
    public void MidGroup_WithStickyHeadersOff_TheHeaderScrollsAway()
    {
        var h = GroupedByCity(sticky: false);
        h.Scroll.OffsetY = 3 * Row + 10;

        var ctx = h.Render();

        Assert.Empty(Labels(ctx, Oslo));
    }

    [Fact]
    public void AtTheTop_TheHeaderIsDrawnOnceInItsNaturalPlace()
    {
        var h = GroupedByCity();

        var ctx = h.Render();

        var label = Assert.Single(Labels(ctx, Oslo));
        Assert.Equal(RowsTop, label.Rect.Y, 2);
    }

    [Fact]
    public void TheNextHeader_PushesThePinnedHeaderUp()
    {
        var h = GroupedByCity();
        // Rome's header (row 11) 20 px below the data-area top: within one row of the pinned slot.
        h.Scroll.OffsetY = 11 * Row - 20;

        var ctx = h.Render();

        var oslo = Assert.Single(Labels(ctx, Oslo));
        Assert.Equal(RowsTop + 20 - Row, oslo.Rect.Y, 2);   // pushed up, ending where Rome begins
        var rome = Assert.Single(Labels(ctx, Rome));
        Assert.Equal(RowsTop + 20, rome.Rect.Y, 2);          // natural place, not overlapped
    }

    [Fact]
    public void OnceTheNextHeaderReachesTheTop_ItIsPinnedInstead()
    {
        var h = GroupedByCity();
        h.Scroll.OffsetY = 12 * Row + 5;

        var ctx = h.Render();

        Assert.Empty(Labels(ctx, Oslo));
        var rome = Assert.Single(Labels(ctx, Rome));
        Assert.Equal(RowsTop, rome.Rect.Y, 2);
    }

    [Fact]
    public void GroupSummaryRows_BelongToTheGroup_SoItsHeaderStaysPinnedOverThem()
    {
        // Oslo: header row 0, data rows 1–10, summary row 11; Rome's header is row 12.
        var h = GroupedByCity(configure: x => x.Source.AddGroupSummaryRow(new GroupSummaryRow("Sub")
        {
            Columns = { new SummaryColumnDescription { PropertyName = "Salary", SummaryType = SummaryType.Sum } },
        }));
        h.Scroll.OffsetY = 11 * Row + 4; // the summary row is at the top; Rome 32 px below it

        var ctx = h.Render();

        var oslo = Assert.Single(Labels(ctx, Oslo));
        Assert.Equal(RowsTop - 4, oslo.Rect.Y, 2);
    }

    [Fact]
    public void NestedGroups_StackOneLevelPerRow_OuterLevelOnTop()
    {
        // City, then Age: row 0 Oslo, row 1 "Age: 20 (2)", rows 2–3, row 4 "Age: 21 (2)", …
        var h = GroupedByCity(configure: x => x.Source.AddGroupDescription(new GroupDescription("Age")));
        h.Scroll.OffsetY = 2 * Row; // rows 0 and 1 (both headers) scrolled away; row 4 ("Age: 21") just below the band

        var ctx = h.Render();

        var city = Assert.Single(LabelsStartingWith(ctx, "City: Oslo"));
        var age = Assert.Single(LabelsStartingWith(ctx, "Age: 20"));
        Assert.Equal(RowsTop, city.Rect.Y, 2);
        Assert.Equal(RowsTop + Row, age.Rect.Y, 2);
        Assert.True(age.Index < city.Index, "the inner level is drawn first, under the outer one");
    }

    [Fact]
    public void NestedGroups_TheNextInnerHeader_PushesOnlyTheInnerLevel()
    {
        var h = GroupedByCity(configure: x => x.Source.AddGroupDescription(new GroupDescription("Age")));
        h.Scroll.OffsetY = 2 * Row + 10; // "Age: 21" (row 4) is 10 px into the inner slot

        var ctx = h.Render();

        var city = Assert.Single(LabelsStartingWith(ctx, "City: Oslo"));
        Assert.Equal(RowsTop, city.Rect.Y, 2);
        var age20 = Assert.Single(LabelsStartingWith(ctx, "Age: 20"));
        Assert.Equal(RowsTop + 2 * Row - 10 - Row, age20.Rect.Y, 2); // pushed up under the city header
        Assert.Contains(LabelsStartingWith(ctx, "Age: 21"), o => RenderHarness.Near(o.Rect.Y, RowsTop + 2 * Row - 10));
    }

    [Fact]
    public void CollapsedGroup_IsNeverDrawnBelowItsOwnRow_AndTheGroupAfterItPins()
    {
        // Oslo collapsed: row 0 Oslo, row 1 Rome, rows 2–11 Rome's people.
        var h = GroupedByCity();
        h.Source.ToggleGroupExpansion(0);

        h.Scroll.OffsetY = 18; // the collapsed header half scrolled away: it has no rows to stay pinned over
        var ctx = h.Render();
        Assert.All(Labels(ctx, "City: Oslo"), o => Assert.Equal(RowsTop - 18, o.Rect.Y, 2));

        h.Scroll.OffsetY = Row + 18; // Rome's header half scrolled away over its rows
        ctx = h.Render();
        Assert.Empty(Labels(ctx, Oslo));
        Assert.Contains(Labels(ctx, Rome), o => RenderHarness.Near(o.Rect.Y, RowsTop)); // pinned over its own row
    }

    [Fact]
    public void GroupingWithNoRows_PinsNothing()
    {
        var h = RenderHarness.Create(rows: 0, configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));
        h.Scroll.OffsetY = 50;

        var ctx = h.Render();

        Assert.DoesNotContain(ctx.Fills, o => o.Color == h.Style.GroupHeaderBackgroundColor);
    }

    [Fact]
    public void WithFrozenColumns_ThePinnedHeaderSpansTheFullWidth_OverEveryPane()
    {
        var h = GroupedByCity(configure: x =>
        {
            x.Column("Name").IsFrozen = true;
            x.Column("Salary").IsFrozenRight = true;
        });
        h.Scroll.OffsetY = 3 * Row + 10;
        h.Scroll.OffsetX = 30;

        var ctx = h.Render();

        var background = ctx.Fills.Single(o => o.Color == h.Style.GroupHeaderBackgroundColor && RenderHarness.Near(o.Rect.Y, RowsTop));
        Assert.Equal(0, background.Rect.X, 2);
        Assert.Equal(h.Scroll.ViewportWidth, background.Rect.Width, 2);
        Assert.Equal(h.Scroll.ViewportWidth, background.Visible!.Value.Width, 2);
        // After every pane's rows: the frozen panes are drawn after the scrolling one.
        int lastRowOp = ctx.Ops.Where(o => o.Clip is { } c && c.Width < h.Scroll.ViewportWidth && o.Rect.Bottom > RowsTop && o.Rect.Y < RowsTop + Row)
            .Max(o => o.Index);
        Assert.True(background.Index > lastRowOp);
    }

    // ── Hit testing and taps ──

    private static InputGrid InputGroupedByCity(bool sticky = true, bool nested = false) =>
        new(rows: 30, configure: x =>
        {
            x.Style.StickyGroupHeaders = sticky;
            x.Source.AddGroupDescription(new GroupDescription("City"));
            if (nested) x.Source.AddGroupDescription(new GroupDescription("Dept"));
        });

    [Fact]
    public void HitTest_OnThePinnedHeader_HitsThatGroup_NotTheRowBeneath()
    {
        // Oslo: row 0 header, rows 1–15; Rome: row 16 header.
        var g = InputGroupedByCity();
        g.Scroll.OffsetY = 5 * Row + 10;

        var row = g.Hit(200, RowsTop + 18);
        var chevron = g.Hit(12, RowsTop + 18);

        Assert.Equal(HitRegion.GroupHeaderRow, row.Region);
        Assert.Equal(0, row.RowIndex);
        Assert.Equal(HitRegion.GroupChevron, chevron.Region);
        Assert.Equal(0, chevron.RowIndex);
        // Just below the band: the row there.
        var below = g.Hit(200, RowsTop + Row + 2);
        Assert.Equal(HitRegion.Cell, below.Region);
        Assert.Equal(6, below.RowIndex);
    }

    [Fact]
    public void HitTest_WithStickyHeadersOff_HitsTheRowBeneath()
    {
        var g = InputGroupedByCity(sticky: false);
        g.Scroll.OffsetY = 5 * Row + 10;

        var hit = g.Hit(200, RowsTop + 18);

        Assert.Equal(HitRegion.Cell, hit.Region);
        Assert.Equal(5, hit.RowIndex);
    }

    [Fact]
    public void HitTest_NestedPinnedHeaders_EachLevelHitsItsOwnGroup()
    {
        // Oslo (row 0) → Dept Eng (row 1, five rows) → Dept Ops (row 7) …
        var g = InputGroupedByCity(nested: true);
        g.Scroll.OffsetY = 3 * Row;

        var outer = g.Hit(200, RowsTop + 18);
        var inner = g.Hit(200, RowsTop + Row + 18);

        Assert.Equal(0, outer.RowIndex);
        Assert.Equal(HitRegion.GroupHeaderRow, inner.Region);
        Assert.Equal(1, inner.RowIndex);
        Assert.Equal("Eng", g.Source.GetGroupHeaderInfo(inner.RowIndex)!.DisplayText);
    }

    [Fact]
    public void HitTest_APushedUpHeader_IsHitOnlyWhereItIsVisible()
    {
        var g = InputGroupedByCity();
        g.Scroll.OffsetY = 16 * Row - 20; // Rome's header (row 16) 20 px below the top pushes Oslo up

        var oslo = g.Hit(200, RowsTop + 10);
        var rome = g.Hit(200, RowsTop + 25);

        Assert.Equal(0, oslo.RowIndex);
        Assert.Equal(HitRegion.GroupHeaderRow, rome.Region);
        Assert.Equal(16, rome.RowIndex);
    }

    [Fact]
    public void TappingThePinnedHeader_CollapsesThatGroup_AndKeepsItsHeaderAtTheTop()
    {
        var g = InputGroupedByCity();
        g.Scroll.OffsetY = 5 * Row + 10;

        g.Tap(200, RowsTop + 18);

        Assert.False(g.Source.GetGroupHeaderInfo(0)!.IsExpanded);
        Assert.Equal(0, g.Scroll.OffsetY, 2);
        Assert.False(g.Selection.CurrentCell.IsValid); // the row beneath was not selected
        // The collapsed header is now in its natural place at the top, with Rome right after it.
        Assert.Equal(0, g.Hit(200, RowsTop + 18).RowIndex);
        Assert.Equal(1, g.Hit(200, RowsTop + Row + 18).RowIndex);
    }

    [Fact]
    public void TappingANestedPinnedHeader_CollapsesTheInnerGroup_AndKeepsItInItsSlot()
    {
        var g = InputGroupedByCity(nested: true);
        g.Scroll.OffsetY = 3 * Row;

        g.Tap(12, RowsTop + Row + 18); // the inner header's chevron

        Assert.False(g.Source.GetGroupHeaderInfo(1)!.IsExpanded);
        Assert.True(g.Source.GetGroupHeaderInfo(0)!.IsExpanded);
        // Row 1 is now just below the city header's slot: offset (1 - level 1) rows.
        Assert.Equal(0, g.Scroll.OffsetY, 2);
    }

    [Fact]
    public void TappingAPinnedHeader_ToExpandIt_DoesNotScroll()
    {
        // A collapsed group is pinned only in its natural place, so expanding it from there keeps the offset.
        var g = InputGroupedByCity();
        g.Source.ToggleGroupExpansion(0);
        g.Render();
        g.Scroll.OffsetY = 10;

        g.Tap(200, RowsTop + 10);

        Assert.True(g.Source.GetGroupHeaderInfo(0)!.IsExpanded);
        Assert.Equal(10, g.Scroll.OffsetY, 2);
    }

    // ── Keyboard ensure-visible ──

    [Fact]
    public void KeyboardUp_KeepsTheCurrentRowBelowThePinnedHeader()
    {
        var g = InputGroupedByCity();
        g.Select(5, 0);
        g.Scroll.OffsetY = 160; // row 5 is partly under the pinned Oslo header

        g.Key(KumikoKey.Up);

        Assert.Equal(4, g.Selection.CurrentCell.Row);
        Assert.Equal(4 * Row - Row, g.Scroll.OffsetY, 2); // row 4's top at the band's bottom edge
        Assert.Equal(HitRegion.Cell, g.Hit(200, RowsTop + Row + 2).Region);
        Assert.Equal(4, g.Hit(200, RowsTop + Row + 2).RowIndex);
    }

    [Fact]
    public void KeyboardUp_WithStickyHeadersOff_ScrollsTheRowToTheTop()
    {
        var g = InputGroupedByCity(sticky: false);
        g.Select(5, 0);
        g.Scroll.OffsetY = 160;

        g.Key(KumikoKey.Up);

        Assert.Equal(4 * Row, g.Scroll.OffsetY, 2);
    }

    [Fact]
    public void KeyboardUp_NestedGroups_KeepTheRowBelowEveryLevel()
    {
        // Oslo → Eng: rows 2–6 are Eng people
        var g = InputGroupedByCity(nested: true);
        g.Select(5, 0);
        g.Scroll.OffsetY = 5 * Row - Row; // row 5 under the two-level band

        g.Key(KumikoKey.Up);

        Assert.Equal(4, g.Selection.CurrentCell.Row);
        Assert.Equal(4 * Row - 2 * Row, g.Scroll.OffsetY, 2);
    }

    [Fact]
    public void KeyboardUp_OntoAGroupHeader_ScrollsItToTheTopOfItsLevel()
    {
        // From Rome's first person (row 17) up to Rome's header (row 16), a level-0 header with nothing above it.
        var g = InputGroupedByCity();
        g.Select(17, 0);
        g.Scroll.OffsetY = 16 * Row + 24;

        g.Key(KumikoKey.Up);

        Assert.Equal(16, g.Selection.CurrentCell.Row);
        Assert.Equal(16 * Row, g.Scroll.OffsetY, 2);
    }
}
