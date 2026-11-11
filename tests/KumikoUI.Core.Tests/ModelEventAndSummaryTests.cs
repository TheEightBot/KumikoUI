using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// The cancelable filter, sort and group-expansion events of a source, the group header info of nested
/// groups, and the computed summary rows for table and group summaries.
/// </summary>
public class ModelEventAndSummaryTests
{
    private sealed class Employee
    {
        public string Name { get; set; } = "";
        public string Dept { get; set; } = "";
        public string City { get; set; } = "";
        public decimal Salary { get; set; }
    }

    private sealed class Fixture
    {
        public List<Employee> People { get; } = new()
        {
            new Employee { Name = "Cy", Dept = "Eng", City = "Oslo", Salary = 100 },
            new Employee { Name = "Ann", Dept = "Eng", City = "Oslo", Salary = 200 },
            new Employee { Name = "Bea", Dept = "Eng", City = "Rome", Salary = 300 },
            new Employee { Name = "Dov", Dept = "Ops", City = "Rome", Salary = 50 },
        };

        public KumikoSource Source { get; } = new();
        public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name" };
        public KumikoColumn Dept { get; } = new() { Header = "Department", PropertyName = "Dept" };
        public KumikoColumn City { get; } = new() { Header = "City", PropertyName = "City" };
        public KumikoColumn Salary { get; } = new() { Header = "Salary", PropertyName = "Salary", ColumnType = KumikoColumnType.Numeric };

        public Fixture()
        {
            Source.SetColumns(new[] { Name, Dept, City, Salary });
            Source.SetItems(People);
        }

        public List<string> Names() =>
            Enumerable.Range(0, Source.RowCount)
                .Where(r => !Source.IsNonDataRow(r))
                .Select(r => (string)Source.GetCellValue(r, Name)!)
                .ToList();
    }

    // ── FilterChangingEventArgs ───────────────────────────────────

    [Fact]
    public void FilterChangingEventArgs_CarriesTheColumnAndTheNewFilter()
    {
        var column = new KumikoColumn();
        var filter = new FilterDescription(column);

        var args = new FilterChangingEventArgs(column, filter);

        Assert.Same(column, args.Column);
        Assert.Same(filter, args.NewFilter);
        Assert.False(args.Cancel);
    }

    [Fact]
    public void FilterChanging_IsRaisedBeforeTheFilterApplies()
    {
        var f = new Fixture();
        var filter = new FilterDescription(f.Dept) { SelectedValues = new HashSet<string> { "Ops" } };
        FilterChangingEventArgs? seen = null;
        int rowsDuringEvent = -1;
        f.Source.FilterChanging += (_, e) => { seen = e; rowsDuringEvent = f.Source.RowCount; };

        f.Source.SetColumnFilter(f.Dept, filter);

        Assert.NotNull(seen);
        Assert.Same(f.Dept, seen!.Column);
        Assert.Same(filter, seen.NewFilter);
        Assert.Equal(4, rowsDuringEvent);
        Assert.Equal(new[] { "Dov" }, f.Names());
    }

