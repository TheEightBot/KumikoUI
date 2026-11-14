---
applyTo: "src/KumikoUI.Maui/**,samples/**"
---

# KumikoUI — MAUI Control & XAML Usage

## Bootstrap (`MauiProgram.cs`)

```csharp
builder
    .UseMauiApp<App>()
    .UseKumikoUI();   // required: SkiaSharp, the canvas handler, and font registration that also reaches Skia
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

## `KumikoDataGrid` — All bindable properties

Same names and defaults on `KumikoUI.Maui` (BindableProperty) and `KumikoUI.Uno` (DependencyProperty).
Full reference with types and defaults: README section "Host Properties, Commands & Events".

```xml
<dg:KumikoDataGrid
    ItemsSource="{Binding Employees}"
    GridSelectionMode="Extended"
    SelectedItem="{Binding SelectedEmployee, Mode=TwoWay}"
    SelectedItems="{Binding SelectedEmployees}"
    SelectedIndex="{Binding SelectedRow, Mode=TwoWay}"
    CurrentCell="{Binding CurrentCell, Mode=TwoWay}"
    SelectionChangedCommand="{Binding SelectionChangedCommand}"
    RowTappedCommand="{Binding RowTappedCommand}"
    CellValueChangedCommand="{Binding CellValueChangedCommand}"
    SortChangedCommand="{Binding SortChangedCommand}"
    CellValidator="{Binding Validator}"
    EditTriggers="DoubleTap,F2Key,Typing"
    EditTextSelectionMode="SelectAll"
    DismissKeyboardOnEnter="True"
    IsReadOnly="False"
    AllowSorting="True"
    AllowFiltering="True"
    FrozenRowCount="2"
    RowHeight="36"
    HeaderHeight="40"
    ShowHorizontalGridLines="True"
    ShowVerticalGridLines="False"
    AlternateRowBackground="#EEF4FF"
    AllowRowDragDrop="True"
    ShowRowDragHandle="True"
    GridDescription="Accessible description for screen readers"
    HorizontalOptions="Fill"
    VerticalOptions="Fill" />
```

Collections (XAML content or property elements): `Columns` (content property), `TableSummaryRows`,
`GroupDescriptions`, `GroupSummaryRows`.

Also bindable: `RenderMode` (`Auto`, `Software`, `Gpu`; `ActiveRenderMode` reports the surface in use),
`BackgroundRebuildThreshold` (rows from which sort, filter, group and summary rebuilds run on a worker; default
`KumikoSource.DefaultBackgroundRebuildThreshold`), `ClearSelectionOnEmptyAreaTap`, `FollowSystemTheme`,
`AnnounceChanges`, `EnableHaptics`, `ShowFrameStats`, `CellTappedCommand`, `HeaderTappedCommand`,
`ContextMenuRequestedCommand`, and the read-only `VerticalOffset` / `HorizontalOffset`.

Plain (not bindable) properties: `Theme`, `GridStyle`, `ScrollSettings`. `ShowHorizontalGridLines`,
`ShowVerticalGridLines`, `AlternateRowBackground`, `AllowRowDragDrop` and `ShowRowDragHandle` are nullable:
null leaves the value to the style or theme, a value overrides it and is re-applied after `Theme` or
`GridStyle` is assigned.

Events: `RowTapped`, `RowDoubleTapped`, `CellBeginEdit`, `CellEndEdit`, `CellValueChanged`, `ValidationFailed`,
`CellTapped`, `HeaderTapped`, `ContextMenuRequested`, `Scrolled`, `SortChanging`, `SortChanged`, `FilterChanging`, `FilterChanged`, `ViewChanged`, `GroupExpanding`,
`GroupExpandedChanged`, `RowReordered`, `RowReorderRejected`, `ColumnReordered`, `ColumnResized`, `SelectionChanged`.

Methods: `Invalidate()` repaints without rebuilding the view (any thread), `ScrollToRow`, `ScrollToItem`,
`ScrollToColumn` (with a `KumikoScrollPosition`), `GetLayoutState` / `ApplyLayoutState`, `SetCellRenderer`, `TrySetText`,
`GetTextAsync`. `DataSource` exposes the `KumikoSource`.

`EditTriggers` flags: `SingleTap`, `DoubleTap`, `LongPress`, `F2Key`, `Typing`

---

## Minimal column declaration

```xml
<dg:KumikoDataGrid ItemsSource="{Binding Items}">
    <dg:KumikoDataGrid.Columns>
        <core:KumikoColumn Header="Name" PropertyName="Name" Width="180" />
    </dg:KumikoDataGrid.Columns>
</dg:KumikoDataGrid>
```

---

## All column types — XAML examples

```xml
<!-- Numeric, frozen left, read-only -->
<core:KumikoColumn Header="Id" PropertyName="Id" ColumnType="Numeric"
                     Width="60" IsReadOnly="True" IsFrozen="True" AllowTabStop="False"
                     TextAlignment="Right" />

<!-- Text, frozen right -->
<core:KumikoColumn Header="City" PropertyName="City" Width="130" FreezeMode="Right" />

<!-- ComboBox — items as CSV -->
<core:KumikoColumn Header="Department" PropertyName="Department" Width="140"
                     ColumnType="ComboBox"
                     EditorItemsString="Engineering,Marketing,Sales,HR,Finance" />

