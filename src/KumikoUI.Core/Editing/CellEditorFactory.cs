using KumikoUI.Core.Components;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Creates appropriate drawn editor components for each column type.
/// </summary>
public static class CellEditorFactory
{
    /// <summary>
    /// Create an editor component for the given column type,
    /// positioned at the cell bounds.
    /// </summary>
    public static DrawnComponent? CreateEditor(
        DataGridColumn column, object? currentValue,
        GridRect cellBounds, char? initialCharacter = null,
        EditTextSelectionMode selectionMode = EditTextSelectionMode.SelectAll)
    {
        return column.ColumnType switch
        {
            DataGridColumnType.Text => CreateTextEditor(column, currentValue, cellBounds, initialCharacter, selectionMode),
            DataGridColumnType.Numeric => CreateNumericEditor(column, currentValue, cellBounds, initialCharacter, selectionMode),
            DataGridColumnType.Date => CreateDateEditor(column, currentValue, cellBounds),
            DataGridColumnType.Boolean => null, // Toggled directly, no overlay editor
            DataGridColumnType.ComboBox => CreateComboBoxEditor(column, currentValue, cellBounds),
            DataGridColumnType.Picker => CreatePickerEditor(column, currentValue, cellBounds),
            DataGridColumnType.Image => null, // Not editable
            DataGridColumnType.Template => CreateTemplateEditor(column, currentValue, cellBounds),
            _ => null
        };
    }

    /// <summary>
    /// Extract the current value from an active editor.
    /// </summary>
    public static object? GetEditorValue(DrawnComponent editor, DataGridColumn column)
    {
        return editor switch
        {
            DrawnTextBox textBox => ConvertTextToValue(textBox.Text, column),
            DrawnNumericUpDown numeric => numeric.Value,
            DrawnDatePicker datePicker => datePicker.SelectedDate,
            DrawnComboBox comboBox => comboBox.SelectedValue,
            DrawnScrollPicker picker => picker.SelectedValue,
            _ => null
        };
    }

    /// <summary>
    /// Applies theme colors from a <see cref="DataGridStyle"/> to the given editor component.
    /// Dispatches to the component-specific <c>ApplyTheme</c> method.
    /// </summary>
    public static void ApplyThemeToEditor(DrawnComponent editor, DataGridStyle style)
    {
        switch (editor)
        {
            case DrawnTextBox textBox: textBox.ApplyTheme(style); break;
            case DrawnComboBox comboBox: comboBox.ApplyTheme(style); break;
            case DrawnNumericUpDown numeric: numeric.ApplyTheme(style); break;
            case DrawnDatePicker datePicker: datePicker.ApplyTheme(style); break;
            case DrawnScrollPicker picker: picker.ApplyTheme(style); break;
        }
    }

    // ── Editor creation ─────────────────────────────────────────

    private static DrawnTextBox CreateTextEditor(
        DataGridColumn column, object? value, GridRect cellBounds, char? initialCharacter,
        EditTextSelectionMode selectionMode)
    {
        var textBox = new DrawnTextBox
        {
            Bounds = cellBounds,
            Font = new GridFont("Default", 13),
            IsVisible = true,
            Padding = 4f,
            InitialSelectionMode = initialCharacter.HasValue
                ? EditTextSelectionMode.CursorAtEnd  // Typing trigger → cursor at end
                : selectionMode
        };

        if (initialCharacter.HasValue)
        {
            textBox.Text = initialCharacter.Value.ToString();
        }
        else
        {
            textBox.Text = value?.ToString() ?? string.Empty;
        }

        return textBox;
    }

    private static DrawnComponent CreateNumericEditor(
        DataGridColumn column, object? value, GridRect cellBounds, char? initialCharacter,
        EditTextSelectionMode selectionMode)
    {
        var textBox = new DrawnTextBox
        {
            Bounds = cellBounds,
            Font = new GridFont("Default", 13),
            IsVisible = true,
            NumericOnly = true,
            Padding = 4f,
            InitialSelectionMode = initialCharacter.HasValue
                ? EditTextSelectionMode.CursorAtEnd  // Typing trigger → cursor at end
                : selectionMode
        };

        if (initialCharacter.HasValue &&
            (char.IsDigit(initialCharacter.Value) ||
             initialCharacter.Value == '-' ||
             initialCharacter.Value == '.'))
        {
            textBox.Text = initialCharacter.Value.ToString();
        }
        else
        {
            textBox.Text = value?.ToString() ?? string.Empty;
        }

        return textBox;
    }

