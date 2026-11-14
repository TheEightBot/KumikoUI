---
mode: agent
description: Add a new KumikoColumnType and wire it end-to-end in KumikoUI
---

# Add a New Column Type

Adds a new `KumikoColumnType` enum value and wires it through every place that switches on the column type.
A new column type is a public API addition (see CHANGELOG and `docs/VERSIONING.md`): if an app only needs one custom
display or editor, use a `Template` column with `CustomCellRenderer`, `EditorDescriptor` or `EditorFactory` instead, which
needs no change to the library.

## What to ask the user before generating code

1. What is the **enum name** for the new type? (for example a rating)
2. What **value type** does the column display/edit? (`string`, `double`, `int`, `DateOnly`, etc.)
3. Does it need a **custom cell renderer**, or will the default `TextCellRenderer` suffice for display?
4. Does it need a **custom inline editor** (`DrawnComponent`), or reuse an existing one?
5. What XAML **configurable properties** (if any) does the editor expose?

---

## Files to create / modify

Search the source for an existing value (`KumikoColumnType.ProgressBar` and `KumikoColumnType.Date` touch most places)
and cover every hit.

| Action | File | What to change |
|---|---|---|
| **Modify** | `src/KumikoUI.Core/Models/KumikoColumnType.cs` | Add the enum value, with a doc comment |
| **Create** (if custom renderer) | `src/KumikoUI.Core/Rendering/{Type}CellRenderer.cs` | New `ICellRenderer` |
| **Modify** | `src/KumikoUI.Core/KumikoRenderer.cs` | Register the renderer for the type in the constructor (`_cellRenderers[...]`) |
| **Create** (if custom editor) | `src/KumikoUI.Core/Components/Drawn{Type}.cs` | New `DrawnComponent` implementing `IValueEditor` |
| **Modify** | `src/KumikoUI.Core/Editing/CellEditorFactory.cs` | `CreateEditor` case, and `ApplyThemeToEditor` / `ApplyFontToEditor` cases |
| **Modify** | `src/KumikoUI.Core/Editing/EditSession.cs` | The value type a commit converts the editor's text to |
| **Modify** | `src/KumikoUI.Core/Models/FilterCondition.cs` | How `Evaluate` filters the type (numeric, date, text, boolean) |
| **Modify** | `src/KumikoUI.Core/Models/KumikoStrings.cs` | The sort menu labels for the type |
| **Modify** | `src/KumikoUI.Core/Models/KumikoColumn.cs` | Defaults that depend on the type (alignment, format) |
| **Create / modify** | `tests/KumikoUI.Core.Tests/` | Editor, factory, renderer, filter and sort tests; `CellEditorFactoryTests` and `KumikoRendererTests` show the shape |
| **Modify** | `CHANGELOG.md`, README "Columns" | Document the new type |

---

## Step 1: `KumikoColumnType` enum

In `src/KumikoUI.Core/Models/KumikoColumnType.cs`, add the new value with a doc comment, at the end so existing values keep
their numbers:

```csharp
public enum KumikoColumnType
{
    // ... existing values ...

    /// <summary>{what the column holds}.</summary>
    {NewType},   // ← add here
}
```

---

## Step 2: Cell renderer (display)

Only create a new renderer if the visual appearance differs from text. `TextCellRenderer` draws `Text`, `Numeric`, `Date`,
`Template`, `ComboBox` and `Picker` cells; a new type without its own renderer is registered with the same instance.

See `add-cell-renderer.prompt.md` for the full `ICellRenderer` template. Key rule: implement the renderer inside
`KumikoUI.Core.Rendering`; use only `IDrawingContext` members and `KumikoColor`/`KumikoPaint`/`KumikoFont`; allocate nothing
per call.

Register it in the `KumikoRenderer` constructor:

```csharp
_cellRenderers[KumikoColumnType.{NewType}] = new {Type}CellRenderer();
// or, to display as text:
_cellRenderers[KumikoColumnType.{NewType}] = textRenderer;
```

---

## Step 3: Inline editor

Only create a new editor if existing editors (`DrawnTextBox`, `DrawnComboBox`, `DrawnDatePicker`, `DrawnNumericUpDown`,
`DrawnScrollPicker`) are insufficient. See `add-drawn-component.prompt.md` for the full `DrawnComponent` template. A column
that is display-only (`Boolean`, `Image`, `ProgressBar`) has no overlay editor: `CreateEditor` returns null for it.

---

## Step 4: `CellEditorFactory` wiring

In `src/KumikoUI.Core/Editing/CellEditorFactory.cs`:

### `CreateEditor`: add a case to the `column.ColumnType switch`

```csharp
KumikoColumnType.{NewType} => Create{Type}Editor(column, currentValue, cellBounds, initialCharacter, culture),
```

with a private factory next to `CreateTextEditor` and the others, which builds the editor, sets `Bounds` and the value,
and applies the column's descriptor when it has one. The editor's value is read back through `IValueEditor.Value`
(`GetEditorValue` needs no change).

### `ApplyThemeToEditor` and `ApplyFontToEditor`: add a case

```csharp
case Drawn{Type} editor: editor.ApplyTheme(style); break;
case Drawn{Type} editor: editor.ApplyFont(font); break;
```

---

## Step 5: Commit conversion, filtering and sorting

- `EditSession` maps a column type to the CLR type a text value is converted to on commit (`typeof(decimal?)` for
  `Numeric`, `typeof(DateTime?)` for `Date`, ...): add the new type.
- `FilterCondition.Evaluate` chooses how a cell value is compared for a column type.
- `KumikoStrings` has the sort menu labels per type (`SortNumericAscending`, `SortDateAscending`, ...; the filter popup shows them): add labels, or map
  the type to existing ones.

---

## Step 6: Verification checklist

- [ ] `KumikoColumnType.{NewType}` compiles in `KumikoUI.Core` and every switch over `KumikoColumnType` handles it
- [ ] `KumikoRenderer` has a renderer registered for it
- [ ] `CellEditorFactory` has the `CreateEditor`, `ApplyThemeToEditor` and `ApplyFontToEditor` cases (or returns no editor on purpose)
- [ ] Can declare a column in XAML on both hosts: `ColumnType="{NewType}"` without error
- [ ] Edit session opens and closes cleanly with `EditTriggers="DoubleTap"`
- [ ] The value is written back to `KumikoSource` (test with an `ObservableCollection`)
- [ ] Sorting and filtering the column work
- [ ] Tests pass (`dotnet test tests/KumikoUI.Core.Tests -c Release`), including `RenderAllocationTests`; public members are documented

---

## XAML usage example (once implemented)

```xml
<core:KumikoColumn Header="Rating" PropertyName="Rating"
                     ColumnType="{NewType}"
                     Width="120" />
```

If an `EditorDescriptor` was created:
```xml
<core:KumikoColumn Header="Rating" PropertyName="Rating"
                     ColumnType="Template" Width="120">
    <core:KumikoColumn.EditorDescriptor>
        <editing:{Type}EditorDescriptor Minimum="1" Maximum="5" />
    </core:KumikoColumn.EditorDescriptor>
</core:KumikoColumn>
```
