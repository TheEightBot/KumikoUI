using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Key routing in <see cref="KumikoInputController.HandleKey"/> while not editing: arrows, Home/End,
/// paging, Tab, F2, Space, Escape, Ctrl+A, typing to edit, and group header keys.
/// </summary>
public class KeyboardNavigationTests
{
    private static InputGrid Grid(int rows = 30, Action<InputGrid>? configure = null) => new(rows, configure: configure);

    // ── Arrows, Home/End ──

    [Fact]
    public void Arrows_MoveTheCurrentCell_AndAreHandled()
    {
        var g = Grid();
        g.Select(5, 2);

        Assert.True(g.Key(KumikoKey.Down).Handled);
        Assert.Equal(new CellPosition(6, 2), g.Selection.CurrentCell);
        g.Key(KumikoKey.Up);
        g.Key(KumikoKey.Up);
        Assert.Equal(new CellPosition(4, 2), g.Selection.CurrentCell);
        g.Key(KumikoKey.Left);
        Assert.Equal(new CellPosition(4, 1), g.Selection.CurrentCell);
        g.Key(KumikoKey.Right);
        g.Key(KumikoKey.Right);
        Assert.Equal(new CellPosition(4, 3), g.Selection.CurrentCell);
    }

    [Fact]
    public void Arrows_StopAtTheEdges()
    {
        var g = Grid(rows: 3);
        g.Select(0, 0);

        g.Key(KumikoKey.Up);
        g.Key(KumikoKey.Left);
        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell);

