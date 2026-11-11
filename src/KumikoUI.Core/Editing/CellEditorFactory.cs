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
            KumikoColumnType.Date => CreateDateEditor(column, currentValue, cellBounds, initialCharacter, culture),
            KumikoColumnType.Boolean => null, // Toggled directly, no overlay editor
            KumikoColumnType.ComboBox => CreateComboBoxEditor(column, currentValue, cellBounds, initialCharacter),
            KumikoColumnType.Picker => CreatePickerEditor(column, currentValue, cellBounds),
            KumikoColumnType.Image => null, // Not editable
            KumikoColumnType.ProgressBar => null, // Read-only display
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

    /// <summary>
    /// Sets the font of an editor that shows text (text box, combo box, numeric up/down, date picker, scroll
    /// picker), so it draws and measures like the cell it replaces. Other editors are left alone.
    /// </summary>
    /// <param name="editor">The editor component.</param>
    /// <param name="font">The font of the cell: family, size, bold and italic.</param>
    public static void ApplyFontToEditor(DrawnComponent editor, KumikoFont font)
    {
        switch (editor)
        {
            case DrawnTextBox textBox: textBox.ApplyFont(font); break;
            case DrawnComboBox comboBox: comboBox.ApplyFont(font); break;
            case DrawnNumericUpDown numeric: numeric.ApplyFont(font); break;
            case DrawnDatePicker datePicker: datePicker.ApplyFont(font); break;
            case DrawnScrollPicker picker: picker.ApplyFont(font); break;
        }
    }

    /// <summary>
    /// Tells an editor the visible area its popups must stay inside (<see cref="DrawnComponent.PopupViewport"/>),
    /// and slides a scroll picker, which is drawn over the cell instead of beside it, into that area.
    /// </summary>
    /// <param name="editor">The editor component.</param>
    /// <param name="viewport">The grid's visible area; empty for no limit.</param>
    public static void ApplyViewportToEditor(DrawnComponent editor, KumikoRect viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0) return;
        editor.PopupViewport = viewport;
        if (editor is DrawnScrollPicker picker)
            picker.Bounds = PopupLayout.Clamp(picker.Bounds, viewport);
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
        KumikoColumn column, object? value, KumikoRect cellBounds, char? initialCharacter, CultureInfo culture)
    {
        var picker = new DrawnDatePicker
        {
            Bounds = cellBounds,
            IsVisible = true,
            Culture = culture,
            DateFormat = column.Format,
            MinDate = column.EditorMinDate,
            MaxDate = column.EditorMaxDate,
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

        // The typed character that opened the edit starts typing a date: a change by the user
        if (initialCharacter is { } typed && !char.IsControl(typed) && !char.IsWhiteSpace(typed))
        {
            picker.StartTrackingChanges();
            picker.OnKeyDown(new Input.KumikoKeyEventArgs { Key = Input.KumikoKey.Character, Character = typed });
        }

        return picker;
    }

    private static DrawnComboBox CreateComboBoxEditor(
        KumikoColumn column, object? value, KumikoRect cellBounds, char? initialCharacter)
    {
        var comboBox = new DrawnComboBox
        {
            Bounds = cellBounds,
            IsVisible = true,
            IsFilterable = column.EditorIsFilterable
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

        // The typed character that opened the edit jumps to its item (or starts the filter): a change by the user
        if (initialCharacter is { } typed && !char.IsControl(typed))
        {
            comboBox.StartTrackingChanges();
            comboBox.OnKeyDown(new Input.KumikoKeyEventArgs { Key = Input.KumikoKey.Character, Character = typed });
        }

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
            // Each item is shown as its display text and committed as its value, with the same selectors and
            // member paths as the combo box; without them, as its text and itself (an enum, a number, an object)
            var texts = new List<string>();
            var values = new List<object?>();
            foreach (var item in column.EditorItems)
            {
                texts.Add(item == null ? string.Empty
                    : column.EditorDisplaySelector != null ? column.EditorDisplaySelector(item) ?? string.Empty
                    : column.EditorDisplayMemberPath != null ? GetPathValue(item, column.EditorDisplayMemberPath)?.ToString() ?? string.Empty
                    : item.ToString() ?? string.Empty);
                values.Add(item == null ? null
                    : column.EditorValueSelector != null ? column.EditorValueSelector(item)
                    : column.EditorValueMemberPath != null ? GetPathValue(item, column.EditorValueMemberPath)
                    : item);
            }
            picker.SetItems(texts, values);
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

    private static object? GetPathValue(object item, string path) =>
        Data.KumikoAccessorRegistry.GetPath(item.GetType(), path).GetValue(item);

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
