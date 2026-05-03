---
applyTo: "src/KumikoUI.Core/Models/**"
---

# KumikoUI — Models & Data

## `DataGridColumn` — Column definition

```csharp
new DataGridColumn
{
    Header          = "Name",
    PropertyName    = "Name",           // nested paths supported: "Address.City"
    ColumnType      = DataGridColumnType.Text,
    Width           = 180f,
    SizeMode        = ColumnSizeMode.Fixed,   // Fixed | Star | Auto
    StarWeight      = 1f,
    MinWidth        = 40f,
    MaxWidth        = float.MaxValue,
    IsReadOnly      = false,
    AllowSorting    = true,
    AllowFiltering  = true,
    AllowResize     = true,
    AllowReorder    = true,
    AllowTabStop    = true,
    FreezeMode      = ColumnFreezeMode.None,  // None | Left | Right
    Format          = "C2",                   // IFormattable format string
    TextAlignment   = GridTextAlignment.Left,
    CustomCellRenderer  = null,
    EditorDescriptor    = null,
    CustomEditorFactory = null,
    EditorItems         = null,
    EditorItemsString   = "A,B,C",            // XAML-friendly CSV for Picker/ComboBox
    CellStyle           = null,
    HeaderCellStyle     = null
};
```

### Column types

| `DataGridColumnType` | Cell renderer | Inline editor |
|---|---|---|
| `Text` | `TextCellRenderer` | `DrawnTextBox` |
| `Numeric` | `TextCellRenderer` | `DrawnTextBox` (numeric-only) |
| `Boolean` | `BooleanCellRenderer` | toggle in-place |
| `Date` | `TextCellRenderer` | `DrawnDatePicker` |
| `ComboBox` | `TextCellRenderer` | `DrawnComboBox` |
| `Picker` | `TextCellRenderer` | `DrawnScrollPicker` |
| `Image` | `ImageCellRenderer` | (not editable) |
| `Template` | `CustomCellRenderer` or text | `CustomEditorFactory` or `EditorDescriptor` |

---

## `DataGridSource` — Data pipeline

Manages filtering, sorting, grouping, and the flat view used by the renderer.

```csharp
source.GetCellValue(rowIndex, column)          // raw value
source.GetCellDisplayText(rowIndex, column)    // formatted string
source.SetCellValue(rowIndex, column, value)   // write-back
source.GetItem(rowIndex)                       // raw data item
source.RowCount                                // visible row count (after filter/group)
source.Columns                                 // column list

// Bulk updates:
source.BeginUpdate();
// ... many add/remove operations
source.EndUpdate();
```

### Sorting

```csharp
source.ApplySort(column);                         // cycles None → Asc → Desc → None
source.ApplySort(column, SortDirection.Ascending);

// Multi-column:
col1.SortDirection = SortDirection.Ascending; col1.SortOrder = 1;
col2.SortDirection = SortDirection.Descending; col2.SortOrder = 2;
source.ApplySorts();
```

### Filtering

```csharp
var filter = new FilterDescription(column)
{
    Condition1 = new FilterCondition
    {
        TextOperator = TextFilterOperator.Contains,
        Value        = "Engineering"
    }
};
source.ApplyFilter(column, filter);   // pass null to clear

// Numeric range:
var numFilter = new FilterDescription(numericColumn)
{
    Condition1      = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = 50000d },
    LogicalOperator = FilterLogicalOperator.And,
    Condition2      = new FilterCondition { NumericOperator = NumericFilterOperator.LessThanOrEqual, Value = 150000d }
};
```

### Grouping

```csharp
source.GroupDescriptions.Add(new GroupDescription("Department"));
source.GroupDescriptions.Add(new GroupDescription("City"));    // multi-level
source.RebuildView();   // required after changing GroupDescriptions

// Custom key display:
source.GroupDescriptions.Add(new GroupDescription("HireDate")
{
    DisplayTextSelector = key => key is DateTime dt ? dt.ToString("yyyy") : key?.ToString() ?? ""
});
```

### Summaries

```csharp
// Table-level:
var row = new TableSummaryRow("Totals", SummaryPosition.Bottom)
{
    Title   = "Totals",
    Columns =
    {
        new SummaryColumnDescription("Salary", SummaryType.Sum)   { Format = "C0" },
        new SummaryColumnDescription("Id",     SummaryType.Count) { Label = "Rows: " },
        new SummaryColumnDescription("Rating", SummaryType.Max)   { Label = "Best: " }
    }
};
```

`SummaryType` values: `Sum`, `Average`, `Count`, `Min`, `Max`, `Custom`.

---

## `DataGridStyle` — Theming

```csharp
// Built-in themes:
DataGridTheme.Create(DataGridThemeMode.Light)
DataGridTheme.Create(DataGridThemeMode.Dark)
DataGridTheme.Create(DataGridThemeMode.HighContrast)

// Dynamic per-cell styling:
style.CellStyleResolver = (item, column) =>
    item is Employee e && !e.IsActive
        ? new CellStyle { TextColor = new GridColor(180, 0, 0) }
        : null;

// Dynamic per-row styling:
style.RowStyleResolver = item =>
    item is Employee e && e.Salary > 200_000
        ? new RowStyle { BackgroundColor = new GridColor(255, 250, 220) }
        : null;
```

### `CellStyle` / `RowStyle` nullable properties

All properties are nullable — `null` means "inherit from grid-level `DataGridStyle`".
`CellStyle.Merge(primary, fallback)` combines two instances with `primary` taking precedence.

---

## Adding a new `DataGridStyle` property

1. Add property (with `///` XML doc) to `DataGridStyle` in `GridState.cs`.
2. Set value in `DataGridTheme.CreateDark()` and `DataGridTheme.CreateHighContrast()`.
3. Use in `DataGridRenderer.cs`.
4. Add `[Fact]` tests in `DataGridThemeTests.cs` for Dark and HighContrast values.

---

## Adding a new `DataGridColumnType`

1. Add enum value to `DataGridColumnType` in `DataGridColumn.cs`.
2. Create cell renderer in `src/KumikoUI.Core/Rendering/` (if display differs from text).
3. Create editor (`DrawnComponent`) in `src/KumikoUI.Core/Components/` (if needed).
4. Wire `CellEditorFactory`: `CreateEditor`, `GetEditorValue`, `ApplyThemeToEditor`.
5. Dispatch in `DataGridRenderer.cs` to the renderer.