        g.Select(2, 4);
        g.Key(KumikoKey.Down);
        g.Key(KumikoKey.Right);
        Assert.Equal(new CellPosition(2, 4), g.Selection.CurrentCell);
    }

    [Fact]
    public void Down_WithoutACurrentCell_StartsAtTheFirstCell()
    {
        var g = Grid();

        g.Key(KumikoKey.Down);

        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void ShiftArrow_ExtendsTheSelection()
    {
        var g = Grid();
        g.Tap(10, g.RowY(2));

        g.Key(KumikoKey.Down, InputModifiers.Shift);
        g.Key(KumikoKey.Down, InputModifiers.Shift);

        Assert.Equal(new[] { 2, 3, 4 }, g.Selection.SelectedRows.OrderBy(r => r));
    }

    [Fact]
    public void HomeAndEnd_MoveWithinTheRow()
    {
        var g = Grid();
        g.Select(7, 2);

        g.Key(KumikoKey.End);
        Assert.Equal(new CellPosition(7, 4), g.Selection.CurrentCell);
        g.Key(KumikoKey.Home);
        Assert.Equal(new CellPosition(7, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void CtrlHomeAndEnd_MoveToTheGridCorners()
    {
        var g = Grid();
        g.Select(7, 2);

        g.Key(KumikoKey.End, InputModifiers.Control);
        Assert.Equal(new CellPosition(29, 4), g.Selection.CurrentCell);
        g.Key(KumikoKey.Home, InputModifiers.Control);
        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void KeyUp_IsIgnored()
    {
        var g = Grid();
        g.Select(1, 1);
        var e = new KumikoKeyEventArgs { Key = KumikoKey.Down, IsKeyDown = false };

        g.Input.HandleKey(e, g.Scroll, g.Selection, g.Style, g.Source);

        Assert.False(e.Handled);
        Assert.Equal(new CellPosition(1, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void KeysRedrawTheGrid()
    {
        var g = Grid();
        g.Select(1, 1);
        int before = g.Redraws;

        g.Key(KumikoKey.Down);

        Assert.True(g.Redraws > before);
    }

    // ── Paging ──

    [Fact]
    public void PageDown_MovesOneViewportOfScrollableRows()
    {
        var g = Grid(); // 216 px of rows = 6
        g.Select(0, 0);

        g.Key(KumikoKey.PageDown);
        Assert.Equal(6, g.Selection.CurrentCell.Row);
        g.Key(KumikoKey.PageUp);
        Assert.Equal(0, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void PageDownAndUp_ClampAtTheGridEnds()
    {
        var g = Grid(rows: 8);
        g.Select(4, 0);

        g.Key(KumikoKey.PageDown);
        Assert.Equal(7, g.Selection.CurrentCell.Row);
        g.Key(KumikoKey.PageUp);
        g.Key(KumikoKey.PageUp);
        Assert.Equal(0, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void ShiftPageDown_ExtendsTheSelection()
    {
        var g = Grid();
        g.Tap(10, g.RowY(0));

        g.Key(KumikoKey.PageDown, InputModifiers.Shift);

        Assert.Equal(7, g.Selection.SelectedRows.Count);
    }

    [Theory]
    [InlineData("none", 6)]
    [InlineData("frozenRows", 4)]
    [InlineData("topSummary", 5)]
    [InlineData("pinnedFooter", 5)]
    [InlineData("unpinnedFooter", 6)]
    [InlineData("groupPanel", 4)]
    [InlineData("topAndFooter", 4)]
    public void PageSize_ExcludesTheFixedBands(string bands, int expectedPage)
    {
        var g = Grid(configure: x =>
        {
            switch (bands)
            {
                case "frozenRows": x.Source.FrozenRowCount = 2; break;
                case "topSummary": x.Source.AddTableSummaryRow(Summary(SummaryPosition.Top)); break;
                case "pinnedFooter": x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom)); break;
                case "unpinnedFooter":
                    x.Style.PinBottomSummaryRows = false;
                    x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom));
                    break;
                case "groupPanel": x.Source.AddGroupDescription(new GroupDescription("City")); break;
                case "topAndFooter":
                    x.Source.AddTableSummaryRow(Summary(SummaryPosition.Top));
                    x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom));
                    break;
            }
        });
        // Group headers are rows too, so start on the group's first data row there.
        int start = bands == "groupPanel" ? 1 : 0;
        g.Select(start, 0);

        g.Key(KumikoKey.PageDown);

        Assert.Equal(start + expectedPage, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void PageSize_IsAtLeastOneRow_InATinyViewport()
    {
        var g = new InputGrid(rows: 10, viewportHeight: 50);
        g.Select(0, 0);

        g.Key(KumikoKey.PageDown);

        Assert.Equal(1, g.Selection.CurrentCell.Row);
    }

    private static TableSummaryRow Summary(SummaryPosition position) => new("Totals", position)
    {
        Columns = { new SummaryColumnDescription { PropertyName = "Score", SummaryType = SummaryType.Sum } },
    };

    // ── Scroll to keep the current cell visible ──

    [Fact]
    public void Down_PastTheBottom_ScrollsTheRowIntoView()
    {
        var g = Grid();
        g.Select(5, 0);

        g.Key(KumikoKey.Down); // row 6: bottom edge 252 against 216 visible

        Assert.Equal(36, g.Scroll.OffsetY);
    }

    [Fact]
    public void Up_AboveTheTop_ScrollsTheRowToTheTop()
    {
        var g = Grid();
        g.Scroll.OffsetY = 360;
        g.Select(10, 0);

        g.Key(KumikoKey.Up); // row 9 starts at 324 < 360

        Assert.Equal(324, g.Scroll.OffsetY);
    }

    [Fact]
    public void Down_WithinTheViewport_DoesNotScroll()
    {
        var g = Grid();
        g.Select(1, 0);

        g.Key(KumikoKey.Down);

        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void CtrlEnd_ScrollsToTheLastRow()
    {
        var g = Grid();
        g.Select(0, 0);

        g.Key(KumikoKey.End, InputModifiers.Control);

        Assert.Equal(g.Scroll.MaxOffsetY, g.Scroll.OffsetY);
        Assert.Equal(30 * 36 - 216, g.Scroll.OffsetY);
        Assert.Equal(g.Scroll.MaxOffsetX, g.Scroll.OffsetX);
    }

    [Fact]
    public void Scroll_WithFrozenRows_ReservesTheirHeight()
    {
        var g = Grid(configure: x => x.Source.FrozenRowCount = 2); // 144 px for scrollable rows
        g.Select(4, 0);

        g.Key(KumikoKey.Down); // row 5: scrollable top 108, bottom 144 fits
        Assert.Equal(0, g.Scroll.OffsetY);
        g.Key(KumikoKey.Down); // row 6: bottom 180
        Assert.Equal(36, g.Scroll.OffsetY);
    }

    [Fact]
    public void Scroll_ToAFrozenRow_LeavesTheOffset()
    {
        var g = Grid(configure: x => x.Source.FrozenRowCount = 2);
        g.Scroll.OffsetY = 100;
        g.Select(0, 0);

        g.Key(KumikoKey.Down); // row 1 is frozen: always visible

        Assert.Equal(100, g.Scroll.OffsetY);
    }

    [Fact]
    public void Scroll_ToTheFirstScrollableRow_AfterFrozenRows_BringsItIntoView()
    {
        var g = Grid(configure: x => x.Source.FrozenRowCount = 2);
        g.Scroll.OffsetY = 100;
        g.Select(1, 0);

        g.Key(KumikoKey.Down); // row 2 is scrollable row 0: top 0 < 100

        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void Scroll_WithTopSummary_ReservesItsHeight()
    {
        var g = Grid(configure: x => x.Source.AddTableSummaryRow(Summary(SummaryPosition.Top))); // 180 px
        g.Select(4, 0);

        g.Key(KumikoKey.Down); // row 5: bottom 216 against 180

        Assert.Equal(36, g.Scroll.OffsetY);
    }

    [Fact]
    public void Scroll_WithPinnedFooter_KeepsTheRowAboveIt()
    {
        var g = Grid(configure: x => x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom)));
        g.Select(4, 0);

        g.Key(KumikoKey.Down);

        Assert.Equal(36, g.Scroll.OffsetY);
    }

    [Fact]
    public void Scroll_WithUnpinnedFooter_UsesTheWholeViewport()
    {
        var g = Grid(configure: x =>
        {
            x.Style.PinBottomSummaryRows = false;
            x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom));
        });
        g.Select(4, 0);

        g.Key(KumikoKey.Down);

        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void Scroll_WithGroupPanel_ReservesItsHeight()
    {
        var g = Grid(configure: x => x.Source.AddGroupDescription(new GroupDescription("City"))); // 176 px
        g.Select(4, 0);

        g.Key(KumikoKey.Down); // row 5: bottom 216 against 176

        Assert.Equal(40, g.Scroll.OffsetY);
    }

    [Fact]
    public void Scroll_Right_BringsTheColumnIntoView()
    {
        var g = Grid();
        g.Select(0, 3);

        g.Key(KumikoKey.Right); // Score spans 400..500 of 300 visible

        Assert.Equal(200, g.Scroll.OffsetX);
        g.Key(KumikoKey.Left); // Dept 300..400 is inside 200..500
        Assert.Equal(200, g.Scroll.OffsetX);
        g.Key(KumikoKey.Left);
        g.Key(KumikoKey.Left); // Age 120..200 starts left of 200
        Assert.Equal(120, g.Scroll.OffsetX);
        g.Key(KumikoKey.Home);
        Assert.Equal(0, g.Scroll.OffsetX);
    }

    [Fact]
    public void Scroll_WithLeftFrozenColumn_StartsAfterIt()
    {
        var g = Grid(configure: x => x.Name.IsFrozen = true);
        g.Select(0, 3);

        g.Key(KumikoKey.Right); // Score at content 400..500
        Assert.Equal(200, g.Scroll.OffsetX);
        g.Key(KumikoKey.Left); // Dept at 300..400 against a scrollable window starting at 200 + 120
        Assert.Equal(180, g.Scroll.OffsetX);
        g.Key(KumikoKey.Home); // frozen column: no horizontal scroll
        Assert.Equal(180, g.Scroll.OffsetX);
    }

    [Fact]
    public void Scroll_WithRightFrozenColumn_EndsBeforeIt()
    {
        var g = Grid(configure: x => x.Score.IsFrozenRight = true);
        g.Select(0, 2);

        g.Key(KumikoKey.Right); // Dept 300..400 against a window ending at 300 - 100
        Assert.Equal(200, g.Scroll.OffsetX);
        g.Key(KumikoKey.Right); // frozen right: unchanged
        Assert.Equal(200, g.Scroll.OffsetX);
    }

    [Fact]
    public void Scroll_IsClampedToTheContent()
    {
        var g = Grid(rows: 3, configure: x => x.Scroll.OffsetY = 0);
        g.Select(2, 4);

        g.Key(KumikoKey.End);

        Assert.True(g.Scroll.OffsetY <= g.Scroll.MaxOffsetY);
        Assert.True(g.Scroll.OffsetX <= g.Scroll.MaxOffsetX);
    }

    // ── Ensure-visible with every combination of bands ──

    public static TheoryData<bool, bool, bool, bool> BandCombinations()
    {
        var data = new TheoryData<bool, bool, bool, bool>();
        for (int bits = 0; bits < 16; bits++)
            data.Add((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0);
        return data;
    }

    private static InputGrid Banded(bool groupPanel, bool topSummary, bool frozenRows, bool pinnedFooter, float viewportHeight = 420) =>
        new(rows: 30, viewportHeight: viewportHeight, configure: x =>
        {
            if (groupPanel) x.Source.AddGroupDescription(new GroupDescription("City"));
            if (topSummary) x.Source.AddTableSummaryRow(Summary(SummaryPosition.Top));
            if (frozenRows) x.Source.FrozenRowCount = 2;
            if (pinnedFooter) x.Source.AddTableSummaryRow(Summary(SummaryPosition.Bottom));
        });

    /// <summary>
    /// The current row is entirely on screen: the hit tester (independent of the scroll math) finds that row
    /// just inside its top and bottom edges, not a band drawn over it.
    /// </summary>
    private static void AssertCurrentRowFullyVisible(InputGrid g)
    {
        int row = g.Selection.CurrentCell.Row;
        float groupPanel = g.Source.IsGroupingActive ? g.Style.GroupPanelHeight : 0;
        float topSummary = g.Source.TopSummaryCount * g.Style.SummaryRowHeight;
        var bounds = g.Input.GetCellScreenBounds(row, 0, g.Scroll, g.Style, g.Source.Columns, groupPanel, topSummary, g.Source);

        foreach (float y in new[] { bounds.Y + 1, bounds.Y + bounds.Height / 2, bounds.Bottom - 1 })
        {
            var hit = g.Hit(10, y);
            Assert.True(hit.RowIndex == row && hit.Region is HitRegion.Cell or HitRegion.GroupHeaderRow or HitRegion.GroupChevron,
                $"row {row} at y {y} hit {hit.Region} row {hit.RowIndex} (offset {g.Scroll.OffsetY})");
        }
    }

    [Theory]
    [MemberData(nameof(BandCombinations))]
    public void DownAndUp_KeepTheCurrentRowFullyVisible_WithEveryBandCombination(
        bool groupPanel, bool topSummary, bool frozenRows, bool pinnedFooter)
    {
        var g = Banded(groupPanel, topSummary, frozenRows, pinnedFooter);
        int rows = g.Source.RowCount;
        g.Select(0, 0);

        for (int i = 0; i < rows - 1; i++)
        {
            g.Key(KumikoKey.Down);
            AssertCurrentRowFullyVisible(g);
        }
        Assert.Equal(rows - 1, g.Selection.CurrentCell.Row);
        Assert.Equal(g.Scroll.MaxOffsetY, g.Scroll.OffsetY);

        for (int i = 0; i < rows - 1; i++)
        {
            g.Key(KumikoKey.Up);
            AssertCurrentRowFullyVisible(g);
        }
        Assert.Equal(0, g.Selection.CurrentCell.Row);
    }

    [Theory]
    [MemberData(nameof(BandCombinations))]
    public void PageDownAndPageUp_KeepTheCurrentRowFullyVisible_WithEveryBandCombination(
        bool groupPanel, bool topSummary, bool frozenRows, bool pinnedFooter)
    {
        var g = Banded(groupPanel, topSummary, frozenRows, pinnedFooter);
        g.Select(0, 0);

        while (g.Selection.CurrentCell.Row < g.Source.RowCount - 1)
        {
            int before = g.Selection.CurrentCell.Row;
            g.Key(KumikoKey.PageDown);
            AssertCurrentRowFullyVisible(g);
            Assert.True(g.Selection.CurrentCell.Row > before);
        }

        while (g.Selection.CurrentCell.Row > 0)
        {
            g.Key(KumikoKey.PageUp);
            AssertCurrentRowFullyVisible(g);
        }
    }

    [Fact]
    public void Down_WithTopSummaryFrozenRowsAndPinnedFooter_ScrollsExactlyEnough()
    {
        // 420 - header 40 - top summary 36 - 2 frozen rows 72 - pinned footer 36 = 236 px for scrollable rows.
        // Rows 0 and 1 are frozen, so row 2 is scrollable row 0.
        var g = Banded(groupPanel: false, topSummary: true, frozenRows: true, pinnedFooter: true);
        g.Select(2, 0);

        for (int i = 0; i < 5; i++) g.Key(KumikoKey.Down); // row 7: scrollable bottom 216 fits
        Assert.Equal(0, g.Scroll.OffsetY);

        g.Key(KumikoKey.Down); // row 8: scrollable bottom 252 against 236
        Assert.Equal(16, g.Scroll.OffsetY);

        g.Key(KumikoKey.End, InputModifiers.Control); // row 29: scrollable bottom 28 * 36
        Assert.Equal(28 * 36 - 236, g.Scroll.OffsetY);
        Assert.Equal(g.Scroll.MaxOffsetY, g.Scroll.OffsetY);
    }

    [Fact]
    public void Down_WithGroupPanelTopSummaryAndPinnedFooter_ScrollsExactlyEnough()
    {
        // 420 - group panel 40 - header 40 - top summary 36 - pinned footer 36 = 268 px. Frozen rows don't apply
        // while grouped. Grouped by City: 32 rows, group headers at 0 and 16.
        var g = Banded(groupPanel: true, topSummary: true, frozenRows: true, pinnedFooter: true);
        Assert.Equal(0, g.Source.EffectiveFrozenRowCount);
        g.Select(0, 0);

        for (int i = 0; i < 6; i++) g.Key(KumikoKey.Down); // row 6: bottom 252 fits
        Assert.Equal(0, g.Scroll.OffsetY);

        g.Key(KumikoKey.Down); // row 7: bottom 288 against 268
        Assert.Equal(20, g.Scroll.OffsetY);

        g.Key(KumikoKey.End, InputModifiers.Control);
        Assert.Equal(32 * 36 - 268, g.Scroll.OffsetY);
        Assert.Equal(g.Scroll.MaxOffsetY, g.Scroll.OffsetY);
    }

    [Fact]
    public void Up_ToTheFirstScrollableRow_UnderFrozenRowsAndATopSummary_ScrollsToTheTop()
    {
        var g = Banded(groupPanel: false, topSummary: true, frozenRows: true, pinnedFooter: true);
        g.Select(3, 0);
        g.Scroll.OffsetY = 300;

        g.Key(KumikoKey.Up); // row 2: scrollable top 0, hidden under the frozen rows until scrolled back

        Assert.Equal(0, g.Scroll.OffsetY);
        AssertCurrentRowFullyVisible(g);
    }

    [Fact]
    public void Up_UnderTheGroupPanelHeaderAndTopSummary_ScrollsTheRowToTheTop()
    {
        var g = Banded(groupPanel: true, topSummary: true, frozenRows: false, pinnedFooter: false);
        g.Style.StickyGroupHeaders = false; // the fixed bands only; the sticky band is covered in StickyGroupHeaderTests (#174)
        g.Select(11, 0);
        g.Scroll.OffsetY = 400;

        g.Key(KumikoKey.Up); // row 10 starts at 360 < 400

        Assert.Equal(360, g.Scroll.OffsetY);
        AssertCurrentRowFullyVisible(g);
    }

    [Theory]
    [InlineData(DragHandlePosition.Left)]
    [InlineData(DragHandlePosition.Right)]
    public void Scroll_Right_WithARowDragHandle_BringsTheWholeColumnIntoView(DragHandlePosition position)
    {
        var g = Grid(configure: x =>
        {
            x.Style.ShowRowDragHandle = true;
            x.Style.RowDragHandlePosition = position;
        });
        g.Select(0, 3);

        g.Key(KumikoKey.Right); // Score 400..500 against 300 - 32 px of columns

        Assert.Equal(232, g.Scroll.OffsetX);
        Assert.Equal(g.Scroll.MaxOffsetX, g.Scroll.OffsetX);
        var bounds = g.Input.GetCellScreenBounds(0, 4, g.Scroll, g.Style, g.Source.Columns, dataSource: g.Source);
        Assert.Equal(4, g.Hit(bounds.Right - 1, g.RowY(0)).ColumnIndex);
        Assert.Equal(4, g.Hit(bounds.X + 1, g.RowY(0)).ColumnIndex);
    }

    [Fact]
    public void Scroll_WithFrozenColumnsOnBothSidesAndADragHandle_KeepsTheColumnBetweenThem()
    {
        var g = new InputGrid(viewportWidth: 400, configure: x =>
        {
            x.Name.IsFrozen = true;
            x.Score.IsFrozenRight = true;
            x.Style.ShowRowDragHandle = true;
        });
        g.Select(0, 2);

        // Dept at content 300..400; the columns get 400 - 32 (handle) - 100 (Score) = 268 px, Name's 120 included
        g.Key(KumikoKey.Right);

        Assert.Equal(400 - 268, g.Scroll.OffsetX);
        var bounds = g.Input.GetCellScreenBounds(0, 3, g.Scroll, g.Style, g.Source.Columns, dataSource: g.Source);
        Assert.Equal(3, g.Hit(bounds.Right - 1, g.RowY(0)).ColumnIndex);
        Assert.Equal(3, g.Hit(bounds.X + 1, g.RowY(0)).ColumnIndex);
    }

    // ── Space, Escape, Ctrl+A ──

    [Fact]
    public void Space_TogglesTheCurrentRow()
    {
        var g = Grid();
        g.Select(3, 1);

        Assert.True(g.Key(KumikoKey.Space).Handled);
        Assert.True(g.Selection.IsRowSelected(3));
        g.Key(KumikoKey.Space);
        Assert.False(g.Selection.IsRowSelected(3));
    }

    [Fact]
    public void Space_WithoutACurrentCell_IsNotHandled()
    {
        var g = Grid();

        Assert.False(g.Key(KumikoKey.Space).Handled);
    }

    [Fact]
    public void Escape_ClearsTheSelectionAndCurrentCell()
    {
        var g = Grid();
        g.Tap(10, g.RowY(2));
        Assert.NotEmpty(g.Selection.SelectedRows);

        Assert.True(g.Key(KumikoKey.Escape).Handled);

        Assert.Empty(g.Selection.SelectedRows);
        Assert.False(g.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void CtrlA_SelectsEveryRow()
    {
        var g = Grid();
        g.Select(0, 0);

        Assert.True(g.Key(KumikoKey.A, InputModifiers.Control).Handled);

        Assert.Equal(30, g.Selection.SelectedRows.Count);
    }

    [Fact]
    public void A_WithoutControl_IsNotHandled()
    {
        var g = Grid();
        g.Select(0, 0);

        Assert.False(g.Key(KumikoKey.A).Handled);
        Assert.Empty(g.Selection.SelectedRows);
    }

    // ── Typing and F2 start an edit ──

    [Fact]
    public void Typing_StartsAnEditWithTheCharacter()
    {
        var g = Grid();
        g.Select(2, 0);

        var e = g.Type('Z');

        Assert.True(e.Handled);
        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(2, 0), g.Edit.EditCell);
        Assert.True(g.Selection.IsEditing);
        g.Key(KumikoKey.Enter);
        Assert.Equal("Z", g.Items[2].Name);
    }

    [Fact]
    public void Typing_WithControlOrAltOrAControlCharacter_DoesNotEdit()
    {
        var g = Grid();
        g.Select(2, 0);

        g.Key(KumikoKey.Character, InputModifiers.Control, 'z');
        g.Key(KumikoKey.Character, InputModifiers.Alt, 'z');
        g.Key(KumikoKey.Character, character: '\u0007');

        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void Typing_WithoutTheTypingTrigger_DoesNotEdit()
    {
        var g = Grid(configure: x => x.Edit.EditTriggers = EditTrigger.DoubleTap | EditTrigger.F2Key);
        g.Select(2, 0);

        var e = g.Type('Z');

        Assert.False(e.Handled);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void Typing_ColumnTriggersOverrideTheGridTriggers()
    {
        var g = Grid(configure: x => x.Age.EditTriggers = EditTrigger.None);
        g.Select(2, 1);
        g.Type('7');
        Assert.False(g.Edit.IsEditing);

        g.Select(2, 0);
        g.Type('7');
        Assert.True(g.Edit.IsEditing);
    }

    [Fact]
    public void Typing_IntoAReadOnlyColumn_DoesNotEdit()
    {
        var g = Grid(configure: x => x.Name.IsReadOnly = true);
        g.Select(2, 0);

        g.Type('Z');

        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void Typing_WithoutAnEditSession_DoesNothing()
    {
        var g = new InputGrid(withEditSession: false);
        g.Select(2, 0);

        var e = g.Type('Z');

        Assert.False(e.Handled);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void F2_StartsAnEditOfTheCurrentCell()
    {
        var g = Grid();
        g.Select(4, 2);

        Assert.True(g.Key(KumikoKey.F2).Handled);

        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(4, 2), g.Edit.EditCell);
    }

    [Fact]
    public void F2_ScrollsTheCellIntoViewBeforeEditing()
    {
        var g = Grid();
        g.Select(10, 4);

        g.Key(KumikoKey.F2);

        Assert.True(g.Edit.IsEditing);
        Assert.Equal(10 * 36 + 36 - 216, g.Scroll.OffsetY);
        Assert.Equal(200, g.Scroll.OffsetX);
    }

    [Fact]
    public void F2_WithoutTheTrigger_Or_OnAReadOnlyCell_DoesNotEdit()
    {
        var g = Grid(configure: x =>
        {
            x.Edit.EditTriggers = EditTrigger.Typing;
            x.Age.EditTriggers = EditTrigger.DoubleTap;
        });
        g.Select(1, 0);
        Assert.False(g.Key(KumikoKey.F2).Handled);
        Assert.False(g.Edit.IsEditing);

        var ro = Grid(configure: x => x.Name.IsReadOnly = true);
        ro.Select(1, 0);
        ro.Key(KumikoKey.F2);
        Assert.False(ro.Edit.IsEditing);
    }

    [Fact]
    public void F2_OnAGroupHeader_DoesNotEdit()
    {
        var g = Grid(rows: 4, configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));
        g.Select(0, 0);

        g.Key(KumikoKey.F2);

        Assert.False(g.Edit.IsEditing);
    }

    // ── Tab ──

    [Fact]
    public void Tab_MovesToTheNextEditableCellAndBeginsEditing()
    {
        var g = Grid();
        g.Select(0, 0);

        Assert.True(g.Key(KumikoKey.Tab).Handled);

        Assert.Equal(new CellPosition(0, 1), g.Selection.CurrentCell);
        Assert.True(g.Edit.IsEditing);
    }

    [Fact]
    public void Tab_SkipsReadOnlyAndNonTabStopColumns()
    {
        var g = Grid(configure: x =>
        {
            x.Age.IsReadOnly = true;
            x.City.AllowTabStop = false;
        });
        g.Select(0, 0);

        g.Key(KumikoKey.Tab);

        Assert.Equal(new CellPosition(0, 3), g.Selection.CurrentCell);
    }

    // The five test columns are Name(0) Age(1) City(2) Dept(3) Score(4). Every editable column type is a Tab
    // stop; only read-only, hidden and AllowTabStop = false columns are skipped.
    public static TheoryData<KumikoColumnType> EditableColumnTypes => new()
    {
        KumikoColumnType.Text,
        KumikoColumnType.Numeric,
        KumikoColumnType.Date,
        KumikoColumnType.Boolean,
        KumikoColumnType.ComboBox,
        KumikoColumnType.Picker,
        KumikoColumnType.Template,
    };

    private static void MakeColumnType(KumikoColumn column, KumikoColumnType type)
    {
        column.ColumnType = type;
        if (type is KumikoColumnType.ComboBox or KumikoColumnType.Picker)
            column.EditorItems = new List<string> { "A", "B" };
    }

    [Theory]
    [MemberData(nameof(EditableColumnTypes))]
    public void Tab_StopsAtAnEditableColumnOfEveryType(KumikoColumnType type)
    {
        var g = Grid(configure: x => MakeColumnType(x.City, type));
        g.Select(0, 1);

        Assert.True(g.Key(KumikoKey.Tab).Handled);

        Assert.Equal(new CellPosition(0, 2), g.Selection.CurrentCell);
    }

    [Theory]
    [MemberData(nameof(EditableColumnTypes))]
    public void ShiftTab_StopsAtAnEditableColumnOfEveryType(KumikoColumnType type)
    {
        var g = Grid(configure: x => MakeColumnType(x.City, type));
        g.Select(0, 3);

        Assert.True(g.Key(KumikoKey.Tab, InputModifiers.Shift).Handled);

        Assert.Equal(new CellPosition(0, 2), g.Selection.CurrentCell);
    }

    [Theory]
    [MemberData(nameof(EditableColumnTypes))]
    public void Tab_SkipsAColumnOfEveryTypeThatOptsOutOrIsReadOnly(KumikoColumnType type)
    {
        var g = Grid(configure: x =>
        {
            MakeColumnType(x.City, type);
            MakeColumnType(x.Dept, type);
            x.City.AllowTabStop = false;
            x.Dept.IsReadOnly = true;
        });
        g.Select(0, 1);

        g.Key(KumikoKey.Tab);

        Assert.Equal(new CellPosition(0, 4), g.Selection.CurrentCell);

        g.Edit.CancelEdit();
        g.Key(KumikoKey.Tab, InputModifiers.Shift);

        Assert.Equal(new CellPosition(0, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_WalksEveryEditableColumnInAMixedRow_ThenLeavesTheGrid()
    {
        var g = Grid(rows: 1, configure: x =>
        {
            x.Age.ColumnType = KumikoColumnType.Date;
            x.City.ColumnType = KumikoColumnType.Boolean;
            MakeColumnType(x.Dept, KumikoColumnType.ComboBox);
            MakeColumnType(x.Score, KumikoColumnType.Picker);
        });
        g.Select(0, 0);

        var visited = new List<int>();
        while (g.Key(KumikoKey.Tab).Handled)
        {
            visited.Add(g.Selection.CurrentCell.Column);
            g.Edit.CancelEdit();
        }

        Assert.Equal(new[] { 1, 2, 3, 4 }, visited);
        Assert.Equal(new CellPosition(0, 4), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_StopsAtAnInteractiveActionButtonColumn_WithoutOpeningAnEditor()
    {
        var g = Grid(configure: x =>
        {
            x.City.PropertyName = "";
            x.City.ColumnType = KumikoColumnType.Template;
            x.City.CustomCellRenderer = new KumikoUI.Core.Rendering.ActionButtonsCellRenderer
            {
                Buttons = { new KumikoUI.Core.Rendering.ActionButtonDefinition { Label = "Go", Action = () => { } } },
            };
        });
        g.Select(0, 1);

        Assert.True(g.Key(KumikoKey.Tab).Handled);

        Assert.Equal(new CellPosition(0, 2), g.Selection.CurrentCell);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void Tab_SkipsAHiddenColumn()
    {
        var g = Grid(configure: x => x.City.IsVisible = false);
        g.Select(0, 1);

        g.Key(KumikoKey.Tab);

        Assert.Equal(new CellPosition(0, 3), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_AtTheRowEnd_WrapsToTheNextRow()
    {
        var g = Grid();
        g.Select(3, 4);

        g.Key(KumikoKey.Tab);

        Assert.Equal(new CellPosition(4, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void ShiftTab_MovesBackAndWrapsToThePreviousRow()
    {
        var g = Grid();
        g.Select(3, 1);
        g.Key(KumikoKey.Tab, InputModifiers.Shift);
        Assert.Equal(new CellPosition(3, 0), g.Selection.CurrentCell);

        g.Edit.CancelEdit();
        g.Select(3, 0);
        g.Key(KumikoKey.Tab, InputModifiers.Shift);
        Assert.Equal(new CellPosition(2, 4), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_SkipsGroupHeaderRows()
    {
        var g = Grid(rows: 4, configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));
        // Rows: 0 header, 1 and 2 data, 3 header, 4 and 5 data.
        g.Select(2, 4);

        g.Key(KumikoKey.Tab);

        Assert.Equal(new CellPosition(4, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_WithoutACurrentCell_IsLeftUnhandled()
    {
        var g = Grid();

        var e = g.Key(KumikoKey.Tab);

        Assert.False(e.Handled);
        Assert.False(g.Selection.CurrentCell.IsValid);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void Tab_AtTheLastEditableCell_IsLeftUnhandled()
    {
        var g = Grid(rows: 2);
        g.Select(1, 4);

        var e = g.Key(KumikoKey.Tab);

        Assert.False(e.Handled);
        Assert.Equal(new CellPosition(1, 4), g.Selection.CurrentCell);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void Tab_BeforeTrailingReadOnlyColumnsOfTheLastRow_IsLeftUnhandled()
    {
        var g = Grid(rows: 2, configure: x =>
        {
            x.Dept.IsReadOnly = true;
            x.Score.AllowTabStop = false;
        });
        g.Select(1, 2);

        Assert.False(g.Key(KumikoKey.Tab).Handled);
        Assert.Equal(new CellPosition(1, 2), g.Selection.CurrentCell);
    }

    [Fact]
    public void ShiftTab_AtTheFirstEditableCell_IsLeftUnhandled()
    {
        var g = Grid(configure: x => x.Name.IsReadOnly = true);
        g.Select(0, 1);

        var e = g.Key(KumikoKey.Tab, InputModifiers.Shift);

        Assert.False(e.Handled);
        Assert.Equal(new CellPosition(0, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_WhenNoColumnIsATabStop_IsLeftUnhandled()
    {
        // A display-only grid with many rows: the search is bounded by rows, not rows x columns.
        var g = new InputGrid(rows: 50_000, configure: x =>
        {
            foreach (var c in x.Source.Columns) c.IsReadOnly = true;
        });
        g.Select(0, 0);

        Assert.False(g.Key(KumikoKey.Tab).Handled);
        Assert.False(g.Key(KumikoKey.Tab, InputModifiers.Shift).Handled);
        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_WithEditingDisabled_DoesNotBeginAnEdit()
    {
        var g = Grid(configure: x => x.Edit.EditTriggers = EditTrigger.None);
        g.Select(0, 0);

        var e = g.Key(KumikoKey.Tab);

        Assert.False(g.Edit.IsEditing);
        Assert.True(e.Handled);
        Assert.Equal(new CellPosition(0, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_IntoAColumnWithEditingDisabled_OnlyMoves()
    {
        var g = Grid(configure: x => x.Age.EditTriggers = EditTrigger.None);
        g.Select(0, 0);

        g.Key(KumikoKey.Tab);
        Assert.Equal(new CellPosition(0, 1), g.Selection.CurrentCell);
        Assert.False(g.Edit.IsEditing);

        g.Key(KumikoKey.Tab);
        Assert.Equal(new CellPosition(0, 2), g.Selection.CurrentCell);
        Assert.True(g.Edit.IsEditing);
    }

    [Fact]
    public void Tab_WhileEditingTheLastEditableCell_CommitsAndIsLeftUnhandled()
    {
        var g = Grid(rows: 2);
        g.Select(1, 0);
        g.Key(KumikoKey.End);
        g.Key(KumikoKey.F2);
        Assert.True(g.Edit.IsEditing);

        var e = g.Key(KumikoKey.Tab);

        Assert.False(e.Handled);
        Assert.False(g.Edit.IsEditing);
        Assert.False(g.Selection.IsEditing);
        Assert.Equal(new CellPosition(1, 4), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_WhileEditing_IntoAColumnWithEditingDisabled_CommitsAndOnlyMoves()
    {
        var g = Grid(configure: x => x.Age.EditTriggers = EditTrigger.None);
        g.Select(0, 0);
        g.Type('W');

        Assert.True(g.Key(KumikoKey.Tab).Handled);

        Assert.Equal("W", g.Items[0].Name);
        Assert.Equal(new CellPosition(0, 1), g.Selection.CurrentCell);
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void ShiftTab_SkipsGroupHeaderRows()
    {
        var g = Grid(rows: 4, configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));
        // Rows: 0 header, 1 and 2 data, 3 header, 4 and 5 data.
        g.Select(4, 0);

        g.Key(KumikoKey.Tab, InputModifiers.Shift);

        Assert.Equal(new CellPosition(2, 4), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_FromAGroupHeaderRow_GoesToTheFirstCellOfTheNextDataRow()
    {
        var g = Grid(rows: 4, configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));
        g.Select(3, 2);

        Assert.True(g.Key(KumikoKey.Tab).Handled);

        Assert.Equal(new CellPosition(4, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void Tab_ScrollsTheNextCellIntoView()
    {
        var g = Grid();
        g.Select(5, 4);

        g.Key(KumikoKey.Tab); // row 6: bottom 252 against 216 visible; Name back at x 0

        Assert.Equal(new CellPosition(6, 0), g.Selection.CurrentCell);
        Assert.Equal(36, g.Scroll.OffsetY);
        Assert.Equal(0, g.Scroll.OffsetX);
    }

    [Fact]
    public void WouldHandleTab_MatchesHandleKey()
    {
        var g = Grid(rows: 2);

        Assert.False(g.Input.WouldHandleTab(g.Selection, g.Source, backwards: false)); // no current cell

        g.Select(0, 0);
        Assert.True(g.Input.WouldHandleTab(g.Selection, g.Source, backwards: false));
        Assert.False(g.Input.WouldHandleTab(g.Selection, g.Source, backwards: true));

        g.Select(1, 4);
        Assert.False(g.Input.WouldHandleTab(g.Selection, g.Source, backwards: false));
        Assert.True(g.Input.WouldHandleTab(g.Selection, g.Source, backwards: true));

        g.Key(KumikoKey.F2); // an open edit: Tab commits first, so it is always processed
        Assert.True(g.Input.WouldHandleTab(g.Selection, g.Source, backwards: false));
    }

    // ── Enter ──

    [Fact]
    public void Enter_BeginsAnEditOfTheCurrentCell()
    {
        var g = Grid();
        g.Select(2, 0);

        var e = g.Key(KumikoKey.Enter);

        Assert.True(e.Handled);
        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(2, 0), g.Edit.EditCell);
    }

    [Fact]
    public void Enter_ScrollsTheCellIntoViewBeforeEditing()
    {
        var g = Grid();
        g.Select(10, 4);

        g.Key(KumikoKey.Enter);

        Assert.True(g.Edit.IsEditing);
        Assert.Equal(10 * 36 + 36 - 216, g.Scroll.OffsetY);
        Assert.Equal(200, g.Scroll.OffsetX);
    }

    [Fact]
    public void Enter_WithoutTheF2Trigger_IsLeftUnhandled()
    {
        var g = Grid(configure: x => x.Age.EditTriggers = EditTrigger.DoubleTap | EditTrigger.Typing);
        g.Select(2, 1);

        var e = g.Key(KumikoKey.Enter);

        Assert.False(e.Handled);
        Assert.False(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(2, 1), g.Selection.CurrentCell);
    }

    [Fact]
    public void Enter_WithEditingDisabled_OrOnAReadOnlyCell_OrWithoutACurrentCell_IsLeftUnhandled()
    {
        var none = Grid(configure: x => x.Edit.EditTriggers = EditTrigger.None);
        none.Select(2, 0);
        Assert.False(none.Key(KumikoKey.Enter).Handled);
        Assert.False(none.Edit.IsEditing);

        var readOnly = Grid(configure: x => x.Name.IsReadOnly = true);
        readOnly.Select(2, 0);
        Assert.False(readOnly.Key(KumikoKey.Enter).Handled);
        Assert.False(readOnly.Edit.IsEditing);

        var empty = Grid();
        Assert.False(empty.Key(KumikoKey.Enter).Handled);
    }

    [Fact]
    public void Enter_WhenBeginEditIsCanceled_IsLeftUnhandled()
    {
        var g = Grid(configure: x => x.Edit.CellBeginEdit += (_, a) => a.Cancel = true);
        g.Select(2, 0);

        Assert.False(g.Key(KumikoKey.Enter).Handled);
        Assert.False(g.Edit.IsEditing);
    }

    // ── Escape ──

    [Fact]
    public void Escape_WithNothingSelected_IsLeftUnhandled()
    {
        var g = Grid();

        Assert.False(g.Key(KumikoKey.Escape).Handled);
    }

    // ── Space on Boolean cells ──

    [Fact]
    public void Space_TogglesTheCurrentBooleanCell_EachPress()
    {
        var g = BooleanColumnGrid.Create();
        g.Select(0, 0);

        Assert.True(g.Key(KumikoKey.Space).Handled);
        Assert.True(g.Items[0].On);
        g.Key(KumikoKey.Space);
        Assert.False(g.Items[0].On);
        Assert.False(g.Selection.IsRowSelected(0)); // the row selection is left alone
    }

    [Fact]
    public void Space_OnAReadOnlyOrDisabledBooleanCell_TogglesTheRowSelectionInstead()
    {
        var readOnly = BooleanColumnGrid.Create(configure: c => c.IsReadOnly = true);
        readOnly.Select(0, 0);
        Assert.True(readOnly.Key(KumikoKey.Space).Handled);
        Assert.False(readOnly.Items[0].On);
        Assert.True(readOnly.Selection.IsRowSelected(0));

        var disabled = BooleanColumnGrid.Create(EditTrigger.None);
        disabled.Select(0, 0);
        disabled.Key(KumikoKey.Space);
        Assert.False(disabled.Items[0].On);
        Assert.True(disabled.Selection.IsRowSelected(0));
    }

    [Fact]
    public void Space_OnABooleanCell_GoesThroughTheValidator()
    {
        var g = BooleanColumnGrid.Create();
        g.Edit.CellValidator = (_, _, _) => CellValidationResult.Error("No.");
        g.Select(0, 0);

        g.Key(KumikoKey.Space);

        Assert.False(g.Items[0].On);
    }

    [Fact]
    public void ATypedSpace_TogglesTheCurrentBooleanCell_LikeTheSpaceKey()
    {
        // Hosts that capture the keyboard through a hidden text box deliver Space as text (#179)
        var g = BooleanColumnGrid.Create();
        g.Select(0, 0);

        Assert.True(g.Type(' ').Handled);
        Assert.True(g.Items[0].On);
        g.Type(' ');
        Assert.False(g.Items[0].On);
        Assert.False(g.Edit.IsEditing);
    }

    [Theory]
    [InlineData(KumikoKey.Enter)]
    [InlineData(KumikoKey.F2)]
    public void EnterAndF2_ToggleTheCurrentBooleanCell(KumikoKey key)
    {
        var g = BooleanColumnGrid.Create();
        g.Select(0, 0);

        Assert.True(g.Key(key).Handled);
        Assert.True(g.Items[0].On);
        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell); // the current cell stays put
        g.Key(key);
        Assert.False(g.Items[0].On);
    }

    [Fact]
    public void TheKeyboardToggles_LeaveAReadOnlyOrDisabledBooleanCellAlone()
    {
        foreach (var g in new[]
                 {
                     BooleanColumnGrid.Create(configure: c => c.IsReadOnly = true),
                     BooleanColumnGrid.Create(EditTrigger.None),
                 })
        {
            g.Select(0, 0);
            g.Type(' ');
            g.Key(KumikoKey.Enter);
            g.Key(KumikoKey.F2);
            Assert.False(g.Items[0].On);
        }
    }

    [Fact]
    public void ATypedSpace_OnATextCell_StillStartsAnEdit()
    {
        var g = Grid();
        g.Select(0, 1);

        g.Type(' ');

        Assert.True(g.Edit.IsEditing);
    }

    // ── Group header keys ──

    private static InputGrid Grouped() =>
        Grid(rows: 4, configure: x => x.Source.AddGroupDescription(new GroupDescription("City")));

    [Fact]
    public void GroupHeader_LeftCollapses_RightExpands()
    {
        var g = Grouped();
        int expanded = g.Source.RowCount;
        g.Select(0, 0);

        Assert.True(g.Key(KumikoKey.Left).Handled);
        Assert.True(g.Source.RowCount < expanded);
        Assert.False(g.Source.GetGroupHeaderInfo(0)!.IsExpanded);

        g.Key(KumikoKey.Left); // already collapsed: no-op
        Assert.False(g.Source.GetGroupHeaderInfo(0)!.IsExpanded);

        Assert.True(g.Key(KumikoKey.Right).Handled);
        Assert.True(g.Source.GetGroupHeaderInfo(0)!.IsExpanded);
        Assert.Equal(expanded, g.Source.RowCount);

        g.Key(KumikoKey.Right); // already expanded: no-op
        Assert.Equal(expanded, g.Source.RowCount);
    }

    [Theory]
    [InlineData(KumikoKey.Space)]
    [InlineData(KumikoKey.Enter)]
    public void GroupHeader_SpaceAndEnter_Toggle(KumikoKey key)
    {
        var g = Grouped();
        int expanded = g.Source.RowCount;
        g.Select(0, 0);

        Assert.True(g.Key(key).Handled);
        Assert.True(g.Source.RowCount < expanded);
        g.Key(key);
        Assert.Equal(expanded, g.Source.RowCount);
    }

    [Fact]
    public void GroupHeader_UpAndDown_Navigate()
    {
        var g = Grouped();
        g.Select(0, 0);

        g.Key(KumikoKey.Down);
        Assert.Equal(1, g.Selection.CurrentCell.Row);

        g.Select(0, 0);
        g.Key(KumikoKey.Up);
        Assert.Equal(0, g.Selection.CurrentCell.Row);
    }

    [Fact]
    public void GroupHeader_OtherKeysFallThrough()
    {
        var g = Grouped();
        g.Select(0, 0);

        g.Key(KumikoKey.End);

        Assert.Equal(new CellPosition(0, 4), g.Selection.CurrentCell);
    }
}
