---
mode: agent
description: Scaffold a new DrawnComponent inline cell editor for KumikoUI
---

# Add a DrawnComponent Editor

Create a new canvas-drawn inline cell editor in `src/KumikoUI.Core/Components/`.
All editors inherit from `DrawnComponent` — there are **no native UI controls**.

## What to ask the user before generating code

1. What is the **class name**? (e.g. `DrawnColorPicker`, `DrawnTimePicker`)
2. What **value type** does it edit? (`string`, `double`, `DateTime`, `bool`, etc.)
3. What **input interactions** are needed? (tap, drag, keyboard, swipe?)
4. Should it be **declarative via XAML**? If yes, also create an `EditorDescriptor` subclass.
5. Does it need to **display a popup** (like `DrawnDatePicker` or `DrawnComboBox`)?

---

## Files to create / modify

| Action | File |
|---|---|
| **Create** | `src/KumikoUI.Core/Components/Drawn{Name}.cs` |
| **Create** (if XAML-declarable) | `src/KumikoUI.Core/Editing/{Name}EditorDescriptor.cs` |
| **Modify** | `src/KumikoUI.Core/Editing/CellEditorFactory.cs` — add case |
| **Create** | `tests/KumikoUI.Core.Tests/Drawn{Name}Tests.cs` (if logic is testable) |

---

## DrawnComponent template

```csharp
// src/KumikoUI.Core/Components/Drawn{Name}.cs
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Canvas-drawn {description} editor component.
/// </summary>
public class Drawn{Name} : DrawnComponent
{
    // ── State ─────────────────────────────────────────────────────────────────────

    private {ValueType} _value;
    private bool _isPressed;

    // ── Properties ────────────────────────────────────────────────────────────────

    /// <summary>Current value of the editor.</summary>
    public {ValueType} Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<{ValueType}>.Default.Equals(_value, value)) return;
            var old = _value;
            _value = value;
            InvalidateVisual();
            RaiseValueChanged(old, _value);
        }
    }

    // ── Theming ───────────────────────────────────────────────────────────────────

    private KumikoColor _backgroundColor = new KumikoColor(255, 255, 255);
    private KumikoColor _foregroundColor  = new KumikoColor(30, 30, 30);
    private KumikoColor _accentColor      = new KumikoColor(0, 120, 215);

    /// <summary>Applies colors from the active KumikoStyle.</summary>
    public void ApplyTheme(KumikoStyle style)
    {
        _backgroundColor = style.EditorBackgroundColor;
        _foregroundColor  = style.EditorTextColor;
        _accentColor      = style.SelectionColor;
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        // Background
        ctx.FillRect(Bounds, new KumikoPaint { Color = _backgroundColor, Style = PaintStyle.Fill });

        // Border
        ctx.DrawRect(Bounds, new KumikoPaint
        {
            Color = _isPressed ? _accentColor : new KumikoColor(180, 180, 180),
            Style = PaintStyle.Stroke,
            StrokeWidth = 1f
        });

        // TODO: draw the value representation inside Bounds using IDrawingContext methods.
        // Use KumikoColor, KumikoPaint, KumikoFont, KumikoRect — never SkiaSharp types.
    }

    // ── Input ─────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnPointerDown(KumikoPointerEventArgs e)
    {
        _isPressed = true;
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void OnPointerUp(KumikoPointerEventArgs e)
    {
        _isPressed = false;
        // TODO: handle tap confirm
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void OnKeyDown(KumikoKeyEventArgs e)
    {
        switch (e.Key)
        {
            case KumikoKey.Escape:
                // Let the edit session handle cancel.
                break;
            case KumikoKey.Enter:
                RaiseEditCompleted();
                e.Handled = true;
                break;
        }
    }
}
```

---

## `CellEditorFactory` — add cases

In `src/KumikoUI.Core/Editing/CellEditorFactory.cs`:

**`CreateEditor` switch:**
```csharp
// add inside CreateEditor(...)  switch (column.ColumnType)
case KumikoColumnType.{NewType}:
    return new Drawn{Name}
    {
        Bounds = cellBounds,
        Value = ({ValueType}?)currentValue ?? default
    };
```

**`GetEditorValue` switch:**
```csharp
case Drawn{Name} e:
    return e.Value;
```

**`ApplyThemeToEditor` switch:**
```csharp
case Drawn{Name} e:
    e.ApplyTheme(style);
    break;
```

---

## Optional: `EditorDescriptor` (for XAML-declarable config)

```csharp
// src/KumikoUI.Core/Editing/{Name}EditorDescriptor.cs
namespace KumikoUI.Core.Editing;

/// <summary>
/// Declarative configuration for <see cref="Drawn{Name}"/> in XAML.
/// </summary>
public class {Name}EditorDescriptor : EditorDescriptor
{
    // TODO: add configurable properties
    public int SomeOption { get; set; } = 1;

    /// <inheritdoc />
    public override DrawnComponent? CreateEditor(object? currentValue, KumikoRect cellBounds)
        => new Drawn{Name}
        {
            Bounds = cellBounds,
            Value  = ({ValueType}?)currentValue ?? default
        };
}
```

**XAML usage:**
```xml
<core:KumikoColumn Header="..." PropertyName="..." ColumnType="Template" Width="120">
    <core:KumikoColumn.EditorDescriptor>
        <editing:{Name}EditorDescriptor SomeOption="42" />
    </core:KumikoColumn.EditorDescriptor>
</core:KumikoColumn>
```

---

## Rules checklist

- [ ] Class is in namespace `KumikoUI.Core.Components`
- [ ] No SkiaSharp APIs — only `IDrawingContext` primitives and `KumikoColor`/`KumikoPaint`/`KumikoFont`
- [ ] `InvalidateVisual()` called whenever visual state changes
- [ ] `RaiseValueChanged(old, new)` called when value changes
- [ ] `RaiseEditCompleted()` called when the user confirms their selection
- [ ] `ApplyTheme(KumikoStyle)` implemented (no interface — `CellEditorFactory` calls it via `switch`)
- [ ] `CellEditorFactory` updated with `CreateEditor`, `GetEditorValue`, and `ApplyThemeToEditor` cases
