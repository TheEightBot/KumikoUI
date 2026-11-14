using System.Globalization;
using KumikoUI.Core.Components;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// XAML-declarable editor descriptor for numeric up/down controls.
/// Usage in XAML:
/// <code>
/// &lt;core:KumikoColumn Header="Rating" ColumnType="Template"&gt;
///     &lt;core:KumikoColumn.EditorDescriptor&gt;
///         &lt;editing:NumericUpDownEditorDescriptor Minimum="1" Maximum="5" Step="1" DecimalPlaces="0" /&gt;
///     &lt;/core:KumikoColumn.EditorDescriptor&gt;
/// &lt;/core:KumikoColumn&gt;
/// </code>
/// </summary>
public sealed class NumericUpDownEditorDescriptor : EditorDescriptor
{
    /// <summary>Minimum allowed value.</summary>
    public double Minimum { get; set; } = double.MinValue;

    /// <summary>Maximum allowed value.</summary>
    public double Maximum { get; set; } = double.MaxValue;

    /// <summary>Increment/decrement step.</summary>
    public double Step { get; set; } = 1;

    /// <summary>Number of decimal places (-1 = auto).</summary>
    public int DecimalPlaces { get; set; } = -1;

    /// <summary>Display format string.</summary>
    public string Format { get; set; } = "G";

    /// <summary>Is the control read-only?</summary>
    public bool IsReadOnly { get; set; }

    /// <inheritdoc/>
    public override DrawnComponent? CreateEditor(object? currentValue, KumikoRect cellBounds) =>
        CreateEditor(new CellEditorContext(currentValue, cellBounds));

    /// <summary>
    /// Creates the numeric up/down in the grid's culture, holding the cell's number. A typed digit, sign or
    /// decimal separator that opened the edit starts typed entry with that character.
    /// </summary>
    /// <param name="context">The cell's value, bounds, culture and the character that opened the edit.</param>
    public override DrawnComponent? CreateEditor(CellEditorContext context)
    {
        var culture = context.Culture ?? CultureInfo.CurrentCulture;
        double value = 0;
        if (context.Value != null)
        {
            try { value = Convert.ToDouble(context.Value, culture); }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) { /* use default */ }
        }

        var editor = new DrawnNumericUpDown
        {
            Bounds = context.Bounds,
            Culture = culture,
            Minimum = Minimum,
            Maximum = Maximum,
            Step = Step,
            DecimalPlaces = DecimalPlaces,
            Format = Format,
            IsReadOnly = IsReadOnly,
            Value = value
        };

        if (context.InitialCharacter is { } typed && !IsReadOnly && IsNumberCharacter(typed, culture.NumberFormat))
        {
            // The typed character replaces the number: a change by the user
            editor.StartTrackingChanges();
            editor.OnKeyDown(new Input.KumikoKeyEventArgs { Key = Input.KumikoKey.Character, Character = typed });
        }

        return editor;
    }

    private static bool IsNumberCharacter(char c, NumberFormatInfo number) =>
        char.IsDigit(c) || c is '-' or '.' || number.NumberDecimalSeparator.Contains(c) || number.NegativeSign.Contains(c);
}
