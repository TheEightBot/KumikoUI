<p align="center">
  <img src="images/logo.svg" alt="KumikoUI logo" width="120" />
  <h1 align="center">KumikoUI DataGrid</h1>
  <p align="center">
    A high-performance, fully canvas-drawn DataGrid for .NET — zero native controls, pixel-perfect on every platform.
  </p>
  <p align="center">
    <a href="#-getting-started">Getting Started</a> ·
    <a href="#-column-types">Columns</a> ·
    <a href="#-action-button-columns">Action Buttons</a> ·
    <a href="#-sorting--filtering">Sorting & Filtering</a> ·
    <a href="#-grouping">Grouping</a> ·
    <a href="#-editing">Editing</a> ·
    <a href="#-theming">Theming</a> ·
    <a href="#-architecture">Architecture</a>
  </p>
  <p align="center">
    <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" />
    <img alt="SkiaSharp" src="https://img.shields.io/badge/SkiaSharp-4.153.1-ff6b6b" />
    <img alt="Platforms" src="https://img.shields.io/badge/platforms-iOS%20%7C%20Android%20%7C%20macOS%20%7C%20Windows%20%7C%20Linux%20%7C%20WebAssembly-blue" />
    <img alt="License" src="https://img.shields.io/badge/license-MIT-green" />
    <a href="https://scorecard.dev/viewer/?uri=github.com/TheEightBot/KumikoUI"><img alt="OpenSSF Scorecard" src="https://api.scorecard.dev/projects/github.com/TheEightBot/KumikoUI/badge" /></a>
  </p>
  <p align="center">
    <a href="https://www.nuget.org/packages/KumikoUI.Maui"><img alt="KumikoUI.Maui on NuGet" src="https://img.shields.io/nuget/v/KumikoUI.Maui?label=KumikoUI.Maui&color=512BD4" /></a>
    <a href="https://www.nuget.org/packages/KumikoUI.SkiaSharp"><img alt="KumikoUI.SkiaSharp on NuGet" src="https://img.shields.io/nuget/v/KumikoUI.SkiaSharp?label=KumikoUI.SkiaSharp&color=512BD4" /></a>
    <a href="https://www.nuget.org/packages/KumikoUI.Core"><img alt="KumikoUI.Core on NuGet" src="https://img.shields.io/nuget/v/KumikoUI.Core?label=KumikoUI.Core&color=512BD4" /></a>
  </p>
</p>

---

Every visual element — cells, headers, editors, scrollbars, popups — is rendered directly on a SkiaSharp canvas. There are **zero native UIKit / AppKit / WinUI controls**, giving you identical, pixel-perfect output across iOS, Android, macOS Catalyst, and Windows.

## Contents