    private static DrawnDatePicker CreateDateEditor(
        DataGridColumn column, object? value, GridRect cellBounds)
    {
        var picker = new DrawnDatePicker
        {
            Bounds = cellBounds,
            IsVisible = true
        };

        if (value is DateTime dt)
            picker.SelectedDate = dt;
        else if (value is DateTimeOffset dto)
            picker.SelectedDate = dto.DateTime;
        else if (DateTime.TryParse(value?.ToString(), out var parsed))
            picker.SelectedDate = parsed;

        // Auto-open the calendar when the editor is created
        picker.OpenCalendar();

        return picker;
    }

    private static DrawnComboBox CreateComboBoxEditor(
        DataGridColumn column, object? value, GridRect cellBounds)
    {
        var comboBox = new DrawnComboBox
        {
            Bounds = cellBounds,
            IsVisible = true
        };

        if (column.EditorItems != null)
        {
            comboBox.SetItemsFromSource(column.EditorItems);
            if (column.EditorDisplayMemberPath != null)
                comboBox.DisplayMemberPath = column.EditorDisplayMemberPath;
            if (column.EditorValueMemberPath != null)
                comboBox.ValueMemberPath = column.EditorValueMemberPath;
        }

        if (value != null)
            comboBox.SetSelectedValue(value);

        // Auto-open the dropdown when the editor is created (like DatePicker.OpenCalendar)
        comboBox.OpenDropdown();

        return comboBox;
    }

    private static DrawnScrollPicker CreatePickerEditor(
        DataGridColumn column, object? value, GridRect cellBounds)
    {
        var picker = new DrawnScrollPicker
        {
            IsVisible = true
        };

        if (column.EditorItems != null)
        {
            var items = new List<string>();
            foreach (var item in column.EditorItems)
                items.Add(item?.ToString() ?? string.Empty);
            picker.SetItems(items);
        }

        // Expand the picker to popup-size below the cell so items are visible.
        // The picker needs height = VisibleItemCount * ItemHeight to display its scroll-wheel.
        float pickerHeight = picker.VisibleItemCount * picker.ItemHeight;
        picker.Bounds = new GridRect(
            cellBounds.X,
            cellBounds.Y,
            cellBounds.Width,
            pickerHeight);

        if (value != null)
        {
            var valueStr = value.ToString() ?? string.Empty;
            var idx = picker.Items.ToList().IndexOf(valueStr);
            if (idx >= 0) picker.SelectedIndex = idx;
        }

        return picker;
    }

    private static DrawnComponent? CreateTemplateEditor(
        DataGridColumn column, object? value, GridRect cellBounds)
    {
        // Template columns: prefer CustomEditorFactory, then EditorDescriptor, then text fallback
        if (column.CustomEditorFactory != null)
            return column.CustomEditorFactory(value, cellBounds);

        if (column.EditorDescriptor != null)
            return column.EditorDescriptor.CreateEditor(value, cellBounds);

        // Fallback to text editor
        return CreateTextEditor(column, value, cellBounds, null, EditTextSelectionMode.SelectAll);
    }

    // ── Value conversion ────────────────────────────────────────

    private static object? ConvertTextToValue(string text, DataGridColumn column)
    {
        return column.ColumnType switch
        {
            DataGridColumnType.Numeric => ParseNumeric(text),
            DataGridColumnType.Date => DateTime.TryParse(text, out var dt) ? dt : text,
            _ => text
        };
    }

    private static object? ParseNumeric(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (int.TryParse(text, out var intVal)) return intVal;
        if (long.TryParse(text, out var longVal)) return longVal;
        if (double.TryParse(text, out var doubleVal)) return doubleVal;
        if (decimal.TryParse(text, out var decVal)) return decVal;
        return text;
    }
}
