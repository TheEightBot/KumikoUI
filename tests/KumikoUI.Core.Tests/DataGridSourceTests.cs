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






}
    }
        Assert.Null(ex);
        var ex = Record.Exception(() => source.Refresh());
        var source = MakeSource(SamplePeople());
    {
    public void Refresh_DoesNotThrow()
    [Fact]

    // ── Refresh ───────────────────────────────────────────────────

    }
        Assert.Equal("42", DataGridSource.FormatCellValue(42, col));
        var col = new DataGridColumn();
    {
    public void FormatCellValue_NoFormat_UsestoString()
    [Fact]

    }
        Assert.Equal("3.14", DataGridSource.FormatCellValue(3.14159265, col));
        var col = new DataGridColumn { Format = "N2" };
    {
    public void FormatCellValue_WithFormat_FormatsValue()
    [Fact]

    }
        Assert.Equal("", DataGridSource.FormatCellValue(null, col));
        var col = new DataGridColumn();
    {
    public void FormatCellValue_NullValue_ReturnsEmpty()
    [Fact]

    // ── FormatCellValue static ────────────────────────────────────

    }
        Assert.True(source.IsMultiSortActive);
        source.ToggleMultiSort(ageCol);
        source.ToggleSort(nameCol);
        var ageCol = source.Columns.First(c => c.PropertyName == "Age");
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var source = MakeSource(SamplePeople());
    {
    public void IsMultiSortActive_ReturnsTrueWhenMultipleColumnsSorted()
    [Fact]

    }
        Assert.Single(source.Columns);
        source.SetColumns(new[] { new DataGridColumn { PropertyName = "Name" } });
        var source = MakeSource(SamplePeople());
    {
    public void SetColumns_ReplacesExistingColumns()
    [Fact]

    // ── Columns ───────────────────────────────────────────────────

    }
        Assert.Equal(rowsBefore, source.RowCount);
        Assert.Null(ex);
        var ex = Record.Exception(() => source.ReorderRow(0, 1));
        int rowsBefore = source.RowCount;
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void ReorderRow_WithGrouping_DoesNothing()
    [Fact]

    }
        Assert.Equal(firstName, ((Person)source.GetItem(0)).Name);
        source.ReorderRow(0, 0);
        string firstName = ((Person)source.GetItem(0)).Name;
        var source = MakeSource(people);
        var people = SamplePeople().ToList();
    {
    public void ReorderRow_SameIndex_DoesNothing()
    [Fact]

    }
        Assert.Equal(firstName, ((Person)source.GetItem(1)).Name);
        Assert.Equal(secondName, ((Person)source.GetItem(0)).Name);
        source.ReorderRow(0, 1);
        string secondName = ((Person)source.GetItem(1)).Name;
        string firstName = ((Person)source.GetItem(0)).Name;
        var source = MakeSource(people);
        var people = SamplePeople().ToList();
    {
    public void ReorderRow_MovesItemInUnderlying()
    [Fact]

    // ── Row reorder ───────────────────────────────────────────────

    }
        Assert.Null(source.GetCellValue(0, col));
        var col = source.Columns[0];
        source.SetItems(new List<Person> { new Person { Name = "Test" } });
        });
            new DataGridColumn { PropertyName = "NonExistentProp", ColumnType = DataGridColumnType.Text }
        {
        source.SetColumns(new[]
        var source = new DataGridSource();
    {
    public void GetCellValue_UnknownProperty_ReturnsNull()
    [Fact]

    }
        Assert.Equal("Seattle", source.GetCellValue(0, col));
        var col = source.Columns[0];
        source.SetItems(items);
        });
            new DataGridColumn { PropertyName = "Address.City", ColumnType = DataGridColumnType.Text }
        {
        source.SetColumns(new[]
        var source = new DataGridSource();
        };
            new NestedItem { Title = "HQ", Address = new Address { City = "Seattle" } }
        {
        var items = new List<NestedItem>
    {
    public void GetCellValue_NestedProperty_ReturnsCorrectValue()
    [Fact]

    // ── Nested property access ────────────────────────────────────

    }
        Assert.Equal(1, source.RowCount); // new source only has 1 item
        Assert.Equal(0, eventCount); // old collection no longer wired
        old.Add(new Person { Name = "Ghost", Age = 1, Department = "X" });
        source.DataChanged += () => eventCount++;
        int eventCount = 0;
        source.SetItems(newPeople);
        };
            new Person { Name = "NewPerson", Age = 99, Department = "IT" }
        {
        var newPeople = new List<Person>
        var source = MakeSource(old);
        var old = new ObservableCollection<Person>(SamplePeople());
    {
    public void SetItems_ReplacingSource_UnsubscribesFromOldCollection()
    [Fact]

    }
        Assert.True(dataChangedCount > 0);
        people[0].Name = "AliceUpdated"; // fires PropertyChanged
        source.DataChanged += () => dataChangedCount++;
        int dataChangedCount = 0;
        var source = MakeSource(people);
        var people = SamplePeople();
    {
    public void SetItems_PropertyChanged_TriggersDataChanged()
    [Fact]

    }
        Assert.Equal(4, source.RowCount);
        people.RemoveAt(0);
        source.SetItems(people);
        });
            new DataGridColumn { PropertyName = "Department", ColumnType = DataGridColumnType.Text }
            new DataGridColumn { PropertyName = "Age", ColumnType = DataGridColumnType.Numeric },
            new DataGridColumn { PropertyName = "Name", ColumnType = DataGridColumnType.Text },
        {
        source.SetColumns(new[]
        var source = new DataGridSource();
        var people = new ObservableCollection<Person>(SamplePeople());
    {
    public void SetItems_ObservableCollection_RemoveItem_UpdatesRowCount()
    [Fact]

    }
        Assert.Equal(6, source.RowCount);
        Assert.True(dataChangedCount > 0);
        people.Add(new Person { Name = "Frank", Age = 22, Department = "HR" });
        source.DataChanged += () => dataChangedCount++;
        int dataChangedCount = 0;
        source.SetItems(people);
        });
            new DataGridColumn { PropertyName = "Department", ColumnType = DataGridColumnType.Text }
            new DataGridColumn { PropertyName = "Age", ColumnType = DataGridColumnType.Numeric },
            new DataGridColumn { PropertyName = "Name", ColumnType = DataGridColumnType.Text },
        {
        source.SetColumns(new[]
        var source = new DataGridSource();
        var people = new ObservableCollection<Person>(SamplePeople());
    {
    public void SetItems_ObservableCollection_SubscribesToChanges()
    [Fact]

    // ── INCC support ──────────────────────────────────────────────

    }
        Assert.True(source.HasGroupSummaries);
        });
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        {
        source.AddGroupSummaryRow(new GroupSummaryRow("Total")
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void GroupSummaryRow_ComputesForEachGroup()
    [Fact]

    }
        Assert.Equal(105.0, (double)row.RawValues["Age"]!);
        var row = source.ComputedBottomSummaries[0];
        // Engineering: Alice(30), Charlie(35), Eve(40) = 105
        source.SetFilter(item => ((Person)item).Department == "Engineering");
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        });
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        {
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Bottom)
        var source = MakeSource(SamplePeople());
    {
    public void Summary_WithFilter_OnlyCountsVisibleRows()
    [Fact]

    }
        Assert.Equal(0, source.BottomSummaryCount);
        source.ClearTableSummaryRows();
        });
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        {
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Bottom)
        var source = MakeSource(SamplePeople());
    {
    public void ClearTableSummaryRows_RemovesAllRows()
    [Fact]

    }
        Assert.Equal(0, source.BottomSummaryCount);
        source.RemoveTableSummaryRow("Total");
        });
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        {
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Bottom)
        var source = MakeSource(SamplePeople());
    {
    public void RemoveTableSummaryRow_RemovesRow()
    [Fact]

    }
        Assert.Equal(0, source.BottomSummaryCount);
        Assert.Equal(1, source.TopSummaryCount);
        });
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        {
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Top)
        var source = MakeSource(SamplePeople());
    {
    public void AddTableSummaryRow_Top_AppearsInTopSummaries()
    [Fact]

    }
        Assert.Equal(5, (int)row.RawValues["Age"]!);
        var row = source.ComputedBottomSummaries[0];
        });
            }
                }
                    CustomAggregate = values => values.Count(v => v != null)
                {
                new SummaryColumnDescription("Age", SummaryType.Custom)
            {
            Columns =
        {
        source.AddTableSummaryRow(new TableSummaryRow("Custom", SummaryPosition.Bottom)
        var source = MakeSource(SamplePeople());
    {
    public void AddTableSummaryRow_Custom_UsesCustomFunction()
    [Fact]

    }
        Assert.Equal(40, (int)(row.RawValues["Age"] as IComparable)!);
        var row = source.ComputedBottomSummaries[0];
        });
            Columns = { new SummaryColumnDescription("Age", SummaryType.Max) }
        {
        source.AddTableSummaryRow(new TableSummaryRow("Max", SummaryPosition.Bottom)
        var source = MakeSource(SamplePeople());
    {
    public void AddTableSummaryRow_Max_FindsMaximum()
    [Fact]

    }
        Assert.Equal(25, (int)(row.RawValues["Age"] as IComparable)!);
        var row = source.ComputedBottomSummaries[0];
        });
            Columns = { new SummaryColumnDescription("Age", SummaryType.Min) }
        {
        source.AddTableSummaryRow(new TableSummaryRow("Min", SummaryPosition.Bottom)
        var source = MakeSource(SamplePeople());
    {
    public void AddTableSummaryRow_Min_FindsMinimum()
    [Fact]

    }
        Assert.Equal(5, (int)row.RawValues["Name"]!);
        var row = source.ComputedBottomSummaries[0];
        });
            Columns = { new SummaryColumnDescription("Name", SummaryType.Count) }
        {
        source.AddTableSummaryRow(new TableSummaryRow("Count", SummaryPosition.Bottom)
        var source = MakeSource(SamplePeople());
    {
    public void AddTableSummaryRow_Count_CountsRows()
    [Fact]

    }
        Assert.Equal(31.6, (double)row.RawValues["Age"]!, 5);
        var row = source.ComputedBottomSummaries[0];
        });
            Columns = { new SummaryColumnDescription("Age", SummaryType.Average) }
        {
        source.AddTableSummaryRow(new TableSummaryRow("Avg", SummaryPosition.Bottom)
        var source = MakeSource(SamplePeople());
    {
    public void AddTableSummaryRow_Average_ComputesCorrectly()
    [Fact]

    }
        Assert.Equal(158.0, (double)row.RawValues["Age"]!);
        Assert.True(row.RawValues.ContainsKey("Age"));
        var row = source.ComputedBottomSummaries[0];
        // 30 + 25 + 35 + 28 + 40 = 158
        });
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        {
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Bottom)
        var source = MakeSource(SamplePeople());
    {
    public void AddTableSummaryRow_Sum_ComputesCorrectly()
    [Fact]

    // ── Summary ───────────────────────────────────────────────────

    }
        Assert.Equal(source.RowCount, source.EffectiveFrozenRowCount);
        source.FrozenRowCount = 100;
        var source = MakeSource(SamplePeople());
    {
    public void EffectiveFrozenRowCount_ClampsToRowCount()
    [Fact]

    }
        Assert.Equal(2, source.FrozenRowCount);
        source.FrozenRowCount = 2;
        var source = MakeSource(SamplePeople());
    {
    public void FrozenRowCount_WithoutGrouping_ReturnsSetValue()
    [Fact]

    }
        Assert.Equal(0, source.FrozenRowCount);
        source.AddGroupDescription(new GroupDescription("Department"));
        source.FrozenRowCount = 2;
        var source = MakeSource(SamplePeople());
    {
    public void FrozenRowCount_WithGrouping_ReturnsZero()
    [Fact]

    }
        Assert.NotNull(eventArgs);
        source.ToggleGroupExpansion(0);
        source.GroupCollapsed += (_, e) => eventArgs = e;
        GroupCollapsedEventArgs? eventArgs = null;
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void GroupCollapsed_RaisedAfterToggle()
    [Fact]

    }
        Assert.Equal(rowsBefore, source.RowCount);
        source.ToggleGroupExpansion(0); // should be cancelled
        source.GroupExpanding += (_, e) => e.Cancel = true;
        int rowsBefore = source.RowCount;
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void GroupExpanding_CanBeCancelled()
    [Fact]

    }
        Assert.False(source.IsGroupingActive);
        source.ClearGroupDescriptions();
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void ClearGroupDescriptions_ClearsAllGrouping()
    [Fact]

    }
        Assert.Equal(5, source.RowCount);
        Assert.False(source.IsGroupingActive);
        source.RemoveGroupDescription("Department");
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void RemoveGroupDescription_ClearsGrouping()
    [Fact]

    }
        Assert.Equal(7, source.RowCount); // 2 headers + 5 data
        source.ExpandAllGroups();
        source.CollapseAllGroups();
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void ExpandAllGroups_ExpandsAllGroups()
    [Fact]

    }
        Assert.Equal(2, source.RowCount);
        // Only 2 group headers remain
        source.CollapseAllGroups();
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void CollapseAllGroups_CollapsesAllGroups()
    [Fact]

    }
        Assert.True(source.RowCount > collapsed);
        source.ToggleGroupExpansion(0); // Expand
        int collapsed = source.RowCount;
        source.ToggleGroupExpansion(0); // Collapse
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void ToggleGroupExpansion_ExpandsCollapsedGroup()
    [Fact]

    }
        Assert.True(rowCountAfter < rowCountBefore);
        int rowCountAfter = source.RowCount;
        source.ToggleGroupExpansion(0); // Collapse first group
        int rowCountBefore = source.RowCount;
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void ToggleGroupExpansion_CollapsesGroup()
    [Fact]

    }
        Assert.Throws<InvalidOperationException>(() => source.GetItem(0));
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void GetItem_GroupHeaderRow_Throws()
    [Fact]

    }
        Assert.Equal(0, info!.Level);
        Assert.NotNull(info);
        var info = source.GetGroupHeaderInfo(0);
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void GetGroupHeaderInfo_ReturnsInfoForGroupHeader()
    [Fact]

    }
        Assert.False(source.IsGroupHeaderRow(1));
        // Second row (after first group header) should be a data row
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void IsGroupHeaderRow_ReturnsFalseForDataRows()
    [Fact]

    }
        Assert.True(source.IsGroupHeaderRow(0));
        // First row should be a group header
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void IsGroupHeaderRow_ReturnsTrueForGroupHeaders()
    [Fact]

    }
        Assert.Equal(7, source.RowCount);
        // 2 groups (Engineering, Marketing) + 5 data rows = 7 rows
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void AddGroupDescription_RowCountIncludesGroupHeaders()
    [Fact]

    }
        Assert.True(source.IsGroupingActive);
        source.AddGroupDescription(new GroupDescription("Department"));
        var source = MakeSource(SamplePeople());
    {
    public void AddGroupDescription_ActivatesGrouping()
    [Fact]

    // ── Grouping ──────────────────────────────────────────────────

    }
        Assert.Equal("Updated", people[0].Name);
        source.SetCellValue(0, nameCol, "Updated");
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var source = MakeSource(people);
        var people = SamplePeople();
    {
    public void SetCellValue_UpdatesItemProperty()
    [Fact]

    // ── SetCellValue ──────────────────────────────────────────────

    }
        Assert.Null(ex);
        var ex = Record.Exception(() => source.ReorderColumn(100, 0));
        var source = MakeSource(SamplePeople());
    {
    public void ReorderColumn_OutOfRange_DoesNotThrow()
    [Fact]

    }
        Assert.Equal(originalOrder, newOrder);
        var newOrder = source.Columns.Select(c => c.PropertyName).ToList();
        source.ReorderColumn(1, 1);
        var originalOrder = source.Columns.Select(c => c.PropertyName).ToList();
        var source = MakeSource(SamplePeople());
    {
    public void ReorderColumn_SameIndex_DoesNothing()
    [Fact]

    }
        Assert.Equal(firstCol, source.Columns[1].PropertyName);
        Assert.Equal(secondCol, source.Columns[0].PropertyName);
        source.ReorderColumn(0, 1);
        var secondCol = source.Columns[1].PropertyName;
        var firstCol = source.Columns[0].PropertyName;
        var source = MakeSource(SamplePeople());
    {
    public void ReorderColumn_ChangesColumnOrder()
    [Fact]

    // ── Column reorder ────────────────────────────────────────────

    }
        Assert.Single(source.ActiveFilters);
        Assert.NotEmpty(source.ActiveFilters);
        });
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "A" }
        {
        source.SetColumnFilter(nameCol, new FilterDescription(nameCol)

        Assert.Empty(source.ActiveFilters);
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var source = MakeSource(SamplePeople());
    {
    public void ActiveFilters_ReflectsActiveColumnFilters()
    [Fact]

    }
        Assert.Equal("Marketing", values[1]);
        Assert.Equal("Engineering", values[0]);
        // Sorted alphabetically
        Assert.Contains("Marketing", values);
        Assert.Contains("Engineering", values);
        Assert.Equal(2, values.Count);
        var values = source.GetUniqueColumnValues(deptCol);
        var deptCol = source.Columns.First(c => c.PropertyName == "Department");
        var source = MakeSource(SamplePeople());
    {
    public void GetUniqueColumnValues_ReturnsDistinctSortedValues()
    [Fact]

    }
        Assert.Same(nameCol, eventArgs!.Column);
        Assert.NotNull(eventArgs);
        });
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "Alice" }
        {
        source.SetColumnFilter(nameCol, new FilterDescription(nameCol)
        source.FilterChanged += (_, e) => eventArgs = e;
        FilterChangedEventArgs? eventArgs = null;
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var source = MakeSource(SamplePeople());
    {
    public void FilterChanged_RaisedAfterFilter()
    [Fact]

    }
        Assert.Equal(5, source.RowCount); // Filter was cancelled
        });
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "Alice" }
        {
        source.SetColumnFilter(nameCol, new FilterDescription(nameCol)
        source.FilterChanging += (_, e) => e.Cancel = true;
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var source = MakeSource(SamplePeople());
    {
    public void FilterChanging_CanBeCancelled()
    [Fact]

    }
        Assert.Empty(source.ActiveFilters);
        Assert.Equal(5, source.RowCount);
        source.ClearAllFilters();
        });
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "Alice" }
        {
        source.SetColumnFilter(nameCol, new FilterDescription(nameCol)
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var source = MakeSource(SamplePeople());
    {
    public void ClearAllFilters_RemovesAllFilters()
    [Fact]

    }
        Assert.Equal(5, source.RowCount);
        source.SetColumnFilter(nameCol, null);
        source.SetColumnFilter(nameCol, filter);
        };
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "li" }
        {
        var filter = new FilterDescription(nameCol)
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var source = MakeSource(SamplePeople());
    {
    public void SetColumnFilter_Null_ClearsFilter()
    [Fact]

    }
        Assert.Equal("Alice", ((Person)source.GetItem(0)).Name);
        Assert.Equal(1, source.RowCount); // Alice only
        source.SetColumnFilter(nameCol, filter);
        };
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "ice" }
        {
        var filter = new FilterDescription(nameCol)
        // "ice" uniquely appears in "Alice" but not in any other sample name
        var nameCol = source.Columns.First(c => c.PropertyName == "Name");
        var source = MakeSource(SamplePeople());
    {
    public void SetColumnFilter_TextContains_FiltersRows()
    [Fact]

    }
        Assert.Equal(5, source.RowCount);
        source.SetFilter(null);
        source.SetFilter(item => ((Person)item).Age > 30);
        var source = MakeSource(SamplePeople());
    {
