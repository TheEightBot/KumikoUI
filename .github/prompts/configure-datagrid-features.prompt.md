---
mode: agent
description: Set up sorting, filtering, grouping, and summaries on KumikoDataGrid
---

# Configure KumikoSource Features

Adds sorting, filtering, grouping, and/or summary rows to an existing `KumikoDataGrid`
binding, on .NET MAUI (`KumikoUI.Maui`) or the Uno Platform (`KumikoUI.Uno`): the properties and `DataSource` are the same
on both. All data configuration targets `KumikoSource` in `KumikoUI.Core.Models`, which the grid exposes as
`DataSource`. The XAML examples use the MAUI namespace prefix `dg:` (`clr-namespace:KumikoUI.Maui`); on Uno it is
`kumiko:` (`using:KumikoUI.Uno`) and the Core prefix is `using:KumikoUI.Core.Models`.

## What to ask before generating

1. Which features are needed? (sorting / filtering / grouping / summaries / all)
2. What are the **property names** of columns that need these features?
3. For summaries: which aggregates are needed? (Sum, Average, Count, Min, Max, Custom)
4. For grouping: should groups default to **expanded** or **collapsed**?
5. Is the data source a raw `IList` or an `ObservableCollection<T>` (live updates)?

---

## Sorting

Sorting is **automatic**: tapping a column header cycles Ascending → Descending → None (Ctrl or Shift on a header adds a
column to the sort chain). Turn it off for the grid or per column:

```csharp
// XAML:
<core:KumikoColumn AllowSorting="True" ... />

// Code (default is True):
column.AllowSorting = false;  // disable for specific column
```

**Programmatic sort:**
```csharp
// Single column ascending:
kumikoDataGrid.DataSource.SetSort(column, KumikoSortDirection.Ascending);

// Multi-column (the order is the place in the chain, 1 = primary):
kumikoDataGrid.DataSource.SetSort(columnA, KumikoSortDirection.Ascending, 1);
kumikoDataGrid.DataSource.SetSort(columnB, KumikoSortDirection.Descending, 2);

kumikoDataGrid.DataSource.ClearSort();
```

---

## Filtering

Filtering uses `FilterDescription` with up to two `FilterCondition` objects, joined by `Combination`.

```csharp
// Text contains filter:
var filter = new FilterDescription(column)
{
    Condition1 = new FilterCondition
    {
        TextOperator = TextFilterOperator.Contains,
        Value        = "Engineering"
    }
};
kumikoDataGrid.DataSource.SetColumnFilter(column, filter);

// Numeric range filter (AND):
var numFilter = new FilterDescription(numericColumn)
{
    Condition1 = new FilterCondition
    {
        NumericOperator = NumericFilterOperator.GreaterThan,
        Value = 50000d
    },
    Combination = FilterCombination.And,
    Condition2 = new FilterCondition
    {
        NumericOperator = NumericFilterOperator.LessThanOrEqual,
        Value = 150000d
    }
};
kumikoDataGrid.DataSource.SetColumnFilter(numericColumn, numFilter);

// Clear one column's filter, or all of them:
kumikoDataGrid.DataSource.SetColumnFilter(column, null);
kumikoDataGrid.DataSource.ClearAllFilters();
```

A predicate over the items themselves is `DataSource.SetFilter(item => ...)` (null clears it).

**Excel-style value-checklist filter** (built into the column header popup; user-driven):
Enabled by default when `AllowFiltering="True"` on the column (the default); the grid-level `AllowFiltering` switch turns
every filter icon off.

---

## Grouping

`KumikoSource.GroupDescriptions` is a read-only list: change it with `AddGroupDescription`, `RemoveGroupDescription`,
`SetGroupDescriptions` or `ClearGroupDescriptions`, each of which rebuilds the view.

