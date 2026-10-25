---
applyTo: "src/KumikoUI.Core/Models/**"
---

# KumikoUI — Models & Data

## `KumikoColumn` — Column definition

```csharp
new KumikoColumn
{
    Header          = "Name",
    PropertyName    = "Name",           // nested paths supported: "Address.City"
    ColumnType      = KumikoColumnType.Text,
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
    TextAlignment   = KumikoTextAlignment.Left,
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

| `KumikoColumnType` | Cell renderer | Inline editor |
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

## `KumikoSource` — Data pipeline

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
source.ApplySort(column, KumikoSortDirection.Ascending);

// Multi-column:
col1.SortDirection = KumikoSortDirection.Ascending; col1.SortOrder = 1;
col2.SortDirection = KumikoSortDirection.Descending; col2.SortOrder = 2;
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

## `KumikoStyle` — Theming

```csharp
// Built-in themes:
KumikoTheme.Create(KumikoThemeMode.Light)
KumikoTheme.Create(KumikoThemeMode.Dark)
KumikoTheme.Create(KumikoThemeMode.HighContrast)

// Dynamic per-cell styling:
style.CellStyleResolver = (item, column) =>
    item is Employee e && !e.IsActive
        ? new KumikoCellStyle { TextColor = new KumikoColor(180, 0, 0) }
        : null;

// Dynamic per-row styling:
style.RowStyleResolver = item =>
    item is Employee e && e.Salary > 200_000
        ? new KumikoRowStyle { BackgroundColor = new KumikoColor(255, 250, 220) }
        : null;
```

### `KumikoCellStyle` / `KumikoRowStyle` nullable properties

All properties are nullable — `null` means "inherit from grid-level `KumikoStyle`".
`KumikoCellStyle.Merge(primary, fallback)` combines two instances with `primary` taking precedence.

---

## Adding a new `KumikoStyle` property

1. Add property (with `///` XML doc) to `KumikoStyle` in `KumikoStyle.cs`.
2. Set value in `KumikoTheme.CreateDark()` and `KumikoTheme.CreateHighContrast()`.
3. Use in `KumikoRenderer.cs`.
4. Add `[Fact]` tests in `KumikoThemeTests.cs` for Dark and HighContrast values.

---

## Adding a new `KumikoColumnType`

1. Add enum value to `KumikoColumnType` in `KumikoColumn.cs`.
2. Create cell renderer in `src/KumikoUI.Core/Rendering/` (if display differs from text).
3. Create editor (`DrawnComponent`) in `src/KumikoUI.Core/Components/` (if needed).
4. Wire `CellEditorFactory`: `CreateEditor`, `GetEditorValue`, `ApplyThemeToEditor`.
5. Dispatch in `KumikoRenderer.cs` to the renderer.