    [Fact]
    public void FilterChanging_Cancel_PreventsTheFilter()
    {
        var f = new Fixture();
        int changed = 0;
        f.Source.FilterChanging += (_, e) => e.Cancel = true;
        f.Source.FilterChanged += (_, _) => changed++;

        f.Source.SetColumnFilter(f.Dept, new FilterDescription(f.Dept) { SelectedValues = new HashSet<string> { "Ops" } });

        Assert.Null(f.Dept.ActiveFilter);
        Assert.Equal(4, f.Source.RowCount);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void FilterChanging_CancelAlsoPreventsClearingAFilter()
    {
        var f = new Fixture();
        var filter = new FilterDescription(f.Dept) { SelectedValues = new HashSet<string> { "Ops" } };
        f.Source.SetColumnFilter(f.Dept, filter);
        f.Source.FilterChanging += (_, e) => e.Cancel = true;

        f.Source.SetColumnFilter(f.Dept, null);

        Assert.Same(filter, f.Dept.ActiveFilter);
        Assert.Equal(new[] { "Dov" }, f.Names());
    }

    [Fact]
    public void FilterChanging_ClearingReportsANullNewFilter()
    {
        var f = new Fixture();
        f.Source.SetColumnFilter(f.Dept, new FilterDescription(f.Dept) { SelectedValues = new HashSet<string> { "Ops" } });
        var seen = new List<FilterChangingEventArgs>();
        f.Source.FilterChanging += (_, e) => seen.Add(e);

        f.Source.SetColumnFilter(f.Dept, null);

        Assert.Null(Assert.Single(seen).NewFilter);
        Assert.Equal(4, f.Source.RowCount);
    }

    [Fact]
    public void FilterChanging_IsNotRaisedWhenClearingAColumnWithoutAFilter()
    {
        var f = new Fixture();
        int raised = 0;
        f.Source.FilterChanging += (_, _) => raised++;

        f.Source.SetColumnFilter(f.Dept, null);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void FilterChanging_IsNotRaisedForAColumnThatDisallowsFiltering()
    {
        var f = new Fixture();
        f.Dept.AllowFiltering = false;
        int raised = 0;
        f.Source.FilterChanging += (_, _) => raised++;

        f.Source.SetColumnFilter(f.Dept, new FilterDescription(f.Dept) { SelectedValues = new HashSet<string> { "Ops" } });

        Assert.Equal(0, raised);
        Assert.Null(f.Dept.ActiveFilter);
    }

    // ── SortColumnsChangingEventArgs ──────────────────────────────

    [Fact]
    public void SortColumnsChangingEventArgs_CarriesItsValues()
    {
        var column = new KumikoColumn();

        var args = new SortColumnsChangingEventArgs(column, true, KumikoSortDirection.Descending);

        Assert.Same(column, args.Column);
        Assert.True(args.IsMultiSort);
        Assert.Equal(KumikoSortDirection.Descending, args.NewDirection);
        Assert.False(args.Cancel);
    }

    [Fact]
    public void SortColumnsChanging_ToggleSortReportsTheNextDirection()
    {
        var f = new Fixture();
        var seen = new List<SortColumnsChangingEventArgs>();
        f.Source.SortColumnsChanging += (_, e) => seen.Add(e);

        f.Source.ToggleSort(f.Name);
        f.Source.ToggleSort(f.Name);
        f.Source.ToggleSort(f.Name);

        Assert.Equal(
            new[] { KumikoSortDirection.Ascending, KumikoSortDirection.Descending, KumikoSortDirection.None },
            seen.Select(e => e.NewDirection));
        Assert.All(seen, e => Assert.Same(f.Name, e.Column));
        Assert.All(seen, e => Assert.False(e.IsMultiSort));
    }

    [Fact]
    public void SortColumnsChanging_Cancel_KeepsTheOrderAndTheSortState()
    {
        var f = new Fixture();
        int changed = 0;
        f.Source.SortColumnsChanging += (_, e) => e.Cancel = true;
        f.Source.SortColumnsChanged += (_, _) => changed++;
        var before = f.Names();

        f.Source.ToggleSort(f.Name);

        Assert.Equal(KumikoSortDirection.None, f.Name.SortDirection);
        Assert.Equal(before, f.Names());
        Assert.Equal(0, changed);
    }

    [Fact]
    public void SortColumnsChanging_NotCanceled_SortsAndRaisesChanged()
    {
        var f = new Fixture();
        int changed = 0;
        f.Source.SortColumnsChanged += (_, _) => changed++;

        f.Source.ToggleSort(f.Name);

        Assert.Equal(new[] { "Ann", "Bea", "Cy", "Dov" }, f.Names());
        Assert.Equal(1, changed);
    }

    [Fact]
    public void SortColumnsChanging_MultiSortIsFlagged()
    {
        var f = new Fixture();
        f.Source.ToggleSort(f.Dept);
        var seen = new List<SortColumnsChangingEventArgs>();
        f.Source.SortColumnsChanging += (_, e) => seen.Add(e);

        f.Source.ToggleMultiSort(f.City);

        var args = Assert.Single(seen);
        Assert.True(args.IsMultiSort);
        Assert.Same(f.City, args.Column);
        Assert.Equal(KumikoSortDirection.Ascending, args.NewDirection);
    }

    [Fact]
    public void SortColumnsChanging_SetSortIsCancelableToo()
    {
        var f = new Fixture();
        f.Source.SortColumnsChanging += (_, e) => e.Cancel = e.NewDirection == KumikoSortDirection.Descending;

        f.Source.SetSort(f.Salary, KumikoSortDirection.Descending);
        Assert.Equal(KumikoSortDirection.None, f.Salary.SortDirection);

        f.Source.SetSort(f.Salary, KumikoSortDirection.Ascending);
        Assert.Equal(KumikoSortDirection.Ascending, f.Salary.SortDirection);
        Assert.Equal("Dov", f.Names()[0]);
    }

    [Fact]
    public void SortColumnsChanging_ClearSortDoesNotRaiseIt()
    {
        var f = new Fixture();
        f.Source.ToggleSort(f.Name);
        int raised = 0;
        f.Source.SortColumnsChanging += (_, _) => raised++;

        f.Source.ClearSort();

        Assert.Equal(0, raised);
        Assert.Equal(KumikoSortDirection.None, f.Name.SortDirection);
    }

    [Fact]
    public void SortColumnsChanging_IsNotRaisedForAColumnThatDisallowsSorting()
    {
        var f = new Fixture();
        f.Name.AllowSorting = false;
        int raised = 0;
        f.Source.SortColumnsChanging += (_, _) => raised++;

        f.Source.ToggleSort(f.Name);

        Assert.Equal(0, raised);
    }

    // ── GroupExpandingEventArgs and GroupHeaderInfo ───────────────

    private static Fixture Grouped()
    {
        var f = new Fixture();
        f.Source.AddGroupDescription(new GroupDescription("Dept"));
        f.Source.AddGroupDescription(new GroupDescription("City", "Town"));
        return f;
    }

    [Fact]
    public void GroupExpandingEventArgs_CarriesTheGroupAndDirection()
    {
        var info = new GroupHeaderInfo { DisplayText = "Eng" };

        var args = new GroupExpandingEventArgs(info, isExpanding: false);

        Assert.Same(info, args.Group);
        Assert.False(args.IsExpanding);
        Assert.False(args.Cancel);
    }

    [Fact]
    public void GroupHeaderInfo_NestedGroups_DescribeEachLevel()
    {
        var f = Grouped();

        // Eng, Oslo (2), Rome (1) then Ops, Rome (1): headers appear in this order in the flat view.
        var headers = Enumerable.Range(0, f.Source.RowCount)
            .Select(r => f.Source.GetGroupHeaderInfo(r))
            .Where(h => h != null)
            .Select(h => h!)
            .ToList();

        Assert.Equal(5, headers.Count);

        var eng = headers[0];
        Assert.Equal(0, eng.Level);
        Assert.Equal("Eng", eng.Key);
        Assert.Equal("Eng", eng.DisplayText);
        Assert.Equal("Dept", eng.PropertyName);
        Assert.Equal("Department", eng.HeaderText);
        Assert.Equal(3, eng.ItemCount); // all nested levels
        Assert.True(eng.IsExpanded);
        Assert.Equal("Dept=Eng", eng.GroupPath);

        var oslo = headers[1];
        Assert.Equal(1, oslo.Level);
        Assert.Equal("Oslo", oslo.Key);
        Assert.Equal("City", oslo.PropertyName);
        Assert.Equal("Town", oslo.HeaderText); // the description's header override
        Assert.Equal(2, oslo.ItemCount);
        Assert.Equal("Dept=Eng|City=Oslo", oslo.GroupPath);

        Assert.Equal("Dept=Eng|City=Rome", headers[2].GroupPath);
        Assert.Equal(1, headers[2].ItemCount);
        Assert.Equal("Dept=Ops", headers[3].GroupPath);
        Assert.Equal(1, headers[3].ItemCount);
        Assert.Equal("Dept=Ops|City=Rome", headers[4].GroupPath);
    }

    [Fact]
    public void GroupHeaderInfo_IsNullForDataRowsAndOutOfRangeRows()
    {
        var f = Grouped();

        Assert.NotNull(f.Source.GetGroupHeaderInfo(0));
        Assert.Null(f.Source.GetGroupHeaderInfo(2)); // a data row under Eng / Oslo
        Assert.Null(f.Source.GetGroupHeaderInfo(-1));
        Assert.Null(f.Source.GetGroupHeaderInfo(500));
    }

    [Fact]
    public void GroupHeaderInfo_UsesTheKeyAndDisplaySelectors()
    {
        var f = new Fixture();
        f.Source.AddGroupDescription(new GroupDescription("Salary")
        {
            KeySelector = v => (decimal)v! >= 150 ? "high" : "low",
            DisplayTextSelector = k => $"{k} pay",
        });

        var headers = Enumerable.Range(0, f.Source.RowCount)
            .Select(r => f.Source.GetGroupHeaderInfo(r)).Where(h => h != null).Select(h => h!).ToList();

        Assert.Equal(new[] { "high pay", "low pay" }, headers.Select(h => h.DisplayText).OrderBy(t => t));
        Assert.Equal(2, headers.Single(h => h.Key as string == "high").ItemCount);
        Assert.Equal(2, headers.Single(h => h.Key as string == "low").ItemCount);
    }

    [Fact]
    public void GroupExpanding_IsRaisedWithTheGroupAndTheDirection_AndCollapsesTheGroup()
    {
        var f = Grouped();
        int rowsBefore = f.Source.RowCount;
        var seen = new List<GroupExpandingEventArgs>();
        f.Source.GroupExpanding += (_, e) => seen.Add(e);

        f.Source.ToggleGroupExpansion(0);

        var args = Assert.Single(seen);
        Assert.Equal("Dept=Eng", args.Group.GroupPath);
        Assert.False(args.IsExpanding); // expanded groups collapse
        Assert.True(f.Source.RowCount < rowsBefore);
        Assert.False(f.Source.GetGroupHeaderInfo(0)!.IsExpanded);
    }

    [Fact]
    public void GroupExpanding_TogglingACollapsedGroupReportsExpanding()
    {
        var f = Grouped();
        f.Source.ToggleGroupExpansion(0);
        var seen = new List<GroupExpandingEventArgs>();
        f.Source.GroupExpanding += (_, e) => seen.Add(e);

        f.Source.ToggleGroupExpansion(0);

        Assert.True(Assert.Single(seen).IsExpanding);
        Assert.True(f.Source.GetGroupHeaderInfo(0)!.IsExpanded);
    }

    [Fact]
    public void GroupExpanding_Cancel_LeavesTheGroupAsItWas()
    {
        var f = Grouped();
        int rowsBefore = f.Source.RowCount;
        int changed = 0;
        f.Source.GroupExpanding += (_, e) => e.Cancel = true;
        f.Source.GroupExpandedChanged += (_, _) => changed++;

        f.Source.ToggleGroupExpansion(0);

        Assert.Equal(rowsBefore, f.Source.RowCount);
        Assert.True(f.Source.GetGroupHeaderInfo(0)!.IsExpanded);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void GroupExpanding_IsNotRaisedForADataRow()
    {
        var f = Grouped();
        int raised = 0;
        f.Source.GroupExpanding += (_, _) => raised++;

        f.Source.ToggleGroupExpansion(2);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void GroupExpandedChanged_FollowsAnAcceptedToggle()
    {
        var f = Grouped();
        var changed = new List<GroupExpandedChangedEventArgs>();
        f.Source.GroupExpandedChanged += (_, e) => changed.Add(e);

        f.Source.ToggleGroupExpansion(0);

        var args = Assert.Single(changed);
        Assert.Equal("Dept=Eng", args.Group.GroupPath);
        Assert.False(args.Group.IsExpanded);
    }

    // ── ComputedSummaryRow ────────────────────────────────────────

    [Fact]
    public void ComputedSummaryRow_Defaults()
    {
        var row = new ComputedSummaryRow();

        Assert.Equal("", row.Name);
        Assert.Null(row.Title);
        Assert.Empty(row.Values);
        Assert.Empty(row.RawValues);
    }

    [Fact]
    public void TableSummary_BottomRowHoldsFormattedAndRawValues()
    {
        var f = new Fixture();
        f.Source.AddTableSummaryRow(new TableSummaryRow("Totals", SummaryPosition.Bottom)
        {
            Title = "All",
            Columns =
            {
                new SummaryColumnDescription("Salary", SummaryType.Sum),
                new SummaryColumnDescription("Name", SummaryType.Count),
            },
        });

        Assert.Equal(1, f.Source.BottomSummaryCount);
        Assert.Equal(0, f.Source.TopSummaryCount);
        var row = Assert.Single(f.Source.ComputedBottomSummaries);
        Assert.Equal("Totals", row.Name);
        Assert.Equal("All", row.Title);
        Assert.Equal("Sum: 650", row.Values["Salary"]);
        Assert.Equal(650m, row.RawValues["Salary"]);
        Assert.Equal("Count: 4", row.Values["Name"]);
        Assert.Equal(4, row.RawValues["Name"]);
    }

    [Fact]
    public void TableSummary_TopPositionLandsInTheTopList()
    {
        var f = new Fixture();
        f.Source.AddTableSummaryRow(new TableSummaryRow("Head", SummaryPosition.Top)
        {
            Columns = { new SummaryColumnDescription("Salary", SummaryType.Max) },
        });

        Assert.Equal(1, f.Source.TopSummaryCount);
        Assert.Empty(f.Source.ComputedBottomSummaries);
        Assert.Equal(300m, Assert.Single(f.Source.ComputedTopSummaries).RawValues["Salary"]);
    }

    [Fact]
    public void TableSummary_EachAggregate()
    {
        var f = new Fixture();
        f.Source.AddTableSummaryRow(new TableSummaryRow("Stats")
        {
            Columns =
            {
                new SummaryColumnDescription("Salary", SummaryType.Average),
            },
        });
        f.Source.AddTableSummaryRow(new TableSummaryRow("Min") { Columns = { new SummaryColumnDescription("Salary", SummaryType.Min) } });
        f.Source.AddTableSummaryRow(new TableSummaryRow("Max") { Columns = { new SummaryColumnDescription("Salary", SummaryType.Max) } });
        f.Source.AddTableSummaryRow(new TableSummaryRow("Custom")
        {
            Columns =
            {
                new SummaryColumnDescription
                {
                    PropertyName = "Name", SummaryType = SummaryType.Custom,
                    CustomAggregate = values => string.Join("+", values.Select(v => v?.ToString()).OrderBy(s => s)),
                },
            },
        });

        var rows = f.Source.ComputedBottomSummaries.ToDictionary(r => r.Name);
        Assert.Equal(162.5m, rows["Stats"].RawValues["Salary"]);
        Assert.StartsWith("Avg: ", rows["Stats"].Values["Salary"]);
        Assert.Equal(50m, rows["Min"].RawValues["Salary"]);
        Assert.StartsWith("Min: ", rows["Min"].Values["Salary"]);
        Assert.Equal(300m, rows["Max"].RawValues["Salary"]);
        Assert.Equal("Ann+Bea+Cy+Dov", rows["Custom"].RawValues["Name"]);
        Assert.Equal("Ann+Bea+Cy+Dov", rows["Custom"].Values["Name"]); // no default label for custom
    }

    [Fact]
    public void TableSummary_LabelAndFormatShapeTheDisplayedValue()
    {
        var f = new Fixture();
        f.Source.AddTableSummaryRow(new TableSummaryRow("Fmt")
        {
            Columns =
            {
                new SummaryColumnDescription("Salary", SummaryType.Sum, "${0} total") { Label = "" },
            },
        });
        f.Source.AddTableSummaryRow(new TableSummaryRow("Fixed")
        {
            Columns =
            {
                new SummaryColumnDescription("Salary", SummaryType.Sum, "F1") { Label = "Total " },
            },
        });

        var rows = f.Source.ComputedBottomSummaries.ToDictionary(r => r.Name);
        Assert.Equal("$650 total", rows["Fmt"].Values["Salary"]);
        Assert.StartsWith("Total 650", rows["Fixed"].Values["Salary"]);
        Assert.Equal(650m, rows["Fixed"].RawValues["Salary"]);
    }

    [Fact]
    public void TableSummary_OfAnUnknownColumn_SumsToZeroAndHasNoMinimum()
    {
        var f = new Fixture();
        f.Source.AddTableSummaryRow(new TableSummaryRow("Sum") { Columns = { new SummaryColumnDescription("Missing", SummaryType.Sum) } });
        f.Source.AddTableSummaryRow(new TableSummaryRow("Min") { Columns = { new SummaryColumnDescription("Missing", SummaryType.Min) } });
        f.Source.AddTableSummaryRow(new TableSummaryRow("Avg") { Columns = { new SummaryColumnDescription("Missing", SummaryType.Average) } });

        var rows = f.Source.ComputedBottomSummaries.ToDictionary(r => r.Name);
        Assert.Equal(0d, rows["Sum"].RawValues["Missing"]);
        Assert.Null(rows["Min"].RawValues["Missing"]);
        Assert.Null(rows["Avg"].RawValues["Missing"]);
        Assert.Equal("Min: ", rows["Min"].Values["Missing"]);
    }

    [Fact]
    public void TableSummary_FollowsTheFilter()
    {
        var f = new Fixture();
        f.Source.AddTableSummaryRow(new TableSummaryRow("Totals")
        {
            Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) },
        });

        f.Source.SetFilter(o => ((Employee)o).Dept == "Ops");

        Assert.Equal(50m, Assert.Single(f.Source.ComputedBottomSummaries).RawValues["Salary"]);
    }

    [Fact]
    public void TableSummary_RemovingTheRowRemovesTheComputedRow()
    {
        var f = new Fixture();
        f.Source.AddTableSummaryRow(new TableSummaryRow("Totals") { Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } });

        f.Source.RemoveTableSummaryRow("Totals");

        Assert.Empty(f.Source.ComputedBottomSummaries);
        Assert.Equal(0, f.Source.BottomSummaryCount);
    }

    [Fact]
    public void GroupSummary_IsComputedPerInnermostGroup()
    {
        var f = Grouped();
        f.Source.AddGroupSummaryRow(new GroupSummaryRow("GT")
        {
            Title = "Group",
            Columns =
            {
                new SummaryColumnDescription("Salary", SummaryType.Sum),
                new SummaryColumnDescription("Name", SummaryType.Count),
            },
        });

        Assert.True(f.Source.HasGroupSummaries);

        var oslo = Assert.Single(f.Source.GetGroupSummaries("Dept=Eng|City=Oslo")!);
        Assert.Equal("GT", oslo.Name);
        Assert.Equal("Group", oslo.Title);
        Assert.Equal(300m, oslo.RawValues["Salary"]);
        Assert.Equal("Sum: 300", oslo.Values["Salary"]);
        Assert.Equal(2, oslo.RawValues["Name"]);
        Assert.Equal("Count: 2", oslo.Values["Name"]);

        Assert.Equal(300m, Assert.Single(f.Source.GetGroupSummaries("Dept=Eng|City=Rome")!).RawValues["Salary"]);
        Assert.Equal(50m, Assert.Single(f.Source.GetGroupSummaries("Dept=Ops|City=Rome")!).RawValues["Salary"]);
        Assert.Null(f.Source.GetGroupSummaries("Dept=Eng")); // outer groups have none
    }

    [Fact]
    public void GroupSummary_UnknownGroupHasNone()
    {
        var f = Grouped();
        f.Source.AddGroupSummaryRow(new GroupSummaryRow("GT") { Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } });

        Assert.Null(f.Source.GetGroupSummaries("Dept=Nope"));
    }

    [Fact]
    public void GroupSummary_AppearsAsANonDataRowAfterTheGroup()
    {
        var f = new Fixture();
        f.Source.AddGroupDescription(new GroupDescription("Dept"));
        f.Source.AddGroupSummaryRow(new GroupSummaryRow("GT") { Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } });

        var summaryRows = Enumerable.Range(0, f.Source.RowCount).Where(f.Source.IsGroupSummaryRow).ToList();

        Assert.Equal(2, summaryRows.Count);
        var info = f.Source.GetGroupSummaryRowInfo(summaryRows[0]);
        Assert.True(info.HasValue);
        var row = info!.Value;
        Assert.Equal("Dept=Eng", row.GroupPath);
        Assert.Equal(0, row.SummaryIndex);
        Assert.Null(f.Source.GetGroupSummaryRowInfo(0)); // a header row
    }
}