```csharp
// Code: before or after ItemsSource is set:
kumikoDataGrid.DataSource.AddGroupDescription(new GroupDescription("Department"));

// Multi-level:
kumikoDataGrid.DataSource.AddGroupDescription(new GroupDescription("City"));

// Custom display text:
kumikoDataGrid.DataSource.AddGroupDescription(new GroupDescription("HireDate")
{
    DisplayTextSelector = key => key is DateTime dt ? dt.ToString("yyyy") : key?.ToString() ?? ""
});

// Expand or collapse:
kumikoDataGrid.DataSource.CollapseAllGroups();   // ExpandAllGroups(), ToggleGroupExpansion(row)
```

The host's own `GroupDescriptions` collection (settable in XAML) feeds the same list.

**Group summary rows (in XAML):**
```xml
<dg:KumikoDataGrid.GroupSummaryRows>
    <core:GroupSummaryRow Name="GroupTotals">
        <core:GroupSummaryRow.Columns>
            <core:SummaryColumnDescription PropertyName="Salary"
                                           SummaryType="Sum"
                                           Format="C0"
                                           Label="Group Total: " />
        </core:GroupSummaryRow.Columns>
    </core:GroupSummaryRow>
</dg:KumikoDataGrid.GroupSummaryRows>
```

---

## Table Summary Rows

**In XAML:**
```xml
<dg:KumikoDataGrid.TableSummaryRows>

    <core:TableSummaryRow Name="Averages" Position="Top" Title="Averages">
        <core:TableSummaryRow.Columns>
            <core:SummaryColumnDescription PropertyName="Salary"
                                           SummaryType="Average"
                                           Format="C0" />
            <core:SummaryColumnDescription PropertyName="Performance"
                                           SummaryType="Average"
                                           Format="N1" />
        </core:TableSummaryRow.Columns>
    </core:TableSummaryRow>

    <core:TableSummaryRow Name="Totals" Position="Bottom" Title="Totals">
        <core:TableSummaryRow.Columns>
            <core:SummaryColumnDescription PropertyName="Salary"
                                           SummaryType="Sum"
                                           Format="C0" />
            <core:SummaryColumnDescription PropertyName="Id"
                                           SummaryType="Count"
                                           Label="Rows: " />
        </core:TableSummaryRow.Columns>
    </core:TableSummaryRow>

</dg:KumikoDataGrid.TableSummaryRows>
```

**In code:**
```csharp
var summaryRow = new TableSummaryRow("Totals", SummaryPosition.Bottom)
{
    Title   = "Totals",
    Columns =
    {
        new SummaryColumnDescription("Salary", SummaryType.Sum)   { Format = "C0" },
        new SummaryColumnDescription("Id",     SummaryType.Count) { Label = "Rows: " },
        new SummaryColumnDescription("Rating", SummaryType.Max)   { Label = "Best: " }
    }
};
kumikoDataGrid.TableSummaryRows.Add(summaryRow);   // the collection exists on both hosts; or DataSource.AddTableSummaryRow(summaryRow)
```

A bottom summary stays visible as a footer while `KumikoStyle.PinBottomSummaryRows` is on (the default).

**Summary types:**
| `SummaryType` | Description |
|---|---|
| `Sum` | Total of all numeric values |
| `Average` | Mean of all numeric values |
| `Count` | Number of visible rows |
| `Min` | Minimum value |
| `Max` | Maximum value |
| `Custom` | Set `SummaryColumnDescription.CustomAggregate` to a `Func<IReadOnlyList<object?>, object?>` |

---

## Live data

```csharp
// Use ObservableCollection<T> for O(1) add/remove notifications:
Employees = new ObservableCollection<Employee>(data);

// Bind:
kumikoDataGrid.ItemsSource = Employees;

// Add/remove: the grid updates automatically (on the UI thread):
Employees.Add(new Employee { ... });
Employees.RemoveAt(index);

// For bulk changes, batch them: one rebuild and one ViewChanged at the end.
using (kumikoDataGrid.DataSource.BeginUpdate())
{
    foreach (var item in newItems) Employees.Add(item);
}
```

With many rows (from `BackgroundRebuildThreshold`, 10,000 by default in the hosts), sort, filter, grouping and summary
changes rebuild on a worker; `DataSource.ViewUpdated` is raised when the new view is in place.
