---
applyTo: "src/KumikoUI.Maui/**,samples/**"
---

# KumikoUI — MAUI Control & XAML Usage

## Bootstrap (`MauiProgram.cs`)

```csharp
builder
    .UseMauiApp<App>()
    .UseSkiaKumikoUI();   // required — registers SkiaSharp drawing services
```

---

## Required XAML namespaces

```xml
xmlns:dg="clr-namespace:KumikoUI.Maui;assembly=KumikoUI.Maui"
xmlns:core="clr-namespace:KumikoUI.Core.Models;assembly=KumikoUI.Core"
xmlns:render="clr-namespace:KumikoUI.Core.Rendering;assembly=KumikoUI.Core"
xmlns:editing="clr-namespace:KumikoUI.Core.Editing;assembly=KumikoUI.Core"
```

---

## `DataGridView` — All bindable properties

```xml
<dg:DataGridView
    ItemsSource="{Binding Employees}"
    EditTriggers="DoubleTap,F2Key,Typing"
    EditTextSelectionMode="SelectAll"
    DismissKeyboardOnEnter="True"
    FrozenRowCount="2"
    RowHeight="36"
    HeaderHeight="40"
    GridDescription="Accessible description for screen readers"
    HorizontalOptions="Fill"
    VerticalOptions="Fill" />
```

`EditTriggers` flags: `SingleTap`, `DoubleTap`, `LongPress`, `F2Key`, `Typing`

---

## Minimal column declaration

```xml
<dg:DataGridView ItemsSource="{Binding Items}">
    <dg:DataGridView.Columns>
        <core:DataGridColumn Header="Name" PropertyName="Name" Width="180" />
    </dg:DataGridView.Columns>
</dg:DataGridView>
```

---

## All column types — XAML examples

```xml
<!-- Numeric, frozen left, read-only -->
<core:DataGridColumn Header="Id" PropertyName="Id" ColumnType="Numeric"
                     Width="60" IsReadOnly="True" IsFrozen="True" AllowTabStop="False"
                     TextAlignment="Right" />

<!-- Text, frozen right -->
<core:DataGridColumn Header="City" PropertyName="City" Width="130" FreezeMode="Right" />

<!-- ComboBox — items as CSV -->
<core:DataGridColumn Header="Department" PropertyName="Department" Width="140"
                     ColumnType="ComboBox"
                     EditorItemsString="Engineering,Marketing,Sales,HR,Finance" />

<!-- Picker — scroll-wheel -->
<core:DataGridColumn Header="Level" PropertyName="Level" Width="120"
                     ColumnType="Picker"
                     EditorItemsString="Junior,Mid,Senior,Staff,Principal" />

<!-- Date -->
<core:DataGridColumn Header="Hire Date" PropertyName="HireDate" Width="120"
                     ColumnType="Date" Format="yyyy-MM-dd" />

<!-- Boolean -->
<core:DataGridColumn Header="Active" PropertyName="IsActive" Width="80"
                     ColumnType="Boolean" AllowTabStop="False" />

<!-- Template — custom renderer (display only) -->
<core:DataGridColumn Header="Performance" PropertyName="Performance"
                     ColumnType="Template" Width="140" IsReadOnly="True">
    <core:DataGridColumn.CustomCellRenderer>
        <render:ProgressBarCellRenderer Minimum="0" Maximum="100"
                                        TrackHeight="12" CornerRadius="6"
                                        ShowText="True" TextFormat="N0" />
    </core:DataGridColumn.CustomCellRenderer>
</core:DataGridColumn>

<!-- Template — declarative editor -->
<core:DataGridColumn Header="Rating" PropertyName="Rating"
                     ColumnType="Template" Width="100">
    <core:DataGridColumn.EditorDescriptor>
        <editing:NumericUpDownEditorDescriptor Minimum="1" Maximum="5"
                                               Step="1" DecimalPlaces="0" />
    </core:DataGridColumn.EditorDescriptor>
</core:DataGridColumn>
```

---

## Summary rows in XAML

```xml
<dg:DataGridView.TableSummaryRows>
    <core:TableSummaryRow Name="Averages" Position="Top" Title="Averages">
        <core:TableSummaryRow.Columns>
            <core:SummaryColumnDescription PropertyName="Salary"
                                           SummaryType="Average" Format="C0" />
            <core:SummaryColumnDescription PropertyName="Rating"
                                           SummaryType="Average" Format="N1" />
        </core:TableSummaryRow.Columns>
    </core:TableSummaryRow>

    <core:TableSummaryRow Name="Totals" Position="Bottom" Title="Totals">
        <core:TableSummaryRow.Columns>
            <core:SummaryColumnDescription PropertyName="Salary"
                                           SummaryType="Sum" Format="C0" />
            <core:SummaryColumnDescription PropertyName="Id"
                                           SummaryType="Count" Label="Rows: " />
        </core:TableSummaryRow.Columns>
    </core:TableSummaryRow>
</dg:DataGridView.TableSummaryRows>
```

---

## `DataGridView` internals (for platform-specific work)

- Extends `Grid`; contains `SKCanvasView` (render surface) + hidden `Entry` (keyboard proxy).
- `[ContentProperty(nameof(Columns))]` — columns are XAML content.
- Double-tap: 400 ms threshold, 20 px distance.
- Long-press: 500 ms, 15 px tolerance.
- Android IME: zero-width-space (`\u200B`) sentinel for backspace detection.
- iOS/macCatalyst: `KeyInputResponder` (native UIKit) + `UIPanGestureRecognizer`.
- Inertial scroll and cursor blink use `IDispatcherTimer`.