<!-- Picker — scroll-wheel -->
<core:KumikoColumn Header="Level" PropertyName="Level" Width="120"
                     ColumnType="Picker"
                     EditorItemsString="Junior,Mid,Senior,Staff,Principal" />

<!-- Date -->
<core:KumikoColumn Header="Hire Date" PropertyName="HireDate" Width="120"
                     ColumnType="Date" Format="yyyy-MM-dd" />

<!-- Boolean -->
<core:KumikoColumn Header="Active" PropertyName="IsActive" Width="80"
                     ColumnType="Boolean" AllowTabStop="False" />

<!-- Template — custom renderer (display only) -->
<core:KumikoColumn Header="Performance" PropertyName="Performance"
                     ColumnType="Template" Width="140" IsReadOnly="True">
    <core:KumikoColumn.CustomCellRenderer>
        <render:ProgressBarCellRenderer Minimum="0" Maximum="100"
                                        TrackHeight="12" CornerRadius="6"
                                        ShowText="True" TextFormat="N0" />
    </core:KumikoColumn.CustomCellRenderer>
</core:KumikoColumn>

<!-- Template — declarative editor -->
<core:KumikoColumn Header="Rating" PropertyName="Rating"
                     ColumnType="Template" Width="100">
    <core:KumikoColumn.EditorDescriptor>
        <editing:NumericUpDownEditorDescriptor Minimum="1" Maximum="5"
                                               Step="1" DecimalPlaces="0" />
    </core:KumikoColumn.EditorDescriptor>
</core:KumikoColumn>
```

---

## Summary rows in XAML

```xml
<dg:KumikoDataGrid.TableSummaryRows>
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
</dg:KumikoDataGrid.TableSummaryRows>
```

---

## `KumikoDataGrid` internals (for platform-specific work)

The control is a `partial class` spread over `src/KumikoUI.Maui/KumikoDataGrid.*.cs`, one file per concern (rendering,
pointer, scrolling, selection, text input with a platform file each, Apple keyboard, haptics, motion, accessibility,
clipboard, images, theming, options, layout, lifecycle, hosting, cursor).

- Extends `ContentView`; its content is a private `KumikoSurface` (in `src/KumikoUI.Maui/Rendering/`): an `SKGLView` or `SKCanvasView` chosen by
  `KumikoSurfaceFactory` from `RenderMode`. `[ContentProperty(nameof(Columns))]`: columns are XAML content.
- Frame loop: every redraw goes through `RequestRedraw`; `KumikoFrameScheduler` (platform-free, unit-tested) coalesces
  requests and measures frame times, `KumikoFrameClock` is the platform callback (`CADisplayLink`, `Choreographer`,
  `CompositionTarget.Rendering`), and it runs only while there is work (a fling, a row drag's autoscroll, an editor
  animation, a deferred redraw). Each frame draws with `ResourceCache.CreateContext(canvas)` and
  `KumikoRenderer.RenderFrame`. `KumikoCaretBlink` and `KumikoCaretFrame` redraw only the caret area between blinks.
  See docs/RENDERING.md "Surfaces and Frame Loop".
- The control creates its `KumikoSource` with `BackgroundRebuildThreshold` set and repaints on `ViewChanged` and
  `ViewUpdated`.
- Windows: the grid's platform view is the keyboard sink (`KumikoDataGrid.TextInput.Windows.cs`): `PreviewKeyDown` → `WindowsKeyInput`, `CharacterReceived` → `WindowsCharacterInput`, and a `CoreTextEditContext` bridged to the editor's `ITextInputSession` through `TextEditContextState` (falls back to `CharacterReceived` where the text services are unavailable).
- Double-tap: 400 ms threshold, 20 px distance. Long-press: 500 ms, 15 px tolerance. A touch long press starts a row drag (`AllowRowDragDrop` or the drag handle); the release after it is sent as `Cancelled` unless a drag is in progress.
- Android: `KumikoTextInputView` (native input view, keyboard focus) + `KumikoInputConnection` (`BaseInputConnection`) over `AndroidTextInputMapper` (platform-independent, unit-tested) drive `ITextInputSession`; no hidden `Entry`. See docs/TEXT-INPUT.md#android.
- iOS/macCatalyst: `KeyInputResponder` (a `UITextInput` responder; no `Entry`) + `UIPanGestureRecognizer`. Its UIKit-independent logic (marked text, UTF-16 positions, typing with no editor open) is `AppleTextInputBridge`, tested in `tests/KumikoUI.Maui.Tests`.
- Keyboard avoidance: `KumikoKeyboardAvoidance` (Core) computes `ScrollState.BottomInset`; the host feeds it the keyboard height.
- Inertial scroll, edge autoscroll (`UpdateAutoScroll` while `IsAutoScrolling`) and editor animation step from the frame clock; the caret blink uses an `IDispatcherTimer`.
- Fonts: `KumikoFonts` and `KumikoFontRegistrar` register `ConfigureFonts` fonts with `SkiaFontRegistrar`. Images load through `MauiImageLoader` into the shared `KumikoImageCache`.
- Tests: `tests/KumikoUI.Maui.Tests` (net10.0, no device: the platform-independent logic and a control created without a handler).
