---
mode: agent
description: Set up sorting, filtering, grouping, and summaries on DataGridView
---

# Configure DataGridSource Features

Adds sorting, filtering, grouping, and/or summary rows to an existing `DataGridView`
binding. All configuration targets `DataGridSource` in `KumikoUI.Core.Models`.

## What to ask before generating

1. Which features are needed? (sorting / filtering / grouping / summaries / all)
2. What are the **property names** of columns that need these features?
3. For summaries: which aggregates are needed? (Sum, Average, Count, Min, Max, Custom)
4. For grouping: should groups default to **expanded** or **collapsed**?
5. Is the data source a raw `IList` or an `ObservableCollection<T>` (live updates)?

---

## Sorting

Sorting is **automatic** — tapping a column header cycles Ascending → Descending → None.
To enable/disable per-column:

```csharp
// XAML:
<core:DataGridColumn AllowSorting="True" ... />

// Code (default is True):
column.AllowSorting = false;  // disable for specific column
```

**Programmatic sort:**
```csharp
// Single column ascending:
dataGridView.Source.ApplySort(column, SortDirection.Ascending);

// Multi-column (set SortOrder to control priority):
columnA.SortDirection = SortDirection.Ascending;
columnA.SortOrder     = 1;
columnB.SortDirection = SortDirection.Descending;
columnB.SortOrder     = 2;
dataGridView.Source.ApplySorts();
```

---

## Filtering

Filtering uses `FilterDescription` with up to two `FilterCondition` objects (AND/OR).

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
dataGridView.Source.ApplyFilter(column, filter);

// Numeric range filter (AND):
var numFilter = new FilterDescription(numericColumn)
{
    Condition1 = new FilterCondition
    {
        NumericOperator = NumericFilterOperator.GreaterThan,
        Value = 50000d
    },
    LogicalOperator = FilterLogicalOperator.And,
    Condition2 = new FilterCondition
    {
        NumericOperator = NumericFilterOperator.LessThanOrEqual,
        Value = 150000d
    }
};
dataGridView.Source.ApplyFilter(numericColumn, numFilter);

// Clear filter:
dataGridView.Source.ApplyFilter(column, null);
```

**Excel-style value-checklist filter** (built into the column header popup; user-driven):
Enabled by default when `AllowFiltering="True"` on the column (the default).

---

## Grouping

```csharp
// Code — add before or after ItemsSource is set:
dataGridView.Source.GroupDescriptions.Add(
    new GroupDescription("Department"));

// Multi-level:
dataGridView.Source.GroupDescriptions.Add(
    new GroupDescription("Department"));
dataGridView.Source.GroupDescriptions.Add(
    new GroupDescription("City"));

dataGridView.Source.RebuildView();   // required after changing GroupDescriptions

// Custom display text:
dataGridView.Source.GroupDescriptions.Add(new GroupDescription("HireDate")
{
    DisplayTextSelector = key => key is DateTime dt ? dt.ToString("yyyy") : key?.ToString() ?? ""
});
```

**Group summary rows (in XAML):**
```xml
<dg:DataGridView.GroupSummaryRows>
    <core:GroupSummaryRow Name="GroupTotals">
        <core:GroupSummaryRow.Columns>
            <core:SummaryColumnDescription PropertyName="Salary"
                                           SummaryType="Sum"
                                           Format="C0"
                                           Label="Group Total: " />
        </core:GroupSummaryRow.Columns>
    </core:GroupSummaryRow>
</dg:DataGridView.GroupSummaryRows>
```

---

## Table Summary Rows

**In XAML:**
```xml
<dg:DataGridView.TableSummaryRows>

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

</dg:DataGridView.TableSummaryRows>
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
dataGridView.TableSummaryRows.Add(summaryRow);
```

**Summary types:**
| `SummaryType` | Description |
|---|---|
| `Sum` | Total of all numeric values |
| `Average` | Mean of all numeric values |
| `Count` | Number of visible rows |
| `Min` | Minimum value |
| `Max` | Maximum value |
| `Custom` | Provide `CustomAggregator = (values) => ...` |

---

## Live data

```csharp
// Use ObservableCollection<T> for O(1) add/remove notifications:
Employees = new ObservableCollection<Employee>(data);

// Bind:
dataGridView.ItemsSource = Employees;

// Add/remove — grid updates automatically:
Employees.Add(new Employee { ... });
Employees.RemoveAt(index);

// For bulk changes, batch with BeginUpdate/EndUpdate:
dataGridView.Source.BeginUpdate();
for (var item in newItems) Employees.Add(item);
dataGridView.Source.EndUpdate();
```
