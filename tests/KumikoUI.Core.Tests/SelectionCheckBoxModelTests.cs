using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>The check box path of the selection model (#321): check-all state, range changes and box toggles.</summary>
public class SelectionCheckBoxModelTests
{
    private sealed class Item
    {
        public string Name { get; set; } = "";
        public string Group { get; set; } = "";
    }

    private static (KumikoSelectionModel Model, KumikoSource Source, List<Item> Items) New(
        KumikoSelectionMode mode = KumikoSelectionMode.Extended, int rows = 10)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoSelectionColumn(),
            new KumikoColumn { PropertyName = "Name" },
            new KumikoColumn { PropertyName = "Group" },
        });
        var items = Enumerable.Range(0, rows).Select(i => new Item { Name = $"N{i}", Group = i % 2 == 0 ? "A" : "B" }).ToList();
        source.SetItems(items);
        return (new KumikoSelectionModel(source) { Mode = mode }, source, items);
    }

    private static int CountChanges(KumikoSelectionModel model, Action act)
    {
        int count = 0;
        model.SelectionChanged += (_, _) => count++;
        act();
        return count;
    }

    // ── DataRowCount ──

    [Fact]
    public void DataRowCount_WithoutGrouping_IsTheRowCount()
    {
        var (_, source, _) = New(rows: 7);

        Assert.Equal(7, source.DataRowCount);
        Assert.Equal(source.RowCount, source.DataRowCount);
    }

    [Fact]
    public void DataRowCount_WithGrouping_LeavesOutGroupHeaders()
    {
        var (_, source, _) = New(rows: 10);

        source.AddGroupDescription(new GroupDescription("Group"));

        Assert.Equal(12, source.RowCount); // 10 items and 2 group headers
        Assert.Equal(10, source.DataRowCount);
    }

    [Fact]
    public void DataRowCount_FollowsCollapsedGroupsAndFilters()
    {
        var (_, source, _) = New(rows: 10);
        source.AddGroupDescription(new GroupDescription("Group"));

        source.ToggleGroupExpansion(0); // collapse the first group (5 rows)
        Assert.Equal(5, source.DataRowCount);

        source.ToggleGroupExpansion(0);
        source.SetFilter(o => ((Item)o).Name is "N0" or "N1" or "N2");
        Assert.Equal(3, source.DataRowCount);
    }

    [Fact]
    public void DataRowCount_IsZeroForNoItems()
    {
        var (_, source, _) = New(rows: 0);

        Assert.Equal(0, source.DataRowCount);
    }

    // ── CheckAllState ──

    [Fact]
    public void CheckAllState_IsFalseNullTrue_AsRowsAreSelected()
    {
        var (model, _, _) = New(rows: 4);
        Assert.False(model.CheckAllState);

        model.HandleCheckBoxToggle(1, shift: false);
        Assert.Null(model.CheckAllState);

        model.SelectAll();
        Assert.True(model.CheckAllState);

        model.HandleCheckBoxToggle(2, shift: false);
        Assert.Null(model.CheckAllState);
    }

    [Fact]
    public void CheckAllState_CoversTheViewAfterAFilter()
    {
        var (model, source, _) = New(rows: 6);
        source.SetFilter(o => ((Item)o).Group == "A"); // 3 rows

        model.SelectAll();

        Assert.True(model.CheckAllState);
        Assert.Equal(3, model.SelectedItems.Count);
    }

    [Fact]
    public void CheckAllState_IsFalseWithoutRows()
    {
        var (model, _, _) = New(rows: 0);

        Assert.False(model.CheckAllState);
    }

    [Fact]
    public void CheckAllState_NoneMode_IsFalse()
    {
        var (model, _, _) = New(KumikoSelectionMode.None);
        model.SelectAll();

        Assert.False(model.CheckAllState);
    }

    // ── SetRangeSelected ──

    [Fact]
    public void SetRangeSelected_SelectsTheRange_WithOneChange()
    {
        var (model, _, _) = New();

        int changes = CountChanges(model, () => model.SetRangeSelected(2, 5, true));

        Assert.Equal(1, changes);
        Assert.Equal(new[] { 2, 3, 4, 5 }, model.SelectedRows.Order());
    }

    [Fact]
    public void SetRangeSelected_AcceptsEitherOrder_AndDeselects()
    {
        var (model, _, _) = New();
        model.SelectAll();

        int changes = CountChanges(model, () => model.SetRangeSelected(6, 3, false));

        Assert.Equal(1, changes);
        Assert.Equal(new[] { 0, 1, 2, 7, 8, 9 }, model.SelectedRows.Order());
    }

    [Fact]
    public void SetRangeSelected_SkipsGroupRows_AndClampsToTheView()
    {
        var (model, source, _) = New(rows: 10);
        source.AddGroupDescription(new GroupDescription("Group"));

        model.SetRangeSelected(-5, 500, true);

        Assert.Equal(10, model.SelectedItems.Count);
        Assert.True(model.CheckAllState);
        Assert.DoesNotContain(0, model.SelectedRows); // row 0 is a group header
    }

    [Fact]
    public void SetRangeSelected_WithNothingToChange_RaisesNoChange()
    {
        var (model, _, _) = New();

        Assert.Equal(0, CountChanges(model, () => model.SetRangeSelected(1, 3, false)));
    }

    [Fact]
    public void SetRangeSelected_NoneMode_DoesNothing()
    {
        var (model, _, _) = New(KumikoSelectionMode.None);

        model.SetRangeSelected(0, 4, true);

        Assert.Empty(model.SelectedItems);
    }

    [Fact]
    public void SetRangeSelected_SingleMode_SelectsOnlyTheLastRow()
    {
        var (model, _, _) = New(KumikoSelectionMode.Single);

        model.SetRangeSelected(1, 4, true);

        Assert.Equal(new[] { 4 }, model.SelectedRows);
    }

    // ── HandleCheckBoxToggle ──

    [Theory]
    [InlineData(KumikoSelectionMode.Multiple)]
    [InlineData(KumikoSelectionMode.Extended)]
    public void HandleCheckBoxToggle_Multi_TogglesTheRow_AndKeepsTheOthers(KumikoSelectionMode mode)
    {
        var (model, _, _) = New(mode);

        model.HandleCheckBoxToggle(1, false);
        model.HandleCheckBoxToggle(4, false);
        Assert.Equal(new[] { 1, 4 }, model.SelectedRows.Order());

        model.HandleCheckBoxToggle(1, false);
        Assert.Equal(new[] { 4 }, model.SelectedRows);
    }

    [Fact]
    public void HandleCheckBoxToggle_Extended_DoesNotApplyThePlainClickRule()
    {
        var (model, _, _) = New(KumikoSelectionMode.Extended);
        model.HandleRowSelection(0, false, false);

        model.HandleCheckBoxToggle(3, false);

        Assert.Equal(new[] { 0, 3 }, model.SelectedRows.Order()); // a row click would have replaced row 0
    }

    [Fact]
    public void HandleCheckBoxToggle_Single_ReplacesTheSelection_AndUncheckClears()
    {
        var (model, _, _) = New(KumikoSelectionMode.Single);

        model.HandleCheckBoxToggle(2, false);
        model.HandleCheckBoxToggle(5, false);
        Assert.Equal(new[] { 5 }, model.SelectedRows);

        model.HandleCheckBoxToggle(5, false);
        Assert.Empty(model.SelectedRows);
    }

    [Fact]
    public void HandleCheckBoxToggle_NoneMode_DoesNothing()
    {
        var (model, _, _) = New(KumikoSelectionMode.None);

        model.HandleCheckBoxToggle(1, false);

        Assert.Empty(model.SelectedItems);
        Assert.False(model.CurrentCell.IsValid);
    }

    [Fact]
    public void HandleCheckBoxToggle_ARangeWithShift_AppliesTheNewStateFromTheAnchor()
    {
        var (model, _, _) = New(KumikoSelectionMode.Extended);
        model.HandleCheckBoxToggle(2, false); // anchor 2

        model.HandleCheckBoxToggle(6, shift: true); // checks 2..6
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, model.SelectedRows.Order());

        model.HandleCheckBoxToggle(4, shift: true); // the anchor moved to 6; unchecks 6..4
        Assert.Equal(new[] { 2, 3 }, model.SelectedRows.Order());
    }

    [Fact]
    public void HandleCheckBoxToggle_ShiftWithoutAnAnchor_TogglesOneRow()
    {
        var (model, _, _) = New();

        model.HandleCheckBoxToggle(4, shift: true);

        Assert.Equal(new[] { 4 }, model.SelectedRows);
    }

    [Fact]
    public void HandleCheckBoxToggle_ShiftRange_SkipsGroupRows()
    {
        var (model, source, _) = New(rows: 10);
        source.AddGroupDescription(new GroupDescription("Group"));
        int first = Enumerable.Range(0, source.RowCount).First(r => !source.IsNonDataRow(r));
        model.HandleCheckBoxToggle(first, false);

        model.HandleCheckBoxToggle(source.RowCount - 1, shift: true);

        Assert.Equal(10, model.SelectedItems.Count);
    }

    [Fact]
    public void HandleCheckBoxToggle_IgnoresAGroupRow()
    {
        var (model, source, _) = New(rows: 10);
        source.AddGroupDescription(new GroupDescription("Group"));

        model.HandleCheckBoxToggle(0, false);

        Assert.Empty(model.SelectedItems);
    }

    [Fact]
    public void HandleCheckBoxToggle_RaisesOneChange_AndMovesTheCurrentCellToTheRow()
    {
        var (model, _, items) = New();
        model.SetCurrentCell(0, 2);
        KumikoSelectionChangedEventArgs? args = null;
        model.SelectionChanged += (_, e) => args = e;

        model.HandleCheckBoxToggle(3, false);

        Assert.NotNull(args);
        Assert.Same(items[3], Assert.Single(args!.AddedItems));
        Assert.Equal(new CellPosition(3, 2), model.CurrentCell); // the column is kept
        Assert.Equal(new CellPosition(3, 2), args.CurrentCell);
    }

    [Fact]
    public void HandleCheckBoxToggle_ShiftRange_RaisesOneChange()
    {
        var (model, _, _) = New();
        model.HandleCheckBoxToggle(1, false);

        Assert.Equal(1, CountChanges(model, () => model.HandleCheckBoxToggle(7, shift: true)));
    }

    // ── SelectOnRowClick ──

    [Fact]
    public void SelectOnRowClick_DefaultsToTrue()
    {
        Assert.True(New().Model.SelectOnRowClick);
    }

    [Theory]
    [InlineData(KumikoSelectionMode.Single)]
    [InlineData(KumikoSelectionMode.Multiple)]
    [InlineData(KumikoSelectionMode.Extended)]
    public void SelectOnRowClick_False_ARowTapOnlyMovesTheCurrentCell(KumikoSelectionMode mode)
    {
        var (model, _, _) = New(mode);
        model.SelectOnRowClick = false;

        model.HandleRowSelection(3, false, false);
        model.HandleRowSelection(5, true, true);

        Assert.Empty(model.SelectedItems);
        Assert.Equal(5, model.CurrentCell.Row);
    }

    [Fact]
    public void SelectOnRowClick_False_TheCheckBoxStillSelects()
    {
        var (model, _, _) = New();
        model.SelectOnRowClick = false;

        model.HandleCheckBoxToggle(2, false);

        Assert.Equal(new[] { 2 }, model.SelectedRows);
    }

    [Fact]
    public void SelectOnRowClick_False_KeyboardNavigationStillSelects()
    {
        var (model, _, _) = New();
        model.SelectOnRowClick = false;
        model.SetCurrentCell(2, 1);

        model.NavigateDown(10, false);

        Assert.Equal(new[] { 3 }, model.SelectedRows);
    }

    // ── Check-all toggle ──

    [Fact]
    public void ToggleCheckAll_SelectsAll_WhenNoneOrSome_AndClearsWhenAll()
    {
        var (model, _, _) = New(rows: 5);
        model.HandleCheckBoxToggle(2, false);
        model.SetCurrentCell(2, 1);

        Assert.True(model.ToggleCheckAll()); // some -> all
        Assert.True(model.CheckAllState);

        Assert.True(model.ToggleCheckAll()); // all -> none
        Assert.False(model.CheckAllState);
        Assert.Equal(new CellPosition(2, 1), model.CurrentCell); // the current cell stays

        Assert.True(model.ToggleCheckAll()); // none -> all
        Assert.True(model.CheckAllState);
    }

    [Theory]
    [InlineData(KumikoSelectionMode.Single)]
    [InlineData(KumikoSelectionMode.None)]
    public void ToggleCheckAll_HasNoCheckAllInSingleAndNone(KumikoSelectionMode mode)
    {
        var (model, _, _) = New(mode);

        Assert.False(model.HasCheckAll);
        Assert.False(model.ToggleCheckAll());
        Assert.Empty(model.SelectedItems);
    }

    // ── The column ──

    [Fact]
    public void SelectionColumn_HasTheDocumentedDefaults()
    {
        var column = new KumikoSelectionColumn();

        Assert.Equal(KumikoColumnType.Selection, column.ColumnType);
        Assert.Equal(44f, column.Width);
        Assert.Equal(44f, column.MinWidth);
        Assert.Equal(44f, column.MaxWidth);
        Assert.Equal(ColumnSizeMode.Fixed, column.SizeMode);
        Assert.Equal(ColumnFreezeMode.Left, column.FreezeMode);
        Assert.True(column.IsReadOnly);
        Assert.False(column.AllowSorting);
        Assert.False(column.AllowFiltering);
        Assert.False(column.AllowResize);
        Assert.False(column.AllowReorder);
        Assert.False(column.AllowTabStop);
        Assert.False(column.AllowSearching);
        Assert.Equal(KumikoUI.Core.Rendering.KumikoTextAlignment.Center, column.TextAlignment);
        Assert.Equal(string.Empty, column.PropertyName);
        Assert.Equal(string.Empty, column.Header);
    }

    [Fact]
    public void SelectionColumn_IsLeftOutOfSavedLayouts()
    {
        var (_, source, _) = New();

        var layout = KumikoLayoutState.Capture(source);

        Assert.DoesNotContain(layout.Columns!, c => string.IsNullOrEmpty(c.Key));
        Assert.Equal(2, layout.Columns!.Count);
    }

    // ── Strings and announcements ──

    [Fact]
    public void Announcements_UseTheStrings()
    {
        var strings = KumikoStrings.Default;

        Assert.Equal("All 120 rows selected", KumikoAnnouncements.CheckAllToggled(strings, true, 120, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("Selection cleared", KumikoAnnouncements.CheckAllToggled(strings, false, 0));
        Assert.Equal("Select row, checked", KumikoAnnouncements.SelectionBox(strings, true));
        Assert.Equal("Select row, not checked", KumikoAnnouncements.SelectionBox(strings, false));
        Assert.Equal("Select all rows, checked", KumikoAnnouncements.CheckAllBox(strings, true));
        Assert.Equal("Select all rows, not checked", KumikoAnnouncements.CheckAllBox(strings, false));
        Assert.Equal("Select all rows, partially checked", KumikoAnnouncements.CheckAllBox(strings, null));
    }

    [Fact]
    public void Announcements_FollowALocalizedTable()
    {
        var strings = KumikoStrings.Default with { SelectRow = "Zeile auswahlen", CheckStateChecked = "ausgewahlt" };

        Assert.Equal("Zeile auswahlen, ausgewahlt", KumikoAnnouncements.SelectionBox(strings, true));
    }
}