- [Features at a Glance](#-features-at-a-glance)
- [Getting Started](#-getting-started)
- [Column Types](#-column-types)
- [Action Button Columns](#-action-button-columns)
- [Column Sizing](#-column-sizing)
- [Frozen Columns & Rows](#-frozen-columns--rows)
- [Sorting & Filtering](#-sorting--filtering)
- [Grouping](#-grouping)
- [Summaries](#-summaries)
- [Editing](#-editing)
- [Selection](#-selection)
- [Keyboard Navigation](#-keyboard-navigation)
- [Row Drag & Drop](#-row-drag--drop)
- [Theming & Styling](#-theming--styling)
- [Custom Fonts](#-custom-fonts)
- [Scrolling & Performance](#-scrolling--performance)
- [Live Data](#-live-data)
- [Native AOT & Trimming](#-native-aot--trimming)
- [Architecture](#-architecture)
- [Sample Application](#-sample-application)
- [Requirements](#-requirements)
- [Contributing](#-contributing)
- [License](#-license)

---

<p align="center">
  <img src="images/main.png" alt="KumikoUI — all components" width="100%" />
</p>

---

## ✨ Features at a Glance

| Category | Highlights |
|---|---|
| **Rendering** | 100% SkiaSharp canvas, zero native controls, 60 fps |
| **Virtual scrolling** | Only visible rows/columns rendered — tested with 100K+ rows |
| **Column types** | Text, Numeric, Boolean, Date, ComboBox, Picker, ProgressBar, Image, Template |
| **Action buttons** | Pill-button columns with `ICommand` / callback support; activate on first tap |
| **Column sizing** | Fixed, Star (proportional), Auto (content-sized) |
| **Frozen columns** | Pin columns to left or right edge |
| **Frozen rows** | Pin top N rows to viewport |
| **Sorting** | Tap header to cycle asc/desc/none; multi-column |
| **Filtering** | Excel-style popup with search, value checklist, and sort controls |
| **Grouping** | Drag-and-drop group panel, expandable groups, nested groups |
| **Summaries** | Table-level and per-group aggregates (Sum, Avg, Count, Min, Max) |
| **Editing** | Configurable triggers (tap, double-tap, long-press, F2, typing); per-column override |
| **Selection** | Single / Multiple / Extended modes; Row or Cell unit |
| **Keyboard nav** | Arrows, Tab/Shift+Tab, Enter, Home/End, Page Up/Down |
| **Row drag & drop** | Handle column or full-row drag reorder |
| **Custom fonts** | Register any TTF/OTF typeface (CJK, icon fonts) via `SkiaFontRegistrar`; use by family name in `KumikoFont` |
| **Theming** | Built-in Light / Dark / HighContrast; fully customizable style object |
| **Momentum scrolling** | Physics-based inertial scroll with configurable friction |
| **Live data** | `INotifyPropertyChanged` and `ObservableCollection` O(1) updates |
| **Native AOT & trimming** | Warning-free under `PublishAot`; `[KumikoGridItem]` source-generated accessors or per-column delegates |
| **Column resizing** | Drag column border |

---

## 🚀 Getting Started

### 0. Install NuGet packages

For a .NET MAUI app, add **KumikoUI.Maui** (which transitively brings in `KumikoUI.Core` and `KumikoUI.SkiaSharp`):

```shell
dotnet add package KumikoUI.Maui
```

| Package | Description |
|---|---|
| [`KumikoUI.Maui`](https://www.nuget.org/packages/KumikoUI.Maui) | `KumikoDataGrid` MAUI control + MAUI hosting extensions |
| [`KumikoUI.SkiaSharp`](https://www.nuget.org/packages/KumikoUI.SkiaSharp) | SkiaSharp drawing context implementation |
| [`KumikoUI.Core`](https://www.nuget.org/packages/KumikoUI.Core) | Platform-agnostic layout, rendering pipeline, and models |

---

### 1. Register the KumikoUI

In `MauiProgram.cs`, call `UseSkiaKumikoUI()`:

```csharp
var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .UseSkiaKumikoUI();   // registers SkiaSharp KumikoUI services
```

### 2. Add XAML namespaces

```xml
<ContentPage
    xmlns:dg="clr-namespace:KumikoUI.Maui;assembly=KumikoUI.Maui"
    xmlns:core="clr-namespace:KumikoUI.Core.Models;assembly=KumikoUI.Core">
```

### 3. Declare the KumikoDataGrid

```xml
<dg:KumikoDataGrid x:Name="kumiko"
                 ItemsSource="{Binding Items}"
                 RowHeight="36"
                 HeaderHeight="40">
    <dg:KumikoDataGrid.Columns>
        <core:KumikoColumn Header="Name"   PropertyName="Name"     Width="180" />
        <core:KumikoColumn Header="Age"    PropertyName="Age"      Width="80"
                             ColumnType="Numeric" TextAlignment="Right" />
        <core:KumikoColumn Header="Active" PropertyName="IsActive" Width="80"
                             ColumnType="Boolean" />
    </dg:KumikoDataGrid.Columns>
</dg:KumikoDataGrid>
```

---

> **iOS and Mac Catalyst:** current Apple OS versions (macOS 27 and iOS 27 with the Xcode 27 toolchain) terminate an app at launch if it has not adopted the UIScene lifecycle. New .NET MAUI templates already include it: a `UIApplicationSceneManifest` in `Platforms/iOS/Info.plist` and `Platforms/MacCatalyst/Info.plist`, and a `SceneDelegate` class deriving from `MauiUISceneDelegate`. If your app was created from an older template, copy these from a fresh `dotnet new maui`. See `samples/SampleApp.Maui/Platforms` for a working setup.

## 📋 Column Types

Each column is a `KumikoColumn` with a `ColumnType` that controls both the read-only cell renderer and the inline editor.

| `ColumnType` | Cell Renderer | Editor | Notes |
|---|---|---|---|
| `Text` | Left-aligned text | `DrawnTextBox` | Default type |
| `Numeric` | Formatted number | `DrawnTextBox` (numeric) | Set `Format` (e.g. `"C0"`, `"N2"`) |
| `Boolean` | Checkbox glyph | `DrawnCheckBox` | Three-state (checked / unchecked / indeterminate) |
| `Date` | Formatted date | `DrawnDatePicker` | Calendar popup with month navigation |
| `ComboBox` | Selected value | `DrawnComboBox` | Searchable dropdown; set `EditorItemsString` |
| `Picker` | Selected value | `DrawnScrollPicker` | Mobile-style scroll wheel with physics snap |
| `Image` | Centered image | — (read-only) | Aspect-ratio preserving |
| `ProgressBar` | Progress bar | `DrawnProgressBar` | Interactive slider when editable |
| `Template` | Custom | Custom (`EditorDescriptor`) | Provide your own `ICellRenderer` |

### Column declaration example

```xml
<core:KumikoColumn Header="Department"
                     PropertyName="Department"
                     Width="140"
                     ColumnType="ComboBox"
                     EditorItemsString="Engineering,Marketing,Sales,HR,Finance" />

<core:KumikoColumn Header="Salary"
                     PropertyName="Salary"
                     Width="120"
                     ColumnType="Numeric"
                     Format="C0"
                     TextAlignment="Right" />

<core:KumikoColumn Header="Rating"
                     PropertyName="Rating"
                     Width="120"
                     ColumnType="ProgressBar" />
```

### Read-only and tab behavior

```xml
<core:KumikoColumn Header="Id" PropertyName="Id"
                     IsReadOnly="True"
                     AllowTabStop="False" />
```

---

## 🖱️ Action Button Columns

Action button columns render a row of clickable pill-buttons directly inside every cell. Any number of buttons can be declared; they are distributed with equal widths across the cell area. Buttons support both `ICommand` (for MVVM data-binding) and an `Action` callback (for code-behind).

Action buttons use the **Template** column type with `ActionButtonsCellRenderer` as the `CustomCellRenderer`. The renderer also implements `ICellEditorProvider`, so no separate `CustomEditorFactory` is required — the same button definitions handle both display and interaction.

> **Tip:** Set the column's `EditTriggers="SingleTap"` so buttons respond on the first touch, even when the grid's default trigger is `DoubleTap`. See [Per-column edit trigger override](#per-column-edit-trigger-override).

### XAML declaration (MVVM)

Set `PropertyName=""` so the full row item is passed as the value to both the renderer and the editor.
`MauiActionButtonDefinition` is a `BindableObject` that supports `{Binding}` on every property, including `Command`.

```xml
xmlns:dg="clr-namespace:KumikoUI.Maui;assembly=KumikoUI.Maui"
xmlns:render="clr-namespace:KumikoUI.Core.Rendering;assembly=KumikoUI.Core"

<dg:KumikoDataGrid x:Name="actionsGrid" ...>
    <dg:KumikoDataGrid.Columns>
        <core:KumikoColumn Header="Actions"
                             PropertyName=""
                             ColumnType="Template"
                             Width="200"
                             AllowSorting="False"
                             AllowFiltering="False"
                             AllowTabStop="False"
                             EditTriggers="SingleTap">
            <core:KumikoColumn.CustomCellRenderer>
                <render:ActionButtonsCellRenderer>
                    <render:ActionButtonsCellRenderer.Buttons>
                        <dg:MauiActionButtonDefinition
                            Label="Details"
                            BackgroundColor="13,110,253"
                            Command="{Binding Path=BindingContext.ViewDetailsCommand,
                                              Source={x:Reference actionsGrid}}" />
                        <dg:MauiActionButtonDefinition
                            Label="Delete"
                            BackgroundColor="220,53,69"
                            Command="{Binding Path=BindingContext.DeleteCommand,
                                              Source={x:Reference actionsGrid}}" />
                    </render:ActionButtonsCellRenderer.Buttons>
                </render:ActionButtonsCellRenderer>
            </core:KumikoColumn.CustomCellRenderer>
        </core:KumikoColumn>
    </dg:KumikoDataGrid.Columns>
</dg:KumikoDataGrid>
```

> **Note:** `MauiActionButtonDefinition` is not in the visual tree, so it does **not** inherit `BindingContext` automatically. Use `Source={x:Reference …}` or `Source={RelativeSource …}` to point bindings at the correct source object.

### Code-behind factory

Use `ActionButtonDefinition` (in `KumikoUI.Core`) when you want a code-behind closure per row:

```csharp
actionsColumn.CustomEditorFactory = (value, bounds) => new DrawnActionButtons
{
    Bounds  = bounds,
    RowItem = value,
    Buttons =
    [
        new ActionButtonDefinition
        {
            Label           = "Edit",
            BackgroundColor = new KumikoColor(13, 110, 253),
            Command         = vm.EditCommand   // row item is CommandParameter by default
        },
        new ActionButtonDefinition
        {
            Label           = "Delete",
            BackgroundColor = new KumikoColor(220, 53, 69),
            Action          = () => vm.DeleteEmployee((Employee)value!)
        }
    ]
};
```

### Button properties

| Property | Type | Description |
|---|---|---|
| `Label` | `string` | Text displayed on the button face |
| `BackgroundColor` | `KumikoColor` | Pill background color (`R,G,B` string format in XAML) |
| `TextColor` | `KumikoColor` | Label text color (default: white) |
| `Command` | `ICommand?` | Executed on tap; row item is the default `CommandParameter` |
| `CommandParameter` | `object?` | Explicit command parameter; overrides the row item default |
| `Action` | `Action?` | Code-behind callback; executed in addition to `Command` if both are set |

Layout properties on `DrawnActionButtons` / `ActionButtonsCellRenderer`:

| Property | Default | Description |
|---|---|---|
| `CornerRadius` | `5f` | Button pill corner radius (pixels) |
| `ButtonSpacing` | `6f` | Horizontal gap between buttons (pixels) |
| `CellPadding` | `4f` | Inset from all four cell edges (pixels) |

---

## 📐 Column Sizing

Columns support three sizing modes controlled by the `SizeMode` property.

| `SizeMode` | Behavior |
|---|---|
| `Fixed` | Exact pixel width set via `Width` (default) |
| `Auto` | Measures header text + visible cell content; respects a `MinWidth`/`MaxWidth` clamp |
| `Star` | Weighted share of remaining space after Fixed and Auto columns; set `Width="*"` or `"2*"` |

Modes can be mixed freely — the layout engine resolves them in a single pass per frame.

---

## 📌 Frozen Columns & Rows

### Frozen columns

Columns can be pinned to the left or right edge and will never scroll out of view.

```xml
<!-- Pin to left (default freeze side) -->
<core:KumikoColumn Header="Id"    PropertyName="Id"    IsFrozen="True" />
<core:KumikoColumn Header="Name"  PropertyName="Name"  IsFrozen="True" />

<!-- Pin to right -->
<core:KumikoColumn Header="Total" PropertyName="Total" FreezeMode="Right" />
```

The renderer uses 3-pass clip regions so frozen columns naturally overlay scrollable content with correct z-ordering.

### Frozen rows

Pin the first N rows so they remain visible while the rest of the data scrolls:

```xml
<dg:KumikoDataGrid FrozenRowCount="2" ... />
```

---

## 🔀 Sorting & Filtering

### Sorting

Tap any column header to sort. The indicator cycles through **Ascending → Descending → None**. Sort state is set through the data source (column `SortDirection` and `SortOrder` are read-only):

```csharp
// Sort by one column:
kumiko.DataSource.SetSort(lastNameColumn, KumikoSortDirection.Ascending);

// Multi-column sort: the order is the column's place in the chain (1 = primary).
kumiko.DataSource.SetSort(departmentColumn, KumikoSortDirection.Ascending, 1);
kumiko.DataSource.SetSort(lastNameColumn, KumikoSortDirection.Descending, 2);

// Remove one column from the sort, or clear it entirely:
kumiko.DataSource.SetSort(departmentColumn, KumikoSortDirection.None);
kumiko.DataSource.ClearSort();
```

Several changes can be batched so the view is rebuilt and repainted once:

```csharp
kumiko.DataSource.BeginUpdate();
kumiko.DataSource.SetSort(lastNameColumn, KumikoSortDirection.Ascending);
kumiko.DataSource.SetFilter(item => ((Employee)item).IsActive);
kumiko.DataSource.AddGroupDescription(new GroupDescription("Department"));
kumiko.DataSource.EndUpdate();
```

`KumikoSource.ViewChanged` reports every change once, with its kind (`Redraw`, `Rebuild`, `Insert`, `Remove`) and `MapRow` to follow a row from before the change to after it. `ScrollState.ScrollChanged` reports scrolling.

Sorting, grouping, filtering and Min/Max summaries share one comparer (`KumikoSource.Comparer`, a `KumikoComparer`) that never throws: nulls come first, numbers compare by value across numeric types, dates and times chronologically, other values of one type through `IComparable`, and anything else by its text. Sorting is stable, and groups are ordered by their key (1, 2, 10), not their header text. Text compares in the source's culture, ignoring case by default; both rules also apply to the filter popup's value list and search and to text filter conditions:

```csharp
kumiko.DataSource.Culture = CultureInfo.GetCultureInfo("de-DE"); // formatting, text order, filter parsing (default: current culture)
kumiko.DataSource.StringCompareOptions = CompareOptions.Ordinal;  // case-sensitive ordinal text (default: IgnoreCase)
```

A column's `CustomComparer` replaces the comparer for that column's sort and grouping. Template and Image columns, and columns with neither a `PropertyName` nor a `ValueAccessor`, are not sortable unless `AllowSorting` is set.

### Filtering

Every column header displays a filter icon. Tapping it opens an **Excel-style filter popup** that provides:

- Full-text search across all values in that column
- Value checklist (check/uncheck individual values)
- Quick sort buttons (A→Z / Z→A) inside the popup

Filters are applied immediately on close and stacked across columns. Remove a filter by opening the popup and selecting all values.

---

## 🗂️ Grouping

Add one or more `GroupDescription` objects to enable a collapsible group hierarchy with an interactive drag-and-drop group panel above the header.

```csharp
kumiko.DataSource.AddGroupDescription(new GroupDescription("Department"));
kumiko.DataSource.AddGroupDescription(new GroupDescription("Level"));
```

Groups are expanded by default. You can bulk-expand or collapse:

```csharp
kumiko.DataSource.ExpandAllGroups();
kumiko.DataSource.CollapseAllGroups();
```

Remove grouping:

```csharp
kumiko.DataSource.ClearGroupDescriptions();
```

Group summaries (aggregate values per group) are declared with `AddGroupSummaryRow()` — see [Summaries](#-summaries) below.

---

## Σ Summaries

Summaries appear as pinned rows at the top or bottom of the grid (table summaries) or at the bottom of each group (group summaries).

### Table summaries (XAML)

```xml
<dg:KumikoDataGrid.TableSummaryRows>
    <core:TableSummaryRow Name="Totals" Position="Bottom" Title="Totals">
        <core:TableSummaryRow.Columns>
            <core:SummaryColumnDescription PropertyName="Salary"
                                           SummaryType="Sum"   Format="C0" />
            <core:SummaryColumnDescription PropertyName="Id"
                                           SummaryType="Count" Label="Rows: " />
            <core:SummaryColumnDescription PropertyName="Salary"
                                           SummaryType="Average" Format="C0"
                                           Label="Avg: " />
        </core:TableSummaryRow.Columns>
    </core:TableSummaryRow>
</dg:KumikoDataGrid.TableSummaryRows>
```

### Group summaries (code)

```csharp
kumiko.DataSource.AddGroupSummaryRow(new SummaryDescription
{
    Columns =
    {
        new SummaryColumnDescription { PropertyName = "Salary", SummaryType = SummaryType.Sum,   Format = "C0" },
        new SummaryColumnDescription { PropertyName = "Salary", SummaryType = SummaryType.Average, Format = "C0", Label = "Avg: " }
    }
});
```

### Available summary types

`Sum` · `Average` · `Count` · `Min` · `Max`

---

## ✏️ Editing

### Edit triggers

Configure which gestures or keys open the inline editor:

```xml
<dg:KumikoDataGrid EditTriggers="DoubleTap,F2Key,Typing"
                 EditTextSelectionMode="SelectAll"
                 DismissKeyboardOnEnter="True" />
```

| Trigger | Description |
|---|---|
| `SingleTap` | Single tap on a cell |
| `DoubleTap` | Double-tap (default) |
| `LongPress` | Long-press gesture |
| `F2Key` | F2 keyboard shortcut |
| `Typing` | Start typing immediately to replace cell content |

Triggers are flags — combine freely: `"DoubleTap,F2Key,Typing"`.

### Per-column edit trigger override

Each column can override the grid-level `EditTriggers` independently via its own `EditTriggers` property. When set, that column uses its own trigger flags; when `null` (the default), the column inherits the grid-level setting.

```xml
<dg:KumikoDataGrid EditTriggers="DoubleTap, F2Key, Typing">
    <dg:KumikoDataGrid.Columns>

        <!-- Most accessible: single-tap, F2, or typing to edit -->
        <core:KumikoColumn Header="Name" PropertyName="Name"
                             EditTriggers="SingleTap, F2Key, Typing" />

        <!-- ComboBox: double-tap or F2 only (no accidental typing edits) -->
        <core:KumikoColumn Header="Department" PropertyName="Department"
                             ColumnType="ComboBox"
                             EditorItemsString="Engineering,Marketing,Finance,HR"
                             EditTriggers="DoubleTap, F2Key" />

        <!-- Salary: keyboard-only — protects financial data from pointer gestures -->
        <core:KumikoColumn Header="Salary" PropertyName="Salary"
                             ColumnType="Numeric" Format="C0"
                             EditTriggers="F2Key" />

        <!-- Action buttons always activate on first tap -->
        <core:KumikoColumn Header="Actions" PropertyName=""
                             ColumnType="Template"
                             EditTriggers="SingleTap" />

    </dg:KumikoDataGrid.Columns>
</dg:KumikoDataGrid>
```

Set `EditTriggers="None"` to suppress all gesture/keyboard triggers for a column without making it fully read-only via `IsReadOnly`.

### Boolean cells

A Boolean cell toggles in place instead of opening an editor:
- a single tap on the check box toggles it; a tap elsewhere in the cell only selects, unless the column has the `SingleTap` trigger, in which case any tap in the cell toggles;
- a double-tap toggles once: the second tap toggles with the `DoubleTap` trigger only when the first tap didn't;
- a long press toggles with the `LongPress` trigger, and `Space` toggles the current Boolean cell;
- a column that is read-only or has `EditTriggers="None"` never toggles.

A toggle goes through the same validation as an edit (see [Validation](#validation)).

### Edit text selection

```xml
<!-- Select all existing text when the editor opens -->
<dg:KumikoDataGrid EditTextSelectionMode="SelectAll" />

<!-- Or keep cursor at end -->
<dg:KumikoDataGrid EditTextSelectionMode="End" />
```

### Commit / cancel

- **Enter** — commit and move to the next row
- **Tab / Shift+Tab** — commit and move to the next/previous editable cell; at the last (first) one, commit and let focus leave the grid
- **Escape** — cancel and restore the original value

An open edit belongs to its item and column, not to a row number. If the rows move underneath it (a background collection change, a programmatic sort or filter), the editor moves with the item and the commit writes to that item; if the item is filtered out, collapsed or removed, the edit is canceled. Tapping a header to sort, opening a filter, toggling a group or starting a drag commits the edit first; when the value is invalid the gesture is ignored and the editor stays open with the error. The editor follows its cell while the grid scrolls and is hidden while the cell is out of view.

### Validation

An edit is written only when the user changed the value. Opening an editor and leaving it never touches the cell:
- a combo box or picker whose cell value isn't in its list opens with nothing selected;
- a date picker opens empty on an empty cell, and keeps the cell's time of day when a new date is picked.

A custom editor takes part by implementing `IValueEditor` (`Value`, `HasValue`, `IsDirty`); one that doesn't is never written.

When a changed value is committed (Enter, Tab, leaving the cell), it goes through four steps:

1. **Conversion.** The editor's value is converted to the property's type, in `KumikoSource.Culture`:
   - every numeric type, with whole numbers only for integer types, and in range;
   - `decimal` at full precision;
   - `bool`, `char`, `Guid`, and enums (by name, or by a defined number);
   - `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` and `TimeSpan`;
   - `string`, and the nullable form of each type.

   Blank text clears a nullable property and is an error for a non-nullable one. Text that doesn't convert is an error ("Enter a number.", "Enter a whole number.", "Enter a valid date.", …). The conversion is `KumikoValueConverter.TryConvert`, which you can call yourself.
2. **The grid's validator.** `EditSession.CellValidator` receives the converted value.
3. **The write.** A setter that throws is an error, and its exception message is shown.
4. **The item's own validation.** `IDataErrorInfo` and `INotifyDataErrorInfo` are checked with the new value in place. An error writes the original value back.

```csharp
grid.EditSession.CellValidator = (row, column, value) => value switch
{
    int quantity when quantity > 100 => CellValidationResult.Error("At most 100."),
    int quantity when quantity > 50 => CellValidationResult.Warning("That's a large order."),
    _ => CellValidationResult.Valid,
};

grid.EditSession.ValidationFailed += (_, e) =>
    ShowStatus($"{e.Column.Header}: {e.Message} ({e.Severity})");
```

What each result does:

| Result | Editor | Item | `ValidationFailed` |
|---|---|---|---|
| Error | stays open, with a red outline, an icon and the message under the cell | unchanged | raised, `Committed = false` |
| Warning, first commit | stays open, with an orange outline and the message | unchanged | raised, `Committed = false` |
| Warning, same value committed again | closes | written | raised, `Committed = true` |
| Info | closes | written | raised, `Committed = true` |

`CellValueChanged` is raised only for a value that was written, after `CellEndEdit`. Changing the value in the editor clears the message. Gestures that commit first are ignored while the value is refused: sorting, filtering, grouping, a tap or long press on another cell, Tab, Enter, and the navigation keys. The selection stays and the editor stays open and focused. `EditSession.BeginEdit` while an edit is open commits it first and returns false when that is refused. Toggling a boolean cell goes through the same validation.

The editor shows numbers and dates in the grid's culture (`3,5` under `de-DE`), so the text parses back to the same value.

`KumikoSource.SetCellValue` converts in the same way and returns whether the value was written. A value that can't be written changes nothing and raises no change.

### Custom editors

For `Template` columns supply an `EditorDescriptor`:

```csharp
var column = new KumikoColumn
{
    Header = "Rating",
    PropertyName = "Rating",
    ColumnType = KumikoColumnType.Template,
    EditorDescriptor = new NumericUpDownEditorDescriptor { Min = 0, Max = 10, Step = 1 }
};
```

To create the editor yourself, set `EditorFactory`. Its `CellEditorContext` carries the value, the cell bounds, the grid's culture and the character typed to open the edit (`InitialCharacter`, null for a double-tap or F2). `EditorDescriptor.CreateEditor(CellEditorContext)` and `ICellEditorProvider.CreateEditor(CellEditorContext)` receive the same context; the older `(value, bounds)` forms keep working but never see the character.

---

## ☑️ Selection

### Selection mode

```xml
<dg:KumikoDataGrid GridSelectionMode="Multiple" />
```

| `KumikoSelectionMode` | Behavior |
|---|---|
| `Single` | One item at a time |
| `Multiple` | Each tap toggles the item |
| `Extended` | Click + Shift/Ctrl range/toggle (desktop-style) |

Selection is by row and tracks **items**, not row numbers: after a sort, filter, regroup or collection change the same items stay selected (items that are filtered out or removed leave the selection), and the current cell stays on its item and column. Group header and summary rows are never selected; Ctrl+A and Shift ranges skip them. Cell selection is planned for 1.x (#108).

A tap on the empty part of the rows area (below the last row or right of the last column) commits an open edit and clears the selection and the current cell. Set `ClearSelectionOnEmptyAreaTap="False"` to keep the selection.

### Reading and changing selections (code)

```csharp
var selection = kumiko.Selection;
IReadOnlySet<object> items = selection.SelectedItems;  // the selected data items
IReadOnlySet<int> rows = selection.SelectedRows;       // their visible rows right now
object? current = selection.CurrentItem;               // the current cell's item

selection.Select(employee);          // or Deselect(employee)
selection.SelectRange(0, 9);         // data rows 0–9
selection.SelectAll();
selection.Clear();

selection.SelectionChanged += (_, e) =>
{
    // e.AddedItems, e.RemovedItems, e.SelectedRows, e.CurrentCell
};
```

### Selection in MVVM

Both hosts (MAUI and Uno) expose the selection as bindable properties, so a view model never reads row indices:

```xml
<dg:KumikoDataGrid ItemsSource="{Binding Employees}"
                   SelectedItem="{Binding SelectedEmployee, Mode=TwoWay}"
                   SelectedItems="{Binding SelectedEmployees, Mode=OneWay}"
                   SelectedIndex="{Binding SelectedRow, Mode=TwoWay}"
                   SelectionChangedCommand="{Binding SelectionChangedCommand}" />
```

| Property / event | Meaning |
|---|---|
| `SelectedItem` (two-way) | The primary selected item: the current cell's item while it is selected, otherwise the item selected last; null when nothing is selected. Setting it selects only that item and moves the current cell to it. |
| `SelectedItems` (`IList`) | The selected items. By default an `ObservableCollection<object>` the grid owns and keeps in sync. Bind your own list to have the grid select its items and keep it updated; if it implements `INotifyCollectionChanged`, adding or removing items selects and deselects in the grid. Set null to go back to the owned collection. |
| `SelectedIndex` (two-way) | The visible row of `SelectedItem`, or -1; it follows the item through sort and filter. Setting it selects the item at that row. |
| `CurrentCell` (two-way) | The keyboard-focus cell as a `CellPosition` (visible row and column). |
| `SelectionChanged` | `KumikoSelectionChangedEventArgs` with `AddedItems` and `RemovedItems`; the properties above are already up to date. |
| `SelectionChangedCommand` | Executed on every selection change with `SelectionChangedCommandParameter`, or the event arguments when that is null. |

Selection is by item, so all of this keeps working after a sort, filter or regroup. Setting `SelectedItem` to an item that is not visible, or any value in `None` mode, leaves the selection empty. A change made on one side is applied to the other without echoing back, so two-way bindings do not loop. On Uno, set `Mode=TwoWay` explicitly (WinUI bindings default to one-way).

---

## ⌨️ Keyboard Navigation

Full keyboard navigation is supported on all platforms (iOS/macOS via `UIKeyCommand`, Android via hidden `Entry` with IME diffing, Windows via hidden `Entry`).

| Key | Action |
|---|---|
| `Arrow keys` | Move the current cell one cell/row (`Shift` extends the row selection) |
| `Tab` | Move to the next editable cell (wraps rows) and edit it; past the last one, focus leaves the grid |
| `Shift+Tab` | Move to the previous editable cell and edit it; before the first one, focus leaves the grid |
| `Enter` | Commit the edit and move down / open the editor on the current cell (if `F2Key` trigger enabled) |
| `Escape` | Cancel the edit / clear the selection |
| `F2` | Open inline editor (if `F2Key` trigger enabled) |
| `Space` | Toggle the current Boolean cell / toggle the current row's selection |
| `Home` / `End` | First / last cell in row |
| `Ctrl+Home` / `Ctrl+End` | First / last cell in grid |
| `Page Up` / `Page Down` | Move one page of visible rows |
| `Ctrl+A` | Select all rows |
| Typing a character | Open the editor with that character (if `Typing` trigger enabled) |

The current cell is always scrolled fully into view, clear of the grouping panel, header, top summaries, frozen rows and columns, and the pinned bottom summary footer. Tab stops are visible columns that are not read-only and allow tab stops (`AllowTabStop`); Tab moves to a column whose `EditTriggers` is `None` without opening its editor. When Tab or Shift+Tab has no further cell to go to, or nothing is current, the grid leaves the key unhandled and the host moves focus to the next control, so the grid is never a keyboard trap. Enter and Escape are also left to the page when the grid has nothing to do with them.

---

## ↕️ Row Drag & Drop

Rows can be reordered by the user via two modes.

### Drag handle column

A dedicated grab-handle column appears as the first (or last) column:

```csharp
var style = kumiko.GridStyle;
style.ShowRowDragHandle = true;
style.RowDragHandlePosition = DragHandlePosition.Left;  // or Right
kumiko.GridStyle = style;
```

### Full-row drag

Long-press any row to begin dragging (no handle column required):

```csharp
style.AllowRowDragDrop = true;
style.ShowRowDragHandle = false;
kumiko.GridStyle = style;
```

A visual overlay tracks the dragged row. The row list is updated on drop.

---

## 🎨 Theming & Styling

### Built-in themes

```xml
<dg:KumikoDataGrid Theme="Light" />        <!-- default -->
<dg:KumikoDataGrid Theme="Dark" />
<dg:KumikoDataGrid Theme="HighContrast" />
```

### Custom style

Every color, font, size, and line width is configurable through the `KumikoStyle` object:

```csharp
var style = kumiko.GridStyle;

// Colors
style.BackgroundColor        = new KumikoColor(0.95f, 0.95f, 0.97f);
style.AlternateRowColor      = new KumikoColor(0.90f, 0.90f, 0.93f);
style.HeaderBackgroundColor  = new KumikoColor(0.20f, 0.40f, 0.80f);
style.HeaderTextColor        = KumikoColor.White;
style.SelectedRowColor       = new KumikoColor(0.85f, 0.92f, 1.00f);
style.GridLineColor          = new KumikoColor(0.80f, 0.80f, 0.80f);
style.FrozenDividerColor     = new KumikoColor(0.40f, 0.40f, 0.40f);

// Typography
style.CellFontSize    = 14f;
style.HeaderFontSize  = 14f;
style.CellFontBold    = false;
style.HeaderFontBold  = true;

// Grid lines
style.ShowHorizontalGridLines = true;
style.ShowVerticalGridLines   = false;

kumiko.GridStyle = style;
```

### Per-cell dynamic style

Apply a `CellStyleResolver` delegate to the `KumikoStyle` to override the style of individual cells at render time:

```csharp
var style = kumiko.GridStyle;
style.CellStyleResolver = (item, column) =>
{
    if (column.PropertyName == "Salary" && item is Employee e && e.Salary > 100_000)
        return new KumikoCellStyle { TextColor = new KumikoColor(0.8f, 0.1f, 0.1f), Bold = true };
    return null;
};
kumiko.GridStyle = style;
```

Style resolution cascades: **per-cell resolver → column `KumikoCellStyle` → grid `KumikoStyle`**.

---

## 🔤 Custom Fonts

`SkiaFontRegistrar` (in `KumikoUI.SkiaSharp`) is a static registry for custom `SKTypeface` instances. Register any TrueType or OpenType font by a family name string, and `SkiaDrawingContext` will resolve it by that name whenever a `KumikoFont` requests that family — bypassing the system font manager entirely.

This is the recommended fix for two common problems on Android:
- **CJK text (Japanese, Chinese, Korean)** renders as empty boxes because the Android system font manager doesn't surface CJK typefaces to SkiaSharp.
- **Icon-font glyphs** (Material Icons, Font Awesome, etc.) appear garbled for the same reason.

### Registering fonts at startup

Register fonts in `MauiProgram.cs` before the MAUI UI loop starts. Place font files in `Resources/Raw/` so they are bundled with the app.

```csharp
// MauiProgram.cs
using KumikoUI.SkiaSharp;

public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder.UseMauiApp<App>().UseSkiaKumikoUI();

    // Register CJK font
    using var jpStream = await FileSystem.OpenAppPackageFileAsync("NotoSansJP-Regular.ttf");
    SkiaFontRegistrar.RegisterTypefaceFromStream("NotoSansJP", jpStream);

    // Register icon font
    using var icStream = await FileSystem.OpenAppPackageFileAsync("MaterialIcons-Regular.ttf");
    SkiaFontRegistrar.RegisterTypefaceFromStream("MaterialIcons", icStream);

    return builder.Build();
}
```

> **Note:** `RegisterTypefaceFromStream` takes ownership of the created typeface and disposes it when `SkiaFontRegistrar.Clear()` is called. The stream may be disposed after the call returns. Use `RegisterTypeface(family, typeface)` instead when you want to manage the typeface lifetime yourself.

### Using registered fonts in the grid

Reference the registered family name in `KumikoFont` on a `KumikoStyle` or directly in a custom renderer:

```csharp
// Apply a CJK font to all headers and cells
var style = kumiko.GridStyle;
style.HeaderFont = new KumikoFont("NotoSansJP", 14, bold: true);
style.CellFont   = new KumikoFont("NotoSansJP", 13);
kumiko.GridStyle = style;
```

### Icon-font columns

Store the Unicode code-point string as the cell value and render it with a custom `ICellRenderer` that specifies the icon font family:

```csharp
// Renderer that draws cell text using a named icon-font typeface
public class IconFontCellRenderer(string fontFamily, float fontSize) : ICellRenderer
{
    public void Render(IDrawingContext ctx, KumikoRect cellRect, object? value,
        string displayText, KumikoColumn column,
        KumikoStyle style, bool isSelected, KumikoCellStyle? cellStyle = null)
    {
        if (string.IsNullOrEmpty(displayText)) return;

        ctx.DrawTextInRect(displayText, cellRect, new KumikoPaint
        {
            Color       = isSelected ? style.SelectionTextColor : style.CellTextColor,
            Font        = new KumikoFont(fontFamily, fontSize),
            IsAntiAlias = true
        }, KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);
    }
}
```

Attach it to a column:

```csharp
// "\uE876" = checkmark, "\uE5CD" = close, "\uE002" = warning in MaterialIcons
new KumikoColumn
{
    Header             = "Status",
    PropertyName       = nameof(Item.StatusIcon),   // returns e.g. "\uE876"
    Width              = 60,
    IsReadOnly         = true,
    TextAlignment      = KumikoTextAlignment.Center,
    CustomCellRenderer = new IconFontCellRenderer("MaterialIcons", 22f)
}
```

### API reference

| Method | Description |
|---|---|
| `SkiaFontRegistrar.RegisterTypefaceFromStream(family, stream)` | Creates a typeface from a stream and registers it. Registrar owns the typeface lifetime. |
| `SkiaFontRegistrar.RegisterTypeface(family, typeface)` | Registers an existing `SKTypeface`. Caller retains ownership. |
| `SkiaFontRegistrar.TryGetTypeface(family, out typeface)` | Looks up a registered typeface by family name. |
| `SkiaFontRegistrar.Clear()` | Removes all registrations and disposes owned typefaces. |

---

## ⚡ Scrolling & Performance

### Virtual scrolling

Only the rows and columns currently within the viewport are measured and drawn. The visible row range is computed as:

```
firstRow = scrollOffsetY / rowHeight
lastRow  = firstRow + viewportHeight / rowHeight + 1
```

Column visibility is resolved by `KumikoLayoutEngine` walking cumulative widths from the scroll offset.

### Momentum scrolling

Touch-based scrolling uses **physics-based inertia** — a velocity tracker samples recent pointer deltas and the `InertialScroller` decelerates with configurable friction once the finger lifts. The deceleration is frame-normalized so it behaves consistently at any refresh rate.

### PaintCache

~60 `KumikoPaint` objects are pre-computed once per frame from `KumikoStyle` so there are zero per-cell paint allocations during rendering.

### SkiaDrawingContext caching

`SKTypeface`, `SKFont`, and `SKPaint` instances are cached by value-type keys and disposed at frame end — no GC pressure from native Skia objects.

### Incremental collection updates

When `ItemsSource` is an `ObservableCollection` and no sort, filter, group, or summary transforms are active, add/remove operations are handled in **O(1)** without rebuilding the entire flat view.

---

## 📡 Live Data

The grid automatically reacts to standard .NET data-change notifications.

### ObservableCollection

```csharp
var employees = new ObservableCollection<Employee>(initialList);
kumiko.ItemsSource = employees;

// These automatically update the grid:
employees.Add(new Employee { Name = "Alice" });
employees.RemoveAt(3);
```

Items may be of mixed runtime types (for example `Dog` and `Cat` in a `List<Animal>`): each column's property is resolved per item, and an item without that property shows an empty cell. `null` entries are kept in the collection but never become rows: they are skipped like items a filter rejects, so they are not sorted, grouped, summarized or listed in filter values, and adding or removing one keeps the grid correct.

### INotifyPropertyChanged

Individual cell values update in real time when the bound object implements `INotifyPropertyChanged`:

```csharp
public class Employee : INotifyPropertyChanged
{
    private decimal _salary;
    public decimal Salary
    {
        get => _salary;
        set { _salary = value; PropertyChanged?.Invoke(this, new(nameof(Salary))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
```

A change to a property that a sort, column filter, grouping or summary uses re-sorts, re-filters, regroups or recomputes; any other change only repaints. With a `SetFilter` predicate set, every change re-filters, since the predicate may read any property. A `null` or empty property name means every property changed. Items added to or removed from an `ObservableCollection` honor the active filters at once.

### Nested property paths

`PropertyName` supports dotted paths for nested objects:

```xml
<core:KumikoColumn Header="City" PropertyName="Address.City" Width="120" />
```

Only the row item is observed: for a sorted, filtered, grouped or summarized `Address.City` to update when the address changes, the item must raise `PropertyChanged` for `Address` (or with an empty name). Each path segment is resolved once per item type and cached. A `null` value part-way along the path reads as an empty cell, and an edit to it is ignored. How the properties are read is covered in [Native AOT & Trimming](#-native-aot--trimming).

---

## 🧩 Native AOT & Trimming

All KumikoUI packages are marked `IsAotCompatible` and build with no trimming or Native AOT warnings. The grid reads and writes item properties named by `PropertyName` through, in order:

1. **Column delegates**: `ValueAccessor` and `ValueSetter` on `KumikoColumn` (and `EditorDisplaySelector` / `EditorValueSelector` for ComboBox items).
2. **Generated accessors**: types marked `[KumikoGridItem]`. The `KumikoUI.Core` package includes a source generator that writes typed accessors for their public properties and registers them when the assembly loads.
3. **Registered providers**: `KumikoAccessorRegistry.Register(type, provider)` or `AddProvider(provider)` with your own `IKumikoAccessorProvider`, for example to read dictionary entries or data rows.
4. **Reflection**: everything else. This fallback is turned off automatically for `PublishAot=true` and `TrimMode=full`, because those publishes remove the metadata it reads. Partial trimming (the .NET MAUI default) keeps it on.

With reflection off, a property that no tier can serve reads as `null`, edits to it are ignored, and a warning naming the type and property is traced once.

```csharp
using KumikoUI.Core.Data;

[KumikoGridItem]          // generated accessors for Name, Age, Address, …
public class Employee
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public Address? Address { get; set; }
}

[KumikoGridItem]          // mark every type a path walks through ("Address.City")
public class Address
{
    public string City { get; set; } = "";
}

// Or per column, without attributes:
var bonus = new KumikoColumn
{
    Header = "Bonus",
    PropertyName = "Bonus",                                   // still names the column for summaries and grouping
    ValueAccessor = item => ((Employee)item).Age * 10m,
    ValueSetter = (item, value) => { /* write it back */ },
};
```

Generated accessors and delegates are faster than the expression trees the grid compiled before, and need no runtime code generation. `bench/KumikoUI.Benchmarks` measures them. The generator reports `KUMIKO001` for generic types and `KUMIKO002` for types it cannot reference (private, protected or file-local); use delegates or a provider for those. Set the `KumikoReflectionPropertyAccess` MSBuild property to `true` or `false` to override the automatic choice.

---

## 🏗️ Architecture

```
KumikoUI.Core (net10.0) — zero external dependencies
       ↑
KumikoUI.SkiaSharp (net10.0) — SkiaSharp 4.153.1
       ↑
KumikoUI.Maui (net10.0 multi-target: iOS / Android / macOS Catalyst / Windows)
```

`KumikoUI.Core` has **no dependency** on MAUI or SkiaSharp. All rendering operations go through the `IDrawingContext` abstraction, making the core portable to any backend (Blazor Canvas, WPF, Direct2D, CoreGraphics, etc.).

### Key classes

| Class | Responsibility |
|---|---|
| `KumikoRenderer` | Orchestrates the 18-step draw pipeline (layout → background → headers → rows → overlays → popups) |
| `KumikoSource` | Data management: filtering, sorting, grouping, summaries, `INotifyCollectionChanged` observation |
| `KumikoLayoutEngine` | Column width computation (Fixed / Auto / Star), visible row/column range |
| `KumikoHitTester` | Resolves pointer coordinates to one of 14 `HitRegion` types across 3 column panes |
| `PaintCache` | Pre-computes ~60 `KumikoPaint` objects per frame from `KumikoStyle` |
| `KumikoInputController` | Routes pointer and keyboard events to selection, editing, resizing, drag handlers |
| `KumikoSelectionModel` | Row/cell selection state, Single / Multiple / Extended modes |
| `EditSession` | Cell edit lifecycle — begin, commit, cancel, validation, write-back |
| `CellEditorFactory` | Instantiates the correct `DrawnComponent` editor for each column type |
| `InertialScroller` | Physics-based scroll deceleration with configurable friction |
| `PopupManager` | Popup z-order and input priority routing (filter, ComboBox, DatePicker) |
| `IDrawingContext` | Platform-independent drawing API: fill, stroke, text, images, clipping |
| `SkiaDrawingContext` | `SKCanvas` implementation with 3-tier native object caching |
| `KumikoDataGrid` | MAUI control — hosts `SKCanvasView`, translates touch/scroll/keyboard events |

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for an in-depth contributor guide and [docs/RENDERING.md](docs/RENDERING.md) for the full 18-step rendering pipeline.

---

## 📱 Sample Application

`samples/SampleApp.Maui` demonstrates every feature across four pages:

| Page | What it shows |
|---|---|
| **All Components** | Every column type, frozen columns, frozen rows, edit triggers, selection modes, drag & drop, summaries, theme toggle |
| **Grouping & Filtering** | Interactive group panel, nested groups, filter popups, group summaries |
| **MVVM + Column EditTriggers** | Action button columns with MVVM commands; per-column `EditTriggers` overrides across all trigger types |
| **Custom Fonts** | CJK (Japanese) column headers via NotoSansJP; Material Design icon-glyph columns via MaterialIcons |
| **Large Data** | 100K-row stress test with virtual scrolling performance metrics |
| **Theming** | Live Light / Dark / HighContrast switching, custom color picker |

<table>
  <tr>
    <td align="center" width="50%">
      <img src="images/main.png" alt="All Components page" />
      <br/><sub><b>All Components</b></sub>
    </td>
    <td align="center" width="50%">
      <img src="images/grouping.png" alt="Grouping & Filtering page" />
      <br/><sub><b>Grouping &amp; Filtering</b></sub>
    </td>
  </tr>
  <tr>
    <td align="center" width="50%">
      <img src="images/dark_mode.png" alt="Dark theme" />
      <br/><sub><b>Dark Theme</b></sub>
    </td>
    <td align="center" width="50%">
      <img src="images/large_data.gif" alt="100K row virtual scroll" />
      <br/><sub><b>100K Rows — Virtual Scroll</b></sub>
    </td>
  </tr>
</table>

---

## 📦 Requirements

All packages target **.NET 10** and **SkiaSharp 4.153.1**. Uno apps must also set `<SkiaSharpVersion>4.153.1</SkiaSharpVersion>` in the app csproj, because Uno.Sdk defaults to SkiaSharp 3.x.

### Supported platforms

| Package | Platform (target framework) | Minimum OS | App build host | Verification |
|---|---|---|---|---|
| KumikoUI.Core, KumikoUI.SkiaSharp | any .NET 10 runtime (`net10.0`) | — | any OS | unit tests in CI |
| KumikoUI.Maui | iOS (`net10.0-ios`) | 17.0 | macOS (Xcode) | CI build |
| KumikoUI.Maui | Mac Catalyst (`net10.0-maccatalyst`) | 17.0 (macOS 14+) | macOS (Xcode) | CI build; sample launch checked on macOS 27 (#121) |
| KumikoUI.Maui | Android (`net10.0-android`) | API 24 (Android 7.0) | any OS | CI build |
| KumikoUI.Maui | Windows (`net10.0-windows10.0.19041.0`) | 10.0.19041 | Windows | CI build |
| KumikoUI.Uno | Desktop: Windows, macOS, Linux (`net10.0-desktop`) | as .NET 10 | any OS | CI build; sample run on macOS |
| KumikoUI.Uno | WebAssembly (`net10.0-browserwasm`) | current evergreen browsers | any OS (`wasm-tools` workload) | CI build |
| KumikoUI.Uno | iOS (`net10.0-ios`) | 17.0 | macOS (Xcode) | CI build |
| KumikoUI.Uno | Android (`net10.0-android`) | API 24 (Android 7.0) | any OS | CI build |
| KumikoUI.Uno | Windows App SDK (`net10.0-windows10.0.26100`) | 10.0.19041 | Windows | CI build |

"App build host" is where an app for that head can be built; the libraries themselves are compiled and packed for every head on Windows in CI. "CI build" means compiled and packed in CI; interactive device testing for each head is tracked in #67. KumikoUI.Uno has no Mac Catalyst head: Uno serves macOS through the desktop head.

**Watermark policy.** The minimum OS versions are defined once in `Directory.Build.props` (`KumikoMin*Version`) and apply to every library, sample and head. They are re-baselined at each .NET major release to the current platform guidance, and every change is called out as breaking in the [CHANGELOG](CHANGELOG.md).

---|---|
| .NET (libraries and apps) | 10.0 |
| SkiaSharp | 4.153.1 (Uno apps: set `<SkiaSharpVersion>4.153.1</SkiaSharpVersion>` in the app csproj; Uno defaults to 3.x) |
| iOS | 15.0+ |
| Android | 5.0+ (API 21) |
| macOS Catalyst | 15.0+ |
| Windows | 10.0.17763+ |

---

## 🤝 Contributing

Contributions are welcome! Please open an issue before submitting a large pull request so we can discuss the approach.

1. Fork the repository and create your branch from `main`.
2. Install the pinned toolchain and build as described in [CONTRIBUTING.md](CONTRIBUTING.md).
3. Run the tests: `dotnet test tests/KumikoUI.Core.Tests` (and `tests/KumikoUI.Uno.Tests` for Uno changes).
4. Submit a pull request with a clear description of the change.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for code structure and extension points.

---

## 📄 License

MIT — see [LICENSE](LICENSE) for details.
