---
mode: agent
description: Add a new DataGridColumnType and wire it end-to-end in KumikoUI
---

# Add a New Column Type

Adds a new `DataGridColumnType` enum value and wires it through all five required
touch-points in the codebase.

## What to ask the user before generating code

1. What is the **enum name** for the new type? (e.g. `Rating`, `Color`, `Url`)
2. What **value type** does the column display/edit? (`string`, `double`, `int`, `DateOnly`, etc.)
3. Does it need a **custom cell renderer**, or will the default `TextCellRenderer` suffice for display?
4. Does it need a **custom inline editor** (`DrawnComponent`), or reuse an existing one?
5. What XAML **configurable properties** (if any) does the editor expose?

---

## Files to create / modify (all five are required)

| Action | File | What to change |
|---|---|---|
| **Modify** | `src/KumikoUI.Core/Models/DataGridColumn.cs` | Add enum value to `DataGridColumnType` |
| **Create** (if custom renderer) | `src/KumikoUI.Core/Rendering/{Type}CellRenderer.cs` | New `ICellRenderer` |
| **Create** (if custom editor) | `src/KumikoUI.Core/Components/Drawn{Type}.cs` | New `DrawnComponent` |
| **Modify** | `src/KumikoUI.Core/Editing/CellEditorFactory.cs` | Add cases for create, get value, apply theme |
| **Modify** | `src/KumikoUI.Core/DataGridRenderer.cs` | Dispatch new type to cell renderer |

---

## Step 1 — `DataGridColumnType` enum

In `src/KumikoUI.Core/Models/DataGridColumn.cs`, add the new value with a doc comment:

```csharp
public enum DataGridColumnType
{
    Text,
    Numeric,
    Boolean,
    Date,
    ComboBox,
    Picker,
    Image,
    Template,
    {NewType},   // ← add here
}
```

---

## Step 2 — Cell renderer (display)

Only create a new renderer if the visual appearance differs from text.
If text display is fine, the existing `TextCellRenderer` will be used automatically.

See `add-cell-renderer.prompt.md` for the full `ICellRenderer` template.

Key rule: implement the renderer inside `KumikoUI.Core.Rendering`; use only
`IDrawingContext` methods and `GridColor`/`GridPaint`/`GridFont` — no SkiaSharp.

---

## Step 3 — Inline editor

Only create a new editor if existing editors (`DrawnTextBox`, `DrawnComboBox`,
`DrawnDatePicker`, `DrawnNumericUpDown`, `DrawnScrollPicker`) are insufficient.

See `add-drawn-component.prompt.md` for the full `DrawnComponent` template.

---

## Step 4 — `CellEditorFactory` wiring

In `src/KumikoUI.Core/Editing/CellEditorFactory.cs`, add three cases:

### `CreateEditor`
```csharp
case DataGridColumnType.{NewType}:
    var editor = new Drawn{Type}
    {
        Bounds = cellBounds,
        Value  = ({ValueType}?)currentValue ?? default
    };
    if (column.EditorDescriptor is {Type}EditorDescriptor desc)
        editor.ApplyDescriptor(desc);
    return editor;
```

### `GetEditorValue`
```csharp
case Drawn{Type} e:
    return e.Value;
```

### `ApplyThemeToEditor`
```csharp
case Drawn{Type} e:
    e.ApplyTheme(style);
    break;
```

---

## Step 5 — Renderer dispatch

In `src/KumikoUI.Core/DataGridRenderer.cs`, locate the section that selects a
cell renderer by column type and add a case:

```csharp
// Inside the method that resolves ICellRenderer for a column:
DataGridColumnType.{NewType} => new {Type}CellRenderer(),
// or, if reusing text renderer:
DataGridColumnType.{NewType} => _textCellRenderer,
```

---

## Step 6 — Verification checklist

- [ ] `DataGridColumnType.{NewType}` compiles in `KumikoUI.Core`
- [ ] `CellEditorFactory` switch has `CreateEditor`, `GetEditorValue`, and `ApplyThemeToEditor` cases
- [ ] `DataGridRenderer` dispatches to the renderer (no missing-case compiler warning)
- [ ] Can declare column in XAML: `ColumnType="{NewType}"` without error
- [ ] Edit session opens and closes cleanly with `EditTriggers="DoubleTap"`
- [ ] Value is written back to `DataGridSource` (test with a bound `ObservableCollection`)

---

## XAML usage example (once implemented)

```xml
<core:DataGridColumn Header="Rating" PropertyName="Rating"
                     ColumnType="{NewType}"
                     Width="120" />
```

If an `EditorDescriptor` was created:
```xml
<core:DataGridColumn Header="Rating" PropertyName="Rating"
                     ColumnType="{NewType}" Width="120">
    <core:DataGridColumn.EditorDescriptor>
        <editing:{Type}EditorDescriptor Minimum="1" Maximum="5" />
    </core:DataGridColumn.EditorDescriptor>
</core:DataGridColumn>
```
