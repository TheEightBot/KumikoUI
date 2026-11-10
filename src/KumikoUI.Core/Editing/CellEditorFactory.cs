using System.Globalization;
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
    /// <param name="column">The edited column.</param>
    /// <param name="currentValue">The cell's value.</param>
    /// <param name="cellBounds">Where the editor goes.</param>
    /// <param name="initialCharacter">A typed character that opened the edit, which replaces the text.</param>
    /// <param name="selectionMode">How the text is selected when editing starts.</param>
    /// <param name="culture">The culture text editors show numbers and dates in (the grid's
    /// <see cref="KumikoSource.Culture"/>), so the text parses back to the same value; the current culture
    /// when null.</param>
    public static DrawnComponent? CreateEditor(
        KumikoColumn column, object? currentValue,
        KumikoRect cellBounds, char? initialCharacter = null,
        EditTextSelectionMode selectionMode = EditTextSelectionMode.SelectAll,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var editor = column.ColumnType switch
        {
            KumikoColumnType.Text => CreateTextEditor(column, currentValue, cellBounds, initialCharacter, selectionMode, culture),
            KumikoColumnType.Numeric => CreateNumericEditor(column, currentValue, cellBounds, initialCharacter, selectionMode, culture),
            KumikoColumnType.Date => CreateDateEditor(column, currentValue, cellBounds, culture),
            KumikoColumnType.Boolean => null, // Toggled directly, no overlay editor
            KumikoColumnType.ComboBox => CreateComboBoxEditor(column, currentValue, cellBounds),
            KumikoColumnType.Picker => CreatePickerEditor(column, currentValue, cellBounds),
            KumikoColumnType.Image => null, // Not editable
            KumikoColumnType.Template => CreateTemplateEditor(column, currentValue, cellBounds, initialCharacter, culture),
            _ => null
        };

        // From here on, a value change is the user's: it makes the editor dirty (IValueEditor.IsDirty)
        if (editor is { IsTrackingChanges: false })
            editor.StartTrackingChanges();
        return editor;
    }

    /// <summary>
    /// Extract the current value from an active editor, as the editor holds it: the text of a text box,
    /// the number of a numeric up/down, the date of a date picker, the selected value of a combo box or
    /// picker. <see cref="EditSession.CommitEdit"/> converts it to the property type with
    /// <see cref="Data.KumikoValueConverter"/>.
    /// </summary>
    public static object? GetEditorValue(DrawnComponent editor, KumikoColumn column) =>
        editor is IValueEditor valueEditor ? valueEditor.Value : null;

    /// <summary>
    /// Applies theme colors from a <see cref="KumikoStyle"/> to the given editor component.
    /// Dispatches to the component-specific <c>ApplyTheme</c> method.
    /// </summary>
    public static void ApplyThemeToEditor(DrawnComponent editor, KumikoStyle style)
    {
        switch (editor)
        {
            case DrawnTextBox textBox: textBox.ApplyTheme(style); break;
            case DrawnComboBox comboBox: comboBox.ApplyTheme(style); break;
            case DrawnNumericUpDown numeric: numeric.ApplyTheme(style); break;
            case DrawnDatePicker datePicker: datePicker.ApplyTheme(style); break;
            case DrawnScrollPicker picker: picker.ApplyTheme(style); break;
            case DrawnActionButtons actionButtons: actionButtons.ApplyTheme(style); break;
        }
    }

    // ── Editor creation ─────────────────────────────────────────

    /// <summary>A value as editable text: no display format, in the grid's culture, so it parses back.</summary>
    internal static string FormatForEditing(object? value, CultureInfo culture) => value switch
    {
        null => string.Empty,
        IFormattable formattable => formattable.ToString(null, culture),
        _ => value.ToString() ?? string.Empty,
    };

    private static DrawnTextBox CreateTextEditor(
        KumikoColumn column, object? value, KumikoRect cellBounds, char? initialCharacter,
        EditTextSelectionMode selectionMode, CultureInfo culture)
    {
        var textBox = new DrawnTextBox
        {
            Bounds = cellBounds,
            Font = new KumikoFont("Default", 13),
            IsVisible = true,
            Padding = 4f,
            InitialSelectionMode = initialCharacter.HasValue
                ? EditTextSelectionMode.CursorAtEnd  // Typing trigger → cursor at end
                : selectionMode
        };

        textBox.Text = FormatForEditing(value, culture);
        if (initialCharacter.HasValue)
        {
            // The typed character replaces the value: a change by the user
            textBox.StartTrackingChanges();
            textBox.Text = initialCharacter.Value.ToString();
        }

        return textBox;
    }

    private static DrawnComponent CreateNumericEditor(
        KumikoColumn column, object? value, KumikoRect cellBounds, char? initialCharacter,
        EditTextSelectionMode selectionMode, CultureInfo culture)
    {
        var textBox = new DrawnTextBox
        {
            Bounds = cellBounds,
            Font = new KumikoFont("Default", 13),
            IsVisible = true,
            NumericOnly = true,
            Padding = 4f,
            InitialSelectionMode = initialCharacter.HasValue
                ? EditTextSelectionMode.CursorAtEnd  // Typing trigger → cursor at end
                : selectionMode
        };

        var number = culture.NumberFormat;
        if (initialCharacter.HasValue &&
            (char.IsDigit(initialCharacter.Value) ||
             initialCharacter.Value == '-' ||
             initialCharacter.Value == '.' ||
             number.NumberDecimalSeparator.Contains(initialCharacter.Value) ||
             number.NegativeSign.Contains(initialCharacter.Value)))
        {
            // The typed character replaces the value: a change by the user
            textBox.Text = FormatForEditing(value, culture);
            textBox.StartTrackingChanges();
            textBox.Text = initialCharacter.Value.ToString();
        }
        else
        {
            textBox.Text = FormatForEditing(value, culture);
        }

        return textBox;
    }

    private static DrawnDatePicker CreateDateEditor(
        KumikoColumn column, object? value, KumikoRect cellBounds, CultureInfo culture)
    {
        var picker = new DrawnDatePicker
        {
            Bounds = cellBounds,
            IsVisible = true,
            Culture = culture
        };

        DateTime? initial = value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            string text when DateTime.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out var parsed) => parsed,
            _ => null,
        };

        // An empty cell opens with nothing selected; a date keeps its time of day through the edit
        picker.SelectedDate = initial;
        picker.TimeOfDay = initial?.TimeOfDay ?? TimeSpan.Zero;

        // Auto-open the calendar when the editor is created
        picker.OpenCalendar();

        return picker;
    }

    private static DrawnComboBox CreateComboBoxEditor(
        KumikoColumn column, object? value, KumikoRect cellBounds)
    {
        var comboBox = new DrawnComboBox
        {
            Bounds = cellBounds,
            IsVisible = true
        };

        if (column.EditorItems != null)
        {
            // Paths and selectors first: SetItemsFromSource reads them while it builds the items.
            comboBox.DisplayMemberPath = column.EditorDisplayMemberPath;
            comboBox.ValueMemberPath = column.EditorValueMemberPath;
            comboBox.DisplaySelector = column.EditorDisplaySelector;
            comboBox.ValueSelector = column.EditorValueSelector;
            comboBox.SetItemsFromSource(column.EditorItems);
        }

        if (value != null)
            comboBox.SetSelectedValue(value);

        // Auto-open the dropdown when the editor is created (like DatePicker.OpenCalendar)
        comboBox.OpenDropdown();

        return comboBox;
    }

    private static DrawnScrollPicker CreatePickerEditor(
        KumikoColumn column, object? value, KumikoRect cellBounds)
    {
        var picker = new DrawnScrollPicker
        {
            IsVisible = true
        };

        if (column.EditorItems != null)
        {
            // Each item is shown as text and committed as itself (an enum, a number, an object)
            var values = column.EditorItems.Cast<object?>().ToList();
            picker.SetItems(values.Select(v => v?.ToString() ?? string.Empty).ToList(), values);
        }

        // Expand the picker to popup-size below the cell so items are visible.
        // The picker needs height = VisibleItemCount * ItemHeight to display its scroll-wheel.
        float pickerHeight = picker.VisibleItemCount * picker.ItemHeight;
        picker.Bounds = new KumikoRect(
            cellBounds.X,
            cellBounds.Y,
            cellBounds.Width,
            pickerHeight);

        // Select the cell's value, or nothing when it isn't one of the items
        if (!picker.SelectValue(value))
            picker.ClearSelection();

        return picker;
    }

    private static DrawnComponent? CreateTemplateEditor(
        KumikoColumn column, object? value, KumikoRect cellBounds, char? initialCharacter, CultureInfo culture)
    {
        // Priority: context factory → explicit factory → EditorDescriptor → renderer self-creates → text fallback.
        // Every custom path gets the typing-trigger character, the legacy factory delegate excepted.
        var context = new CellEditorContext(value, cellBounds, initialCharacter, culture);

        if (column.EditorFactory != null)
            return column.EditorFactory(context);

        if (column.CustomEditorFactory != null)
            return column.CustomEditorFactory(value, cellBounds);

        if (column.EditorDescriptor != null)
            return column.EditorDescriptor.CreateEditor(context);

        if (column.CustomCellRenderer is ICellEditorProvider editorProvider)
            return editorProvider.CreateEditor(context);

        // Fallback to text editor
        return CreateTextEditor(column, value, cellBounds, initialCharacter, EditTextSelectionMode.SelectAll, culture);
    }
}
