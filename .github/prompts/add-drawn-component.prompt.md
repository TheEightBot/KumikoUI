---
mode: agent
description: Scaffold a new DrawnComponent inline cell editor for KumikoUI
---

# Add a DrawnComponent Editor

Create a new canvas-drawn inline cell editor in `src/KumikoUI.Core/Components/`.
All editors inherit from `DrawnComponent`: there are **no native UI controls**. Read
`.github/instructions/components-and-editing.instructions.md` first.

## What to ask the user before generating code

1. What is the **class name**? (a `Drawn...` name, for example a time picker)
2. What **value type** does it edit? (`string`, `double`, `DateTime`, `bool`, etc.)
3. What **input interactions** are needed? (tap, drag, keyboard, swipe?)
4. Should it be **declarative via XAML**? If yes, also create an `EditorDescriptor` subclass.
5. Does it need to **display a popup** (like `DrawnDatePicker` or `DrawnComboBox`)? Those place their popup with
   `PopupLayout` inside `DrawnComponent.PopupViewport`, and the grid draws it through `PopupManager`.
6. Does it take text? Then it also implements `ITextInputSession` (see `DrawnTextBox` and `docs/TEXT-INPUT.md`).

---

## Files to create / modify

| Action | File |
|---|---|
| **Create** | `src/KumikoUI.Core/Components/Drawn{Name}.cs` |
| **Create** (if XAML-declarable) | `src/KumikoUI.Core/Editing/{Name}EditorDescriptor.cs` |
| **Modify** | `src/KumikoUI.Core/Editing/CellEditorFactory.cs`: the creation case and the `ApplyThemeToEditor` / `ApplyFontToEditor` cases |
| **Create** | `tests/KumikoUI.Core.Tests/Components/Drawn{Name}Tests.cs` (use `ComponentInput` for pointer and key events) |

An editor that only one app needs lives in that app, returned from `KumikoColumn.EditorFactory` or an `EditorDescriptor`;
add it to the library only when it is a general component.

---

## DrawnComponent template

```csharp
// src/KumikoUI.Core/Components/Drawn{Name}.cs
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Canvas-drawn {description} editor component.
/// </summary>
public sealed class Drawn{Name} : DrawnComponent, IValueEditor
{
    // ── State ─────────────────────────────────────────────────────────────────────

    private {ValueType} _value;
    private bool _isPressed;
    private bool _isDirty;

    // Paints are created once and reused every frame (the render loop allocates nothing).
    private readonly PaintMemo _paints = new();

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
            _isDirty = true;
            InvalidateVisual();
            RaiseValueChanged(old, _value);
        }
    }

    /// <inheritdoc />
    object? IValueEditor.Value => _value;

    /// <inheritdoc />
    public bool HasValue => true;

    /// <inheritdoc />
    public bool IsDirty => _isDirty;

    // ── Theming ───────────────────────────────────────────────────────────────────

    private KumikoColor _backgroundColor = KumikoColor.White;
    private KumikoColor _foregroundColor = new(30, 30, 30);
    private KumikoColor _accentColor = new(0, 120, 215);

    /// <summary>Applies colors from the active KumikoStyle.</summary>
    public void ApplyTheme(KumikoStyle style)
    {
        _backgroundColor = style.BackgroundColor;
        _foregroundColor = style.CellTextColor;
        _accentColor = style.SelectionColor;
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        // Background
        ctx.FillRect(Bounds, _paints.Get(_backgroundColor));

        // Border
        ctx.DrawRect(Bounds, _paints.Get(_isPressed ? _accentColor : new KumikoColor(180, 180, 180), strokeWidth: 1f));

        // TODO: draw the value representation inside Bounds using IDrawingContext methods.
        // Use KumikoColor, KumikoPaint, KumikoFont, KumikoRect: never SkiaSharp types.
    }

    // ── Input ─────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        _isPressed = true;
        InvalidateVisual();
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerUp(KumikoPointerEventArgs e)
    {
        _isPressed = false;
        // TODO: handle tap confirm
        InvalidateVisual();
        return true;
    }

    /// <inheritdoc />
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        if (e.Key == KumikoKey.Enter)
        {
            RaiseEditCompleted();   // the edit session commits and closes the editor
            return true;
        }
        return false;   // Escape and the rest are handled by the edit session
    }
}
```

Every public member needs XML documentation (doc warnings are errors), and an editor that animates overrides
`UpdateAnimation(float frameIntervalMs)` and returns true while it needs frames.

---

## `CellEditorFactory`: add cases

In `src/KumikoUI.Core/Editing/CellEditorFactory.cs`:

**`CreateEditor`**: add a case to the `column.ColumnType switch` (a new `KumikoColumnType`, see `add-column-type.prompt.md`) with a
private factory method next to the others, or, for a `Template` column, return the editor from the column's
`EditorFactory` or `EditorDescriptor`:

```csharp
KumikoColumnType.{NewType} => Create{Name}Editor(column, currentValue, cellBounds, initialCharacter, culture),
```

**`ApplyThemeToEditor` and `ApplyFontToEditor`**:
```csharp
case Drawn{Name} editor: editor.ApplyTheme(style); break;
```

The edited value is read back through `IValueEditor`, so `GetEditorValue` needs no case.

---

## Optional: `EditorDescriptor` (for XAML-declarable config)

```csharp
// src/KumikoUI.Core/Editing/{Name}EditorDescriptor.cs
using KumikoUI.Core.Components;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Declarative configuration for <see cref="Drawn{Name}"/> in XAML.
/// </summary>
public sealed class {Name}EditorDescriptor : EditorDescriptor
{
    /// <summary>TODO: describe the option. Default 1.</summary>
    public int Step { get; set; } = 1;

    /// <inheritdoc />
    public override DrawnComponent? CreateEditor(object? currentValue, KumikoRect cellBounds)
        => new Drawn{Name}
        {
            Bounds = cellBounds,
            Value  = ({ValueType}?)currentValue ?? default
        };
}
```

Override `CreateEditor(CellEditorContext context)` instead to read the typed character and the culture.

**XAML usage:**
```xml
<core:KumikoColumn Header="..." PropertyName="..." ColumnType="Template" Width="120">
    <core:KumikoColumn.EditorDescriptor>
        <editing:{Name}EditorDescriptor Step="2" />
    </core:KumikoColumn.EditorDescriptor>
</core:KumikoColumn>
```

---

## Rules checklist

- [ ] Class is `sealed` (unless it is a base), in namespace `KumikoUI.Core.Components`, and `internal` unless users create it
- [ ] No SkiaSharp APIs: only `IDrawingContext` primitives and `KumikoColor`/`KumikoPaint`/`KumikoFont`
- [ ] No allocation in `OnDraw`: reuse paints with a `PaintMemo` or fields
- [ ] `InvalidateVisual()` called whenever visual state changes
- [ ] `RaiseValueChanged(old, new)` called when value changes
- [ ] `RaiseEditCompleted()` called when the user confirms their selection
- [ ] Implements `IValueEditor`
- [ ] `ApplyTheme(KumikoStyle)` and `ApplyFont(KumikoFont)` implemented (no interface: `CellEditorFactory` calls them through `switch`)
- [ ] `CellEditorFactory` creates it and applies theme and font
- [ ] Tests in `tests/KumikoUI.Core.Tests/Components/`; public members documented
