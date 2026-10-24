using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.TestHelpers.RenderHarness;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// <see cref="DataGridRenderer"/>'s full-width layers: the grouping panel, group header and group
/// summary rows, table summary rows, column and row drag overlays, the editor and popups — plus
/// known defects pinned as skipped tests until their fixes land.
/// </summary>
public class DataGridRendererOverlayTests
{
    // ── Grouping panel ────────────────────────────────────────────

    [Fact]
    public void GroupingPanel_IsDrawnAboveTheHeader_AndShiftsTheGridDown()
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render();

        Assert.Contains(ctx.Fills, f => f.Color == h.Style.GroupPanelBackgroundColor && f.Rect.Equals(new GridRect(0, 0, 600, 40)));
        Assert.Contains(ctx.Lines, l => l.Color == h.Style.GridLineColor && Near(l.Y1, 40) && Near(l.Y2, 40) && Near(l.X2, 600));
        // Everything below the panel is translated by its height.
        Assert.Contains(ctx.Fills, f => f.Color == h.Style.HeaderBackgroundColor && f.Rect.Equals(new GridRect(0, 40, 600, 40)));
        Assert.Equal(40, ctx.SingleText("Name").Rect.Y);
        Assert.Equal(40 + 40 + 36, ctx.SingleText("P00").Rect.Y);
    }

    [Fact]
    public void GroupingPanel_DrawsTheLabelThenAChipPerGroup()
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));
        h.Source.AddGroupDescription(new GroupDescription("Age", "Years"));

        var ctx = h.Render();

        var label = ctx.SingleText("Grouped by:");
        Assert.Equal(new GridRect(10, 0, 11 * RecordingDrawingContext.CharWidth, 40), label.Rect);
        Assert.Equal(h.Style.GroupPanelLabelColor, label.Color);

        // Chip text uses the column header unless the group names its own.
        var city = ctx.SingleText("City", h.Style.GroupPanelChipTextColor);
        var years = ctx.SingleText("Years", h.Style.GroupPanelChipTextColor);
        Assert.Equal(10 + 88 + 10 + 4 + 10, city.Rect.X);
        Assert.True(years.Rect.X > city.Rect.Right);
        Assert.Equal(2, ctx.Fills.Count(f => f.Kind == DrawOpKind.FillRoundRect && f.Color == h.Style.GroupPanelChipBackgroundColor));
        // One hierarchy chevron (two strokes) between the chips.
        Assert.Equal(2, ctx.Lines.Count(l => l.Color == h.Style.GroupPanelLabelColor));
    }

    [Theory]
    [InlineData(SortDirection.Ascending, 16f)]  // arrow head at the top of the 26 px chip (centre 20)
    [InlineData(SortDirection.Descending, 24f)] // arrow head at the bottom
    public void GroupingPanel_ChipArrow_ShowsTheGroupSortDirection(SortDirection direction, float headY)
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City") { GroupSortDirection = direction });

        var arrow = h.Render().Lines.Where(l => l.Color == h.Style.GroupPanelChipAccentColor).ToList();

        Assert.Equal(3, arrow.Count);
        var shaft = Assert.Single(arrow, l => Near(l.X1, l.X2));
        Assert.Equal(8, shaft.Rect.Height);
        Assert.All(arrow.Where(l => l != shaft), l => Assert.Equal(headY, l.Y2));
    }

    // ── Group header rows ─────────────────────────────────────────

    [Fact]
    public void GroupHeader_SpansTheViewport_WithTitleCountAndChevron()
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render();

        // Row 0 is the Oslo header: below the 40 px panel and the 40 px header.
        Assert.Contains(ctx.Fills, f => f.Color == h.Style.GroupHeaderBackgroundColor && f.Rect.Equals(new GridRect(0, 80, 600, 36)));
        var title = Assert.Single(ctx.Texts, t => t.Color == h.Style.GroupHeaderTextColor && Near(t.Rect.Y, 80));
        Assert.Equal("City: Oslo (10)", title.Text);
        Assert.Equal(8 + 14 + 8, title.Rect.X);
        // Full-width rows are clipped to the data area, below the header.
        Assert.Equal(new GridRect(0, 80, 600, 320), title.Clip);
        Assert.Contains(ctx.Fills, f => f.Kind == DrawOpKind.FillRoundRect && f.Color == h.Style.GroupChevronBackgroundColor && f.Rect.Top > 80 && f.Rect.Bottom < 116);
    }

    [Theory]
    [InlineData(false, true)]  // expanded ▼: has a horizontal top edge
    [InlineData(true, false)]  // collapsed ▶: has a vertical left edge
    public void GroupHeader_ChevronShape_FollowsExpansion(bool collapse, bool expectHorizontalEdge)
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));
        if (collapse) h.Source.ToggleGroupExpansion(0);

        var chevron = h.Render().Lines.Where(l => l.Color == h.Style.GroupChevronColor && l.Rect.Top >= 80 && l.Rect.Bottom <= 116).ToList();

        Assert.Equal(3, chevron.Count);
        Assert.Equal(expectHorizontalEdge, chevron.Any(l => Near(l.Y1, l.Y2)));
        Assert.Equal(!expectHorizontalEdge, chevron.Any(l => Near(l.X1, l.X2)));
    }

    [Fact]
    public void GroupHeader_NestedLevel_IsIndented()
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));
        h.Source.AddGroupDescription(new GroupDescription("Age"));

        var titles = h.Render().Texts.Where(t => t.Color == h.Style.GroupHeaderTextColor).OrderBy(t => t.Rect.Y).ToList();

        Assert.StartsWith("City: Oslo", titles[0].Text);
        Assert.StartsWith("Age: ", titles[1].Text);
        Assert.Equal(titles[0].Rect.X + h.Style.GroupIndentWidth, titles[1].Rect.X);
    }

    [Fact]
    public void GroupHeader_AppendsTheCaptionSummary()
    {
        var h = Create();
        h.Source.CaptionSummary = new CaptionSummaryDescription { Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } };
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var title = h.Render().Texts.First(t => t.Color == h.Style.GroupHeaderTextColor);

        Assert.StartsWith("City: Oslo (10)  —  ", title.Text);
        Assert.EndsWith(h.Source.GetCaptionSummaryText(h.Source.GetGroupHeaderInfo(0)!.GroupPath)!, title.Text);
    }

    [Fact]
    public void GroupRows_AreSkippedByTheColumnPasses()
    {
        var h = Create(rows: 4);
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render();

        // The Oslo header row (y 80) gets only the full-width group background, no data-row background.
        var atHeader = RowBackgrounds(ctx, 80, 600).ToList();
        Assert.Equal(new[] { h.Style.GroupHeaderBackgroundColor }, atHeader.Select(f => f.Color));
    }

    // ── Group summary rows ────────────────────────────────────────

    [Fact]
    public void GroupSummary_FollowsTheGroupsRows_WithTitleAndValueInItsColumn()
    {
        var h = Create(rows: 4);
        h.Source.AddGroupSummaryRow(new GroupSummaryRow("Sub") { Title = "Subtotal", Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) } });
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render();

        // Oslo header, P00, P02, then the summary at row 3: 80 + 3 × 36.
        const float y = 188;
        Assert.True(h.Source.IsGroupSummaryRow(3));
        Assert.Contains(ctx.Fills, f => f.Color == h.Style.GroupSummaryRowBackgroundColor && f.Rect.Equals(new GridRect(0, y, 600, 36)));
        // Each group gets its own summary: Oslo's at row 3, Rome's at row 7.
        var titles = ctx.TextsEqual("Subtotal").OrderBy(t => t.Rect.Y).ToList();
        Assert.Equal(2, titles.Count);
        Assert.Equal(new GridRect(16, y, 200, 36), titles[0].Rect);
        Assert.Equal(y + 4 * 36, titles[1].Rect.Y);
        var value = Assert.Single(ctx.Texts, t => t.Color == h.Style.GroupSummaryRowTextColor && Near(t.Rect.Y, y) && t.Text != "Subtotal");
        Assert.Equal(new GridRect(158, y, 84, 36), value.Rect);
    }

    [Fact]
    public void GroupSummary_ValueInANarrowColumn_OverflowsAfterTheTitle()
    {
        var h = Create(rows: 4);
        h.Column("Age").Width = 80;
        h.Source.AddGroupSummaryRow(new GroupSummaryRow("Sub") { Title = "Subtotal", Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) } });
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render();

        var overflow = ctx.Texts.Where(t => t.Text!.StartsWith("  |  ", StringComparison.Ordinal)).OrderBy(t => t.Rect.Y).ToList();
        Assert.Equal(2, overflow.Count);
        Assert.Equal(new GridRect(16 + 8 * RecordingDrawingContext.CharWidth, 188, 600 / 3f - 80 - 8, 36), overflow[0].Rect);
        Assert.Equal(h.Style.GroupSummaryRowTextColor, overflow[0].Color);
    }

    [Fact]
    public void GroupSummary_WithoutATitle_DrawsOnlyTheValues()
    {
        var h = Create(rows: 4);
        h.Column("Age").Width = 80;
        h.Source.AddGroupSummaryRow(new GroupSummaryRow("Sub") { Columns = { new SummaryColumnDescription("Age", SummaryType.Sum), new SummaryColumnDescription("Salary", SummaryType.Sum) } });
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render();

        // Age is too narrow and there is no title to flow after, so only Salary's value is drawn.
        var values = ctx.Texts.Where(t => t.Color == h.Style.GroupSummaryRowTextColor).ToList();
        Assert.Equal(2, values.Count);
        Assert.All(values, v => Assert.Equal(388, v.Rect.X));
    }

    // ── Table summary rows ────────────────────────────────────────

    [Fact]
    public void TopSummary_SitsBelowTheHeader_AndPushesRowsDown()
    {
        var h = Create();
        h.Source.AddTableSummaryRow(new TableSummaryRow("Totals", SummaryPosition.Top)
        {
            Title = "Totals",
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) },
        });

        var ctx = h.Render();

        var background = Assert.Single(ctx.Fills, f => f.Color == h.Style.SummaryRowBackgroundColor);
        Assert.Equal(new GridRect(0, 40, 600, 36), background.Rect);
        Assert.Equal(new GridRect(16, 40, 200, 36), ctx.SingleText("Totals").Rect);
        var value = ctx.SingleText(h.Source.ComputedTopSummaries[0].Values["Age"]);
        Assert.Equal(new GridRect(158, 40, 84, 36), value.Rect);
        Assert.Equal(h.Style.SummaryRowTextColor, value.Color);
        Assert.Equal(76, ctx.SingleText("P00").Rect.Y);
        Assert.True(background.Index > ctx.SingleText("P00").Index, "rows are painted over the top summary");
    }

    [Fact]
    public void Summary_ValueInANarrowColumn_OverflowsAfterTheTitle()
    {
        var h = Create();
        h.Column("Age").Width = 80; // 64 px usable: below the 70 px threshold
        h.Source.AddTableSummaryRow(new TableSummaryRow("Totals", SummaryPosition.Top)
        {
            Title = "Totals",
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) },
        });

        var ctx = h.Render();

        string value = h.Source.ComputedTopSummaries[0].Values["Age"];
        Assert.Empty(ctx.TextsEqual(value));
        var overflow = ctx.SingleText("  |  " + value);
        Assert.Equal(16 + 6 * RecordingDrawingContext.CharWidth, overflow.Rect.X);
        Assert.Equal(40, overflow.Rect.Y);
    }

    // ── Column drag overlay ───────────────────────────────────────

    [Fact]
    public void ColumnDrag_DrawsAGhostHeaderAtThePointer_OverTheHeader()
    {
        var h = Create();

        var ctx = h.Render(dragColumnIndex: 1, dragColumnScreenX: 220);

        var ghost = Assert.Single(ctx.Fills, f => f.Color == h.Style.HeaderBackgroundColor.WithAlpha(180));
        Assert.Equal(new GridRect(220, 0, 100, 40), ghost.Rect);
        Assert.Contains(ctx.Ops, o => o.Kind == DrawOpKind.DrawRect && o.Color == h.Style.CurrentCellBorderColor && o.Rect.Equals(ghost.Rect) && Near(o.StrokeWidth, 2));
        var titles = ctx.TextsEqual("Age").OrderBy(t => t.Index).ToList();
        Assert.Equal(2, titles.Count);
        Assert.Equal(228, titles[1].Rect.X);
        Assert.True(ghost.Index > titles[0].Index);
    }

    [Fact]
    public void ColumnDrag_OutOfRangeIndex_DrawsNothing()
    {
        var h = Create();

        var ctx = h.Render(dragColumnIndex: 9, dragColumnScreenX: 220);

        Assert.DoesNotContain(ctx.Fills, f => f.Color == h.Style.HeaderBackgroundColor.WithAlpha(180));
    }

    // ── Row drag overlay ──────────────────────────────────────────

    [Fact]
    public void RowDrag_DrawsAGhostRowAndADropIndicatorAtTheInsertionPoint()
    {
        var h = Create();

        var ctx = h.Render(dragRowIndex: 2, dragRowScreenY: 150);

        var ghost = Assert.Single(ctx.Fills, f => f.Color == h.Style.RowDragOverlayColor);
        Assert.Equal(new GridRect(0, 150, 600, 36), ghost.Rect);
        Assert.Contains(ctx.Ops, o => o.Kind == DrawOpKind.DrawRect && o.Color == h.Style.RowDragBorderColor && o.Rect.Equals(ghost.Rect));
        var ghostText = Assert.Single(ctx.TextsEqual("P02"), t => Near(t.Rect.Y, 150));
        Assert.Equal(h.Style.CellTextColor.WithAlpha(200), ghostText.Color);

        // Ghost centre 168 → drop before row 3, whose top edge is 40 + 3 × 36 = 148.
        var indicator = ctx.Lines.Where(l => l.Color == h.Style.RowDropIndicatorColor).ToList();
        Assert.Contains(indicator, l => Near(l.Y1, 148) && Near(l.Y2, 148) && Near(l.X1, 0) && Near(l.X2, 600));
        Assert.Equal(2, indicator.Count(l => Near(l.X1, l.X2))); // end caps
    }

    [Fact]
    public void RowDrag_OverTheFrozenBand_DropsBetweenFrozenRows()
    {
        var h = Create();
        h.Source.FrozenRowCount = 3;
        h.Scroll.OffsetY = 100;

        var ctx = h.Render(dragRowIndex: 5, dragRowScreenY: 60);

        // Ghost centre 78 lies in frozen row 1 (76–112): the indicator marks its top edge, ignoring the scroll.
        Assert.Contains(ctx.Lines, l => l.Color == h.Style.RowDropIndicatorColor && Near(l.Y1, 76) && Near(l.Y2, 76) && Near(l.X2, 600));
        Assert.Single(ctx.TextsEqual("P05"), t => Near(t.Rect.Y, 60));
    }

    [Fact]
    public void RowDrag_GroupRow_DrawsAnEmptyGhost()
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render(dragRowIndex: 0, dragRowScreenY: 150);

        Assert.Single(ctx.Fills, f => f.Color == h.Style.RowDragOverlayColor);
        Assert.DoesNotContain(ctx.Texts, t => t.Color == h.Style.CellTextColor.WithAlpha(200));
    }

    [Fact]
    public void RowDrag_WithLeftDragHandle_StartsTheGhostAfterTheHandle()
    {
        var h = Create();
        h.Style.ShowRowDragHandle = true;

        var ctx = h.Render(dragRowIndex: 2, dragRowScreenY: 150);

        var ghost = Assert.Single(ctx.Fills, f => f.Color == h.Style.RowDragOverlayColor);
        Assert.Equal(new GridRect(32, 150, 568, 36), ghost.Rect);
        Assert.Contains(ctx.Lines, l => l.Color == h.Style.RowDropIndicatorColor && Near(l.Y1, l.Y2) && Near(l.X1, 32) && Near(l.X2, 600));
    }

    // ── Editor and popups ─────────────────────────────────────────

    [Fact]
    public void PopupsAreDrawnLast_AboveTheEditor_Unclipped()
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));
        var edit = new EditSession { Style = h.Style };
        Assert.True(edit.BeginEdit(1, 0, h.Column("Name"), h.Source, new GridRect(0, 116, 150, 36)));
        var popups = new PopupManager();
        var probe = new ProbeComponent { Bounds = new GridRect(0, 0, 80, 60) };
        popups.Show(probe, new GridRect(100, 100, 50, 20));

        var ctx = h.Render(editSession: edit, popupManager: popups);

        var popup = ctx.Ops[^1];
        Assert.Equal(ProbeComponent.ProbeColor, popup.Color);
        Assert.Equal(new GridRect(100, 120, 80, 60), popup.Rect);
        Assert.Null(popup.Clip);
        int editorBackground = ctx.Ops.FindLastIndex(o => o.Kind == DrawOpKind.FillRect && o.Rect.Equals(edit.ActiveEditor!.Bounds));
        Assert.True(editorBackground > 0 && editorBackground < popup.Index);
        // The editor is drawn in viewport coordinates, outside the grouping panel's translation.
        Assert.Equal(116, ctx.Ops[editorBackground].Rect.Y);
    }

    // ── Known defects (un-skip when the fix lands) ────────────────

    [Fact(Skip = "CORE-03, fixed by #22: the row-style text override is written into the column's shared CellStyle.")]
    public void RowStyleTextOverride_DoesNotLeakIntoTheColumnStyleOrOtherRows()
    {
        var h = Create();
        var name = h.Column("Name");
        name.CellStyle = new CellStyle { Padding = 4 };
        var red = new GridColor(255, 0, 0);
        h.Style.RowStyleResolver = item => ((RenderHarness.Person)item!).Name == "P00" ? new RowStyle { TextColor = red } : null;

        var ctx = h.Render();

        Assert.Equal(red, ctx.SingleText("P00").Color);
        Assert.Equal(h.Style.CellTextColor, ctx.SingleText("P01").Color);
        Assert.Null(name.CellStyle.TextColor);
    }

    [Fact(Skip = "CORE-25, fixed by #27: summary values of scrollable columns are drawn outside the scroll clip.")]
    public void TableSummary_ScrollableColumnValue_NeverOverlapsTheFrozenPane()
    {
        // Name frozen (0–150); scrolled 50 px, Age spans 100–200 and slides under the frozen pane.
        var h = Create(width: 300);
        h.Column("Name").FreezeMode = ColumnFreezeMode.Left;
        h.Source.AddTableSummaryRow(new TableSummaryRow("Totals") { Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) } });
        h.Scroll.OffsetX = 50;

        var ctx = h.Render();

        var value = ctx.SingleText(h.Source.ComputedBottomSummaries[0].Values["Age"]);
        Assert.Equal(0, RectMath.HorizontalOverlap(value.Visible ?? GridRect.Empty, 0, 150));
    }

    [Fact(Skip = "CORE-25, fixed by #27: group summary values of scrollable columns are drawn outside the scroll clip.")]
    public void GroupSummary_ScrollableColumnValue_NeverOverlapsTheFrozenPane()
    {
        var h = Create(rows: 4, width: 300);
        h.Column("Name").FreezeMode = ColumnFreezeMode.Left;
        h.Source.AddGroupSummaryRow(new GroupSummaryRow("Sub") { Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) } });
        h.Source.AddGroupDescription(new GroupDescription("City"));
        h.Scroll.OffsetX = 50;

        var ctx = h.Render();

        var values = ctx.Texts.Where(t => t.Color == h.Style.GroupSummaryRowTextColor).ToList();
        Assert.NotEmpty(values);
        Assert.All(values, v => Assert.Equal(0, RectMath.HorizontalOverlap(v.Visible ?? GridRect.Empty, 0, 150)));
    }
}

internal static class ColoredTextAssertions
{
    /// <summary>The single text op with this text drawn in <paramref name="color"/>.</summary>
    public static DrawOp SingleText(this RecordingDrawingContext ctx, string text, GridColor color) =>
        Assert.Single(ctx.TextsEqual(text), o => o.Color == color);
}
