---
applyTo: "src/KumikoUI.Core/Components/**,src/KumikoUI.Core/Editing/**"
---

# KumikoUI — Drawn Components & Editing

## `DrawnComponent` — Canvas-drawn editors (no native controls)

All inline cell editors inherit from `DrawnComponent` in `KumikoUI.Core.Components`.
There are **zero native UIKit / WinUI / Android widgets**: everything is drawn on canvas.

Public components: `DrawnTextBox`, `DrawnComboBox`, `DrawnDatePicker`, `DrawnNumericUpDown`, `DrawnScrollPicker`,
`DrawnFilterPopup`, `DrawnActionButtons`. `DrawnCheckBox`, `DrawnProgressBar`, `DrawnFocusManager` and `PopupManager`
are internal. A new component is public only when users create it themselves (an editor a column can return);
otherwise keep it `internal`, and follow the naming rules in CONTRIBUTING.md ("Public type names").

### Minimum implementation (inside `KumikoUI.Core`; an editor in app code has no `PaintMemo`, so it keeps its own `KumikoPaint` fields)

```csharp
public sealed class RatingEditor : DrawnComponent, IValueEditor
{
    private readonly PaintMemo _paints = new();     // internal: hands back the same paint for the same values
    private int _value;
    private bool _dirty;

    public object? Value => _value;
    public bool HasValue => true;
    public bool IsDirty => _dirty;                 // set it when the user changes the value

    public override void OnDraw(IDrawingContext ctx)
    {
        // Draw inside Bounds using IDrawingContext only.
        ctx.FillRect(Bounds, _paints.Get(KumikoColor.White));
    }
}
```

`IValueEditor` (`Value`, `HasValue`, `IsDirty`) is how `EditSession` reads the edited value back
(`CellEditorFactory.GetEditorValue`); an editor that does not implement it commits no value.

### Lifecycle overrides

| Member | When called |
|---|---|
| `abstract OnDraw(IDrawingContext ctx)` | **Required.** Every repaint. |
| `OnPointerDown(KumikoPointerEventArgs e)` | Press. Returns true when handled. |
| `OnPointerUp(KumikoPointerEventArgs e)` | Release. Returns true when handled. |
| `OnPointerMove(KumikoPointerEventArgs e)` | Move or drag. Returns true when handled. |
| `OnPointerCancel(KumikoPointerEventArgs e)` | The system took the pointer away. |
| `OnScroll(KumikoPointerEventArgs e)` | Wheel or trackpad scroll over the component. |
| `OnKeyDown(KumikoKeyEventArgs e)` / `OnKeyUp(...)` | Key press and release. Returns true when handled. |
| `HitTest(float x, float y)` | Whether a point is on the component (default: inside `Bounds`). |
| `UpdateAnimation(float frameIntervalMs)` | Each frame while the edit session animates; returns true while more frames are needed. |
| `protected OnGotFocus()` / `OnLostFocus()` | Keyboard focus gained or lost. |
| `protected OnBoundsChanged()` | `Bounds` changed. |

Text-like editors also implement `ITextInputSession` (see [docs/TEXT-INPUT.md](../../docs/TEXT-INPUT.md)); `DrawnTextBox`
is the reference.

### Signals

```csharp
InvalidateVisual();              // request a repaint; call whenever visual state changes
RaiseValueChanged(old, new);     // raises ValueChanged; EditSession listens (also marks the editor dirty)
RaiseEditCompleted();            // raises EditCompleted: the grid commits and closes the editor (confirm gesture)
```

### Theming hook

Built-in editors have `ApplyTheme(KumikoStyle style)` and `ApplyFont(KumikoFont font)`; there is no interface.
`CellEditorFactory.ApplyThemeToEditor` and `ApplyFontToEditor` dispatch to them with a `switch`, so a new built-in editor
adds a case to both. A custom editor created through a descriptor or factory reads the style itself.

```csharp
public void ApplyTheme(KumikoStyle style)
{
    _background = style.BackgroundColor;
    _foreground = style.CellTextColor;
    _accent     = style.SelectionColor;
    InvalidateVisual();
}
```

### `CellEditorFactory` — where an editor type is created

`CellEditorFactory.CreateEditor(column, currentValue, cellBounds, initialCharacter, selectionMode, culture)` switches on
`KumikoColumnType`:

```csharp
// CreateEditor: one case per column type (Boolean, Image and ProgressBar have no overlay editor)
KumikoColumnType.{NewType} => Create{Type}Editor(column, currentValue, cellBounds, initialCharacter, culture),

// ApplyThemeToEditor and ApplyFontToEditor: one case per editor type
case Drawn{Type} editor: editor.ApplyTheme(style); break;
```

Template columns resolve an editor in this order: `KumikoColumn.EditorFactory`, `CustomEditorFactory`,
`EditorDescriptor`, an `ICellEditorProvider` renderer, then a text editor. A new editor that only one app needs goes
through one of those, not through a new `KumikoColumnType`.

