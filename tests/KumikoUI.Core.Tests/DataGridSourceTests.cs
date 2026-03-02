using System.Collections.ObjectModel;
using System.ComponentModel;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

// ── Test data models ──────────────────────────────────────────────────────────

internal class Person : INotifyPropertyChanged
{
    private string _name = "";
    private int _age;
    private string _department = "";

    public string Name
    {
        get => _name;
        set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
    }

    public int Age
    {
        get => _age;
        set { _age = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Age))); }
    }

    public string Department
    {
        get => _department;
        set { _department = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Department))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal class NestedItem
{
    public string Title { get; set; } = "";
    public Address Address { get; set; } = new();
}

internal class Address
{
    public string City { get; set; } = "";
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class DataGridSourceTests
{
    private static DataGridSource MakeSource(IEnumerable<Person> items, params DataGridColumn[] columns)
    {
        var source = new DataGridSource();
        source.SetColumns(columns.Length > 0
            ? columns
            : new[]
            {
                new DataGridColumn { PropertyName = "Name", Header = "Name", ColumnType = DataGridColumnType.Text },
                new DataGridColumn { PropertyName = "Age", Header = "Age", ColumnType = DataGridColumnType.Numeric },
                new DataGridColumn { PropertyName = "Department", Header = "Department", ColumnType = DataGridColumnType.Text }
            });
        source.SetItems(items.ToList());
        return source;
    }

    private static List<Person> SamplePeople() => new()
    {
        new Person { Name = "Alice", Age = 30, Department = "Engineering" },
        new Person { Name = "Bob", Age = 25, Department = "Marketing" },
        new Person { Name = "Charlie", Age = 35, Department = "Engineering" },
        new Person { Name = "Diana", Age = 28, Department = "Marketing" },
        new Person { Name = "Eve", Age = 40, Department = "Engineering" },
    };

    // ── Basic access ──────────────────────────────────────────────

    [Fact]
    public void RowCount_ReflectsTotalItems()
    {
        var source = MakeSource(SamplePeople());
        Assert.Equal(5, source.RowCount);
    }

    [Fact]
    public void GetItem_ValidIndex_ReturnsCorrectItem()
    {
        var people = SamplePeople();
        var source = MakeSource(people);
        var item = source.GetItem(0);
        Assert.IsType<Person>(item);
        Assert.Equal("Alice", ((Person)item).Name);
    }

    [Fact]
    public void GetItem_OutOfRange_Throws()
    {
        var source = MakeSource(SamplePeople());
        Assert.Throws<ArgumentOutOfRangeException>(() => source.GetItem(100));
    }

    [Fact]
    public void GetCellValue_ReturnsCorrectValue()
    {
        var source = MakeSource(SamplePeople());
        var col = source.Columns.First(c => c.PropertyName == "Name");
        var value = source.GetCellValue(0, col);
        Assert.Equal("Alice", value);
    }

    [Fact]
    public void GetCellDisplayText_WithFormat_FormatsCorrectly()
    {
        var source = new DataGridSource();
        source.SetColumns(new[]
        {
            new DataGridColumn { PropertyName = "Age", ColumnType = DataGridColumnType.Numeric, Format = "N2" }
        });
        source.SetItems(new List<Person> { new Person { Age = 30 } });
        var col = source.Columns[0];
        var text = source.GetCellDisplayText(0, col);
        Assert.Equal("30.00", text);
    }

    [Fact]
    public void GetCellDisplayText_NullValue_ReturnsEmpty()
    {
        var source = new DataGridSource();
        source.SetColumns(new[]
        {
            new DataGridColumn { PropertyName = "Name", ColumnType = DataGridColumnType.Text }
        });
        source.SetItems(new List<Person> { new Person { Name = "" } });
        var col = source.Columns[0];
        var text = source.GetCellDisplayText(0, col);
        Assert.Equal("", text);
    }

    // ── Sorting ───────────────────────────────────────────────────

    [Fact]
    public void ToggleSort_Ascending_SortsCorrectly()
    {
        var source = MakeSource(SamplePeople());
        var col = source.Columns.First(c => c.PropertyName == "Name");
        source.ToggleSort(col); // → Ascending
        var names = Enumerable.Range(0, source.RowCount)
            .Select(i => ((Person)source.GetItem(i)).Name)
            .ToList();
        Assert.Equal(names.OrderBy(n => n).ToList(), names);
    }

    [Fact]
    public void ToggleSort_SecondToggle_SortsDescending()
    {
        var source = MakeSource(SamplePeople());
        var col = source.Columns.First(c => c.PropertyName == "Name");
        source.ToggleSort(col); // Ascending
        source.ToggleSort(col); // Descending
        var names = Enumerable.Range(0, source.RowCount)
            .Select(i => ((Person)source.GetItem(i)).Name)
            .ToList();
        Assert.Equal(names.OrderByDescending(n => n).ToList(), names);
    }

    [Fact]
    public void ToggleSort_ThirdToggle_ClearsSorting()
    {
        var source = MakeSource(SamplePeople());
        var col = source.Columns.First(c => c.PropertyName == "Name");
        source.ToggleSort(col); // Ascending
        source.ToggleSort(col); // Descending
        source.ToggleSort(col); // None
        Assert.Equal(SortDirection.None, col.SortDirection);
    }

    [Fact]
    public void ToggleSort_ClearsOtherColumnsSortState()
    {
        var source = MakeSource(SamplePeople());
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var ageCol = source.Columns.First(c => c.PropertyName == "Age");
        source.ToggleSort(nameCol);
        source.ToggleSort(ageCol);
        Assert.Equal(SortDirection.None, nameCol.SortDirection);
        Assert.Equal(SortDirection.Ascending, ageCol.SortDirection);
    }

    [Fact]
    public void ToggleMultiSort_AddsToSortChain()
    {
        var source = MakeSource(SamplePeople());
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var ageCol = source.Columns.First(c => c.PropertyName == "Age");
        source.ToggleSort(nameCol);
        source.ToggleMultiSort(ageCol);
        Assert.Equal(2, source.ActiveSortColumns.Count);
    }

    [Fact]
    public void ToggleMultiSort_RemovesColumnFromChain()
    {
        var source = MakeSource(SamplePeople());
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var ageCol = source.Columns.First(c => c.PropertyName == "Age");
        source.ToggleSort(nameCol);
        source.ToggleMultiSort(ageCol);
        // Toggle ageCol again → Descending
        source.ToggleMultiSort(ageCol);
        // Toggle again → None, removes from chain
        source.ToggleMultiSort(ageCol);
        Assert.Single(source.ActiveSortColumns);
    }

    [Fact]
    public void ClearSort_ResetsAllSortState()
    {
        var source = MakeSource(SamplePeople());
        var col = source.Columns.First(c => c.PropertyName == "Name");
        source.ToggleSort(col);
        source.ClearSort();
        Assert.Equal(SortDirection.None, col.SortDirection);
        Assert.Empty(source.ActiveSortColumns);
    }

    [Fact]
    public void SortColumnsChanging_CanBeCancelled()
    {
        var source = MakeSource(SamplePeople());
        var col = source.Columns.First(c => c.PropertyName == "Name");
        source.SortColumnsChanging += (_, e) => e.Cancel = true;
        source.ToggleSort(col);
        Assert.Equal(SortDirection.None, col.SortDirection);
    }

    [Fact]
    public void SortColumnsChanged_RaisedAfterSort()
    {
        var source = MakeSource(SamplePeople());
        var col = source.Columns.First(c => c.PropertyName == "Name");
        SortColumnsChangedEventArgs? eventArgs = null;
        source.SortColumnsChanged += (_, e) => eventArgs = e;
        source.ToggleSort(col);
        Assert.NotNull(eventArgs);
        Assert.Same(col, eventArgs!.Column);
    }

    // ── Filtering ─────────────────────────────────────────────────

    [Fact]
    public void SetFilter_GlobalPredicate_FiltersRows()
    {
        var source = MakeSource(SamplePeople());
        source.SetFilter(item => ((Person)item).Age > 30);
        Assert.Equal(2, source.RowCount); // Charlie (35) and Eve (40)
    }

    [Fact]
    public void SetFilter_Null_ClearsFilter()
    {
        var source = MakeSource(SamplePeople());
        source.SetFilter(item => ((Person)item).Age > 30);
        source.SetFilter(null);
        Assert.Equal(5, source.RowCount);
    }

    [Fact]
    public void SetColumnFilter_TextContains_FiltersRows()
    {
        var source = MakeSource(SamplePeople());
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        // "ice" uniquely appears in "Alice" but not in any other sample name
        var filter = new FilterDescription(nameCol)
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "ice" }
        };
        source.SetColumnFilter(nameCol, filter);
        Assert.Equal(1, source.RowCount); // Alice only
        Assert.Equal("Alice", ((Person)source.GetItem(0)).Name);
    }

    [Fact]
    public void SetColumnFilter_Null_ClearsFilter()
    {
        var source = MakeSource(SamplePeople());
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var filter = new FilterDescription(nameCol)
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "li" }
        };
        source.SetColumnFilter(nameCol, filter);
        source.SetColumnFilter(nameCol, null);
        Assert.Equal(5, source.RowCount);
    }

    [Fact]
    public void ClearAllFilters_RemovesAllFilters()
    {
        var source = MakeSource(SamplePeople());
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        source.SetColumnFilter(nameCol, new FilterDescription(nameCol)
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "Alice" }
        });
        source.ClearAllFilters();
        Assert.Equal(5, source.RowCount);
        Assert.Empty(source.ActiveFilters);
    }

    [Fact]
    public void FilterChanging_CanBeCancelled()
    {
        var source = MakeSource(SamplePeople());
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        source.FilterChanging += (_, e) => e.Cancel = true;
        source.SetColumnFilter(nameCol, new FilterDescription(nameCol)
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "Alice" }
        });
        Assert.Equal(5, source.RowCount); // Filter was cancelled
    }

    [Fact]
    public void FilterChanged_RaisedAfterFilter()
    {
        var source = MakeSource(SamplePeople());
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        FilterChangedEventArgs? eventArgs = null;
        source.FilterChanged += (_, e) => eventArgs = e;
        source.SetColumnFilter(nameCol, new FilterDescription(nameCol)
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "Alice" }
        });
        Assert.NotNull(eventArgs);
        Assert.Same(nameCol, eventArgs!.Column);
    }

    [Fact]
    public void GetUniqueColumnValues_ReturnsDistinctSortedValues()
    {
        var source = MakeSource(SamplePeople());
        var deptCol = source.Columns.First(c => c.PropertyName == "Department");
        var values = source.GetUniqueColumnValues(deptCol);
        Assert.Equal(2, values.Count);
        Assert.Contains("Engineering", values);
        Assert.Contains("Marketing", values);
        // Sorted alphabetically
        Assert.Equal("Engineering", values[0]);
        Assert.Equal("Marketing", values[1]);
    }

    [Fact]
    public void ActiveFilters_ReflectsActiveColumnFilters()
    {
        var source = MakeSource(SamplePeople());
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        Assert.Empty(source.ActiveFilters);

        source.SetColumnFilter(nameCol, new FilterDescription(nameCol)
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "A" }
        });
        Assert.NotEmpty(source.ActiveFilters);
        Assert.Single(source.ActiveFilters);
    }

    // ── Column reorder ────────────────────────────────────────────

    [Fact]
    public void ReorderColumn_ChangesColumnOrder()
    {
        var source = MakeSource(SamplePeople());
        var firstCol = source.Columns[0].PropertyName;
        var secondCol = source.Columns[1].PropertyName;
        source.ReorderColumn(0, 1);
        Assert.Equal(secondCol, source.Columns[0].PropertyName);
        Assert.Equal(firstCol, source.Columns[1].PropertyName);
    }

    [Fact]
    public void ReorderColumn_SameIndex_DoesNothing()
    {
        var source = MakeSource(SamplePeople());
        var originalOrder = source.Columns.Select(c => c.PropertyName).ToList();
        source.ReorderColumn(1, 1);
        var newOrder = source.Columns.Select(c => c.PropertyName).ToList();
        Assert.Equal(originalOrder, newOrder);
    }

    [Fact]
    public void ReorderColumn_OutOfRange_DoesNotThrow()
    {
        var source = MakeSource(SamplePeople());
        var ex = Record.Exception(() => source.ReorderColumn(100, 0));
        Assert.Null(ex);
    }

    // ── SetCellValue ──────────────────────────────────────────────

    [Fact]
    public void SetCellValue_UpdatesItemProperty()
    {
        var people = SamplePeople();
        var source = MakeSource(people);
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        source.SetCellValue(0, nameCol, "Updated");
        Assert.Equal("Updated", people[0].Name);
    }

    // ── Grouping ──────────────────────────────────────────────────

    [Fact]
    public void AddGroupDescription_ActivatesGrouping()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        Assert.True(source.IsGroupingActive);
    }

    [Fact]
    public void AddGroupDescription_RowCountIncludesGroupHeaders()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        // 2 groups (Engineering, Marketing) + 5 data rows = 7 rows
        Assert.Equal(7, source.RowCount);
    }

    [Fact]
    public void IsGroupHeaderRow_ReturnsTrueForGroupHeaders()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        // First row should be a group header
        Assert.True(source.IsGroupHeaderRow(0));
    }

    [Fact]
    public void IsGroupHeaderRow_ReturnsFalseForDataRows()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        // Second row (after first group header) should be a data row
        Assert.False(source.IsGroupHeaderRow(1));
    }

    [Fact]
    public void GetGroupHeaderInfo_ReturnsInfoForGroupHeader()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        var info = source.GetGroupHeaderInfo(0);
        Assert.NotNull(info);
        Assert.Equal(0, info!.Level);
    }

    [Fact]
    public void GetItem_GroupHeaderRow_Throws()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        Assert.Throws<InvalidOperationException>(() => source.GetItem(0));
    }

    [Fact]
    public void ToggleGroupExpansion_CollapsesGroup()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        int rowCountBefore = source.RowCount;
        source.ToggleGroupExpansion(0); // Collapse first group
        int rowCountAfter = source.RowCount;
        Assert.True(rowCountAfter < rowCountBefore);
    }

    [Fact]
    public void ToggleGroupExpansion_ExpandsCollapsedGroup()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        source.ToggleGroupExpansion(0); // Collapse
        int collapsed = source.RowCount;
        source.ToggleGroupExpansion(0); // Expand
        Assert.True(source.RowCount > collapsed);
    }

    [Fact]
    public void CollapseAllGroups_CollapsesAllGroups()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        source.CollapseAllGroups();
        // Only 2 group headers remain
        Assert.Equal(2, source.RowCount);
    }

    [Fact]
    public void ExpandAllGroups_ExpandsAllGroups()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        source.CollapseAllGroups();
        source.ExpandAllGroups();
        Assert.Equal(7, source.RowCount); // 2 headers + 5 data
    }

    [Fact]
    public void RemoveGroupDescription_ClearsGrouping()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        source.RemoveGroupDescription("Department");
        Assert.False(source.IsGroupingActive);
        Assert.Equal(5, source.RowCount);
    }

    [Fact]
    public void ClearGroupDescriptions_ClearsAllGrouping()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        source.ClearGroupDescriptions();
        Assert.False(source.IsGroupingActive);
    }

    [Fact]
    public void GroupExpanding_CanBeCancelled()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        int rowsBefore = source.RowCount;
        source.GroupExpanding += (_, e) => e.Cancel = true;
        source.ToggleGroupExpansion(0); // should be cancelled
        Assert.Equal(rowsBefore, source.RowCount);
    }

    [Fact]
    public void GroupCollapsed_RaisedAfterToggle()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        GroupCollapsedEventArgs? eventArgs = null;
        source.GroupCollapsed += (_, e) => eventArgs = e;
        source.ToggleGroupExpansion(0);
        Assert.NotNull(eventArgs);
    }

    [Fact]
    public void FrozenRowCount_WithGrouping_ReturnsZero()
    {
        var source = MakeSource(SamplePeople());
        source.FrozenRowCount = 2;
        source.AddGroupDescription(new GroupDescription("Department"));
        Assert.Equal(0, source.FrozenRowCount);
    }

    [Fact]
    public void FrozenRowCount_WithoutGrouping_ReturnsSetValue()
    {
        var source = MakeSource(SamplePeople());
        source.FrozenRowCount = 2;
        Assert.Equal(2, source.FrozenRowCount);
    }

    [Fact]
    public void EffectiveFrozenRowCount_ClampsToRowCount()
    {
        var source = MakeSource(SamplePeople());
        source.FrozenRowCount = 100;
        Assert.Equal(source.RowCount, source.EffectiveFrozenRowCount);
    }

    // ── Summary ───────────────────────────────────────────────────

    [Fact]
    public void AddTableSummaryRow_Sum_ComputesCorrectly()
    {
        var source = MakeSource(SamplePeople());
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        });
        // 30 + 25 + 35 + 28 + 40 = 158
        var row = source.ComputedBottomSummaries[0];
        Assert.True(row.RawValues.ContainsKey("Age"));
        Assert.Equal(158.0, (double)row.RawValues["Age"]!);
    }

    [Fact]
    public void AddTableSummaryRow_Average_ComputesCorrectly()
    {
        var source = MakeSource(SamplePeople());
        source.AddTableSummaryRow(new TableSummaryRow("Avg", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Average) }
        });
        var row = source.ComputedBottomSummaries[0];
        Assert.Equal(31.6, (double)row.RawValues["Age"]!, 5);
    }

    [Fact]
    public void AddTableSummaryRow_Count_CountsRows()
    {
        var source = MakeSource(SamplePeople());
        source.AddTableSummaryRow(new TableSummaryRow("Count", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Name", SummaryType.Count) }
        });
        var row = source.ComputedBottomSummaries[0];
        Assert.Equal(5, (int)row.RawValues["Name"]!);
    }

    [Fact]
    public void AddTableSummaryRow_Min_FindsMinimum()
    {
        var source = MakeSource(SamplePeople());
        source.AddTableSummaryRow(new TableSummaryRow("Min", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Min) }
        });
        var row = source.ComputedBottomSummaries[0];
        Assert.Equal(25, (int)(row.RawValues["Age"] as IComparable)!);
    }

    [Fact]
    public void AddTableSummaryRow_Max_FindsMaximum()
    {
        var source = MakeSource(SamplePeople());
        source.AddTableSummaryRow(new TableSummaryRow("Max", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Max) }
        });
        var row = source.ComputedBottomSummaries[0];
        Assert.Equal(40, (int)(row.RawValues["Age"] as IComparable)!);
    }

    [Fact]
    public void AddTableSummaryRow_Custom_UsesCustomFunction()
    {
        var source = MakeSource(SamplePeople());
        source.AddTableSummaryRow(new TableSummaryRow("Custom", SummaryPosition.Bottom)
        {
            Columns =
            {
                new SummaryColumnDescription("Age", SummaryType.Custom)
                {
                    CustomAggregate = values => values.Count(v => v != null)
                }
            }
        });
        var row = source.ComputedBottomSummaries[0];
        Assert.Equal(5, (int)row.RawValues["Age"]!);
    }

    [Fact]
    public void AddTableSummaryRow_Top_AppearsInTopSummaries()
    {
        var source = MakeSource(SamplePeople());
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Top)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        });
        Assert.Equal(1, source.TopSummaryCount);
        Assert.Equal(0, source.BottomSummaryCount);
    }

    [Fact]
    public void RemoveTableSummaryRow_RemovesRow()
    {
        var source = MakeSource(SamplePeople());
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        });
        source.RemoveTableSummaryRow("Total");
        Assert.Equal(0, source.BottomSummaryCount);
    }

    [Fact]
    public void ClearTableSummaryRows_RemovesAllRows()
    {
        var source = MakeSource(SamplePeople());
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        });
        source.ClearTableSummaryRows();
        Assert.Equal(0, source.BottomSummaryCount);
    }

    [Fact]
    public void Summary_WithFilter_OnlyCountsVisibleRows()
    {
        var source = MakeSource(SamplePeople());
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        });
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        source.SetFilter(item => ((Person)item).Department == "Engineering");
        // Engineering: Alice(30), Charlie(35), Eve(40) = 105
        var row = source.ComputedBottomSummaries[0];
        Assert.Equal(105.0, (double)row.RawValues["Age"]!);
    }

    [Fact]
    public void GroupSummaryRow_ComputesForEachGroup()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        source.AddGroupSummaryRow(new GroupSummaryRow("Total")
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        });
        Assert.True(source.HasGroupSummaries);
    }

    // ── INCC support ──────────────────────────────────────────────

    [Fact]
    public void SetItems_ObservableCollection_SubscribesToChanges()
    {
        var people = new ObservableCollection<Person>(SamplePeople());
        var source = new DataGridSource();
        source.SetColumns(new[]
        {
            new DataGridColumn { PropertyName = "Name", ColumnType = DataGridColumnType.Text },
            new DataGridColumn { PropertyName = "Age", ColumnType = DataGridColumnType.Numeric },
            new DataGridColumn { PropertyName = "Department", ColumnType = DataGridColumnType.Text }
        });
        source.SetItems(people);
        int dataChangedCount = 0;
        source.DataChanged += () => dataChangedCount++;
        people.Add(new Person { Name = "Frank", Age = 22, Department = "HR" });
        Assert.True(dataChangedCount > 0);
        Assert.Equal(6, source.RowCount);
    }

    [Fact]
    public void SetItems_ObservableCollection_RemoveItem_UpdatesRowCount()
    {
        var people = new ObservableCollection<Person>(SamplePeople());
        var source = new DataGridSource();
        source.SetColumns(new[]
        {
            new DataGridColumn { PropertyName = "Name", ColumnType = DataGridColumnType.Text },
            new DataGridColumn { PropertyName = "Age", ColumnType = DataGridColumnType.Numeric },
            new DataGridColumn { PropertyName = "Department", ColumnType = DataGridColumnType.Text }
        });
        source.SetItems(people);
        people.RemoveAt(0);
        Assert.Equal(4, source.RowCount);
    }

    [Fact]
    public void SetItems_PropertyChanged_TriggersDataChanged()
    {
        var people = SamplePeople();
        var source = MakeSource(people);
        int dataChangedCount = 0;
        source.DataChanged += () => dataChangedCount++;
        people[0].Name = "AliceUpdated"; // fires PropertyChanged
        Assert.True(dataChangedCount > 0);
    }

    [Fact]
    public void SetItems_ReplacingSource_UnsubscribesFromOldCollection()
    {
        var old = new ObservableCollection<Person>(SamplePeople());
        var source = MakeSource(old);
        var newPeople = new List<Person>
        {
            new Person { Name = "NewPerson", Age = 99, Department = "IT" }
        };
        source.SetItems(newPeople);
        int eventCount = 0;
        source.DataChanged += () => eventCount++;
        old.Add(new Person { Name = "Ghost", Age = 1, Department = "X" });
        Assert.Equal(0, eventCount); // old collection no longer wired
        Assert.Equal(1, source.RowCount); // new source only has 1 item
    }

    // ── Nested property access ────────────────────────────────────

    [Fact]
    public void GetCellValue_NestedProperty_ReturnsCorrectValue()
    {
        var items = new List<NestedItem>
        {
            new NestedItem { Title = "HQ", Address = new Address { City = "Seattle" } }
        };
        var source = new DataGridSource();
        source.SetColumns(new[]
        {
            new DataGridColumn { PropertyName = "Address.City", ColumnType = DataGridColumnType.Text }
        });
        source.SetItems(items);
        var col = source.Columns[0];
        Assert.Equal("Seattle", source.GetCellValue(0, col));
    }

    [Fact]
    public void GetCellValue_UnknownProperty_ReturnsNull()
    {
        var source = new DataGridSource();
        source.SetColumns(new[]
        {
            new DataGridColumn { PropertyName = "NonExistentProp", ColumnType = DataGridColumnType.Text }
        });
        source.SetItems(new List<Person> { new Person { Name = "Test" } });
        var col = source.Columns[0];
        Assert.Null(source.GetCellValue(0, col));
    }

    // ── Row reorder ───────────────────────────────────────────────

    [Fact]
    public void ReorderRow_MovesItemInUnderlying()
    {
        var people = SamplePeople().ToList();
        var source = MakeSource(people);
        string firstName = ((Person)source.GetItem(0)).Name;
        string secondName = ((Person)source.GetItem(1)).Name;
        source.ReorderRow(0, 1);
        Assert.Equal(secondName, ((Person)source.GetItem(0)).Name);
        Assert.Equal(firstName, ((Person)source.GetItem(1)).Name);
    }

    [Fact]
    public void ReorderRow_SameIndex_DoesNothing()
    {
        var people = SamplePeople().ToList();
        var source = MakeSource(people);
        string firstName = ((Person)source.GetItem(0)).Name;
        source.ReorderRow(0, 0);
        Assert.Equal(firstName, ((Person)source.GetItem(0)).Name);
    }

    [Fact]
    public void ReorderRow_WithGrouping_DoesNothing()
    {
        var source = MakeSource(SamplePeople());
        source.AddGroupDescription(new GroupDescription("Department"));
        int rowsBefore = source.RowCount;
        var ex = Record.Exception(() => source.ReorderRow(0, 1));
        Assert.Null(ex);
        Assert.Equal(rowsBefore, source.RowCount);
    }

    // ── Columns ───────────────────────────────────────────────────

    [Fact]
    public void SetColumns_ReplacesExistingColumns()
    {
        var source = MakeSource(SamplePeople());
        source.SetColumns(new[] { new DataGridColumn { PropertyName = "Name" } });
        Assert.Single(source.Columns);
    }

    [Fact]
    public void IsMultiSortActive_ReturnsTrueWhenMultipleColumnsSorted()
    {
        var source = MakeSource(SamplePeople());
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var ageCol = source.Columns.First(c => c.PropertyName == "Age");
        source.ToggleSort(nameCol);
        source.ToggleMultiSort(ageCol);
        Assert.True(source.IsMultiSortActive);
    }

    // ── FormatCellValue static ────────────────────────────────────

    [Fact]
    public void FormatCellValue_NullValue_ReturnsEmpty()
    {
        var col = new DataGridColumn();
        Assert.Equal("", DataGridSource.FormatCellValue(null, col));
    }

    [Fact]
    public void FormatCellValue_WithFormat_FormatsValue()
    {
        var col = new DataGridColumn { Format = "N2" };
        Assert.Equal("3.14", DataGridSource.FormatCellValue(3.14159265, col));
    }

    [Fact]
    public void FormatCellValue_NoFormat_UsestoString()
    {
        var col = new DataGridColumn();
        Assert.Equal("42", DataGridSource.FormatCellValue(42, col));
    }

    // ── Refresh ───────────────────────────────────────────────────

    [Fact]
    public void Refresh_DoesNotThrow()
    {
        var source = MakeSource(SamplePeople());
        var ex = Record.Exception(() => source.Refresh());
        Assert.Null(ex);
    }
}
