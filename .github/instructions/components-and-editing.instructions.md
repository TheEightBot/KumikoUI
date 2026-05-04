---
applyTo: "src/KumikoUI.Core/Components/**,src/KumikoUI.Core/Editing/**"
---

# KumikoUI — Drawn Components & Editing

## `DrawnComponent` — Canvas-drawn editors (no native controls)

All inline cell editors inherit from `DrawnComponent` in `KumikoUI.Core.Components`.
There are **zero native UIKit / WinUI / Android widgets** — everything is drawn on canvas.

### Minimum implementation

```csharp
public class MyEditor : DrawnComponent
{
    public override void OnDraw(IDrawingContext ctx)
    {
        // Draw inside this.Bounds using IDrawingContext only.
        ctx.FillRect(Bounds, new GridPaint { Color = new GridColor(255, 255, 255) });
    }
}
```

### Lifecycle overrides

| Method | When called |
|---|---|
| `OnDraw(IDrawingContext ctx)` | **Required.** Every repaint. |
| `OnPointerDown(GridPointerEventArgs e)` | Touch / mouse press |
| `OnPointerUp(GridPointerEventArgs e)` | Touch / mouse release |
| `OnPointerMove(GridPointerEventArgs e)` | Touch / mouse drag |
| `OnKeyDown(GridKeyEventArgs e)` | Key press (set `e.Handled = true` to consume) |
| `OnKeyUp(GridKeyEventArgs e)` | Key release |
| `OnGotFocus()` | Component gained keyboard focus |
| `OnLostFocus()` | Component lost focus |
| `OnBoundsChanged()` | `Bounds` property changed |

### Signals

```csharp
InvalidateVisual();              // request repaint — call whenever visual state changes
RaiseValueChanged(old, new);     // consumed by EditSession — call when value changes
RaiseEditCompleted();            // grid commits + closes editor — call on confirm gesture
```

### Theming hook

Add `public void ApplyTheme(DataGridStyle style)` — no interface required.
`CellEditorFactory.ApplyThemeToEditor()` dispatches to it via `switch`.

```csharp
public void ApplyTheme(DataGridStyle style)
{
    _backgroundColor = style.EditorBackgroundColor;
    _foregroundColor  = style.EditorTextColor;
    _accentColor      = style.SelectionColor;
    InvalidateVisual();
}
```

### `CellEditorFactory` — three required cases per editor type

```csharp
// 1. CreateEditor:
case DataGridColumnType.{NewType}:
    return new MyEditor { Bounds = cellBounds, Value = ({T}?)currentValue ?? default };

// 2. GetEditorValue:
case MyEditor e:
    return e.Value;

// 3. ApplyThemeToEditor:
case MyEditor e:
    e.ApplyTheme(style);
    break;
```

---

## `EditorDescriptor` — XAML-declarable editor configuration

Subclass `EditorDescriptor` to let column authors configure the editor in XAML without code-behind.

```csharp
public class MyEditorDescriptor : EditorDescriptor
{
    public double Minimum { get; set; } = 0;
    public double Maximum { get; set; } = 100;

    public override DrawnComponent? CreateEditor(object? currentValue, GridRect cellBounds)
        => new MyEditor { Bounds = cellBounds, Minimum = Minimum, Maximum = Maximum };
}
```

XAML usage:
```xml
<core:DataGridColumn Header="Score" PropertyName="Score" ColumnType="Template" Width="120">
    <core:DataGridColumn.EditorDescriptor>
        <editing:MyEditorDescriptor Minimum="0" Maximum="10" />
    </core:DataGridColumn.EditorDescriptor>
</core:DataGridColumn>
```

Built-in descriptors: `NumericUpDownEditorDescriptor`, `TextEditorDescriptor`.

---

## `EditSession` — Cell editing pipeline

```
BeginEdit(row, col, column, dataSource, cellBounds, initialChar?)
  → CellEditorFactory.CreateEditor(...)
  → CellEditorFactory.ApplyThemeToEditor(...)
  → EditSession.DrawEditor(ctx)        ← called each frame after main render
  → CommitEdit(dataSource)             ← writes value back via DataGridSource.SetCellValue
    or CancelEdit()
```

**Events:** `CellBeginEdit` (cancelable), `CellEndEdit`, `CellValueChanged`

**Validation:**
```csharp
// Option A — implement on model:
class Employee : IDataErrorInfo { ... }

// Option B — delegate:
editSession.CellValidator = (row, col, value) =>
    value is int i && i >= 1 && i <= 5
        ? CellValidationResult.Success
        : CellValidationResult.Error("Value must be 1–5");
```

---

## `InertialScroller`

```csharp
var scroller = new InertialScroller { Settings = new ScrollSettings { Friction = 0.92f } };

scroller.TrackVelocity(dx, dy, timestampMs);   // during pan
scroller.StartFling();                          // on pointer release

if (scroller.IsActive)
{
    var (dx, dy) = scroller.Update(frameIntervalMs: 16f);
    scroll.ScrollBy(-dx, -dy);
    // trigger repaint
}
```

---

## Checklist: adding a new `DrawnComponent`

- [ ] Class in `src/KumikoUI.Core/Components/`, named `Drawn{Name}.cs`
- [ ] Inherits `DrawnComponent`
- [ ] `OnDraw` uses only `IDrawingContext` + `GridColor`/`GridPaint`/`GridFont`/`GridRect`
- [ ] `InvalidateVisual()` called on every state change
- [ ] `RaiseValueChanged(old, new)` called when value changes
- [ ] `RaiseEditCompleted()` called on confirm gesture
- [ ] `ApplyTheme(DataGridStyle)` implemented
- [ ] `CellEditorFactory` updated: `CreateEditor`, `GetEditorValue`, `ApplyThemeToEditor`