---

## `EditorDescriptor` — XAML-declarable editor configuration

Subclass `EditorDescriptor` to let column authors configure the editor in XAML without code-behind.

```csharp
public sealed class RatingEditorDescriptor : EditorDescriptor
{
    public double Minimum { get; set; } = 0;
    public double Maximum { get; set; } = 5;

    public override DrawnComponent? CreateEditor(object? currentValue, KumikoRect cellBounds)
        => new RatingEditor { Bounds = cellBounds };

    // Override this overload instead to read the typed character and the culture:
    // public override DrawnComponent? CreateEditor(CellEditorContext context)
}
```

XAML usage:
```xml
<core:KumikoColumn Header="Score" PropertyName="Score" ColumnType="Template" Width="120">
    <core:KumikoColumn.EditorDescriptor>
        <editing:RatingEditorDescriptor Minimum="0" Maximum="5" />
    </core:KumikoColumn.EditorDescriptor>
</core:KumikoColumn>
```

Built-in descriptors: `NumericUpDownEditorDescriptor`, `TextEditorDescriptor`.

---

## `EditSession` — Cell editing pipeline

```
BeginEdit(rowIndex, columnIndex, column, dataSource, cellBounds, initialCharacter?)
  → CellBeginEdit (cancelable)
  → CellEditorFactory.CreateEditor(...)  then ApplyThemeToEditor / ApplyFontToEditor
  → PlaceEditor + DrawEditor(ctx)        ← each frame, by KumikoRenderer, after the grid
  → CommitEdit(dataSource)               ← validates, writes back through KumikoSource.SetCellValue, CellEndEdit
    or CancelEdit()
```

`ToggleBooleanCell` toggles a boolean cell without an editor. `HandleKeyEvent` and `HandlePointerEvent` route input to the
open editor; `UpdateAnimation` steps editor animations from the host's frame loop.

**Events:** `CellBeginEdit` (cancelable), `CellEndEdit`, `CellValueChanged`, `ValidationFailed`.
`EditTriggers` (`EditTrigger` flags: `SingleTap`, `DoubleTap`, `F2Key`, `Typing`, `LongPress`; default `DoubleTap | F2Key | Typing`).

**Validation:**
```csharp
// Option A: implement IDataErrorInfo or INotifyDataErrorInfo on the model; the session reads the error after the write.

// Option B: a delegate, called with (row, column, value) before the write:
editSession.CellValidator = (row, col, value) =>
    value is int i && i >= 1 && i <= 5
        ? CellValidationResult.Valid
        : CellValidationResult.Error("Value must be 1-5");   // also CellValidationResult.Warning(message)
```

---

## `InertialScroller`

```csharp
var scroller = new InertialScroller { Settings = ScrollSettings.FromPreset(KumikoMomentumPreset.Default) };

scroller.TrackVelocity(dx, dy, timestampMs);   // during a pan
scroller.StartFling(releaseTimestampMs);       // on release
// each frame, with the measured time since the previous frame (clamped by the host to 1-50 ms):
if (scroller.IsActive)
{
    var (dx, dy) = scroller.Update(frameIntervalMs);
    scroll.ScrollBy(-dx, -dy);
    // request a redraw
}
```

Travel and friction scale with the frame interval, so a fling covers the same distance at 60 and 120 Hz.
`ReportApplied` tells it how much of a step the scroll bounds accepted. `ScrollSettings` holds `Friction`,
`MinVelocity`, `MaxVelocity`, `WheelScrollMultiplier`, `EnableInertia` and the edge autoscroll values.

---

## Checklist: adding a new `DrawnComponent`

- [ ] Class in `src/KumikoUI.Core/Components/`, in `Drawn{Name}.cs`; `sealed`; `internal` unless users create it
- [ ] Inherits `DrawnComponent`; an editor also implements `IValueEditor`
- [ ] `OnDraw` uses only `IDrawingContext` + `KumikoColor`/`KumikoPaint`/`KumikoFont`/`KumikoRect`, and allocates nothing per call (use a `PaintMemo` or fields for paints)
- [ ] `InvalidateVisual()` called on every state change
- [ ] `RaiseValueChanged(old, new)` called when the value changes
- [ ] `RaiseEditCompleted()` called on the confirm gesture
- [ ] `ApplyTheme(KumikoStyle)` and `ApplyFont(KumikoFont)` implemented, and added to `CellEditorFactory`'s two `switch`es
- [ ] Created in `CellEditorFactory.CreateEditor` (new `KumikoColumnType`) or through an `EditorDescriptor`
- [ ] Every public member has XML documentation; tests in `tests/KumikoUI.Core.Tests/Components/Drawn{Name}Tests.cs`, using the input helpers in `ComponentInput.cs`
