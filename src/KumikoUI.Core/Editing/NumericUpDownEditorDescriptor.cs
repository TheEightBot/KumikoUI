using KumikoUI.Core.Components;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// XAML-declarable editor descriptor for numeric up/down controls.
/// Usage in XAML:
/// <code>
/// &lt;core:DataGridColumn Header="Rating" ColumnType="Template"&gt;
///     &lt;core:DataGridColumn.EditorDescriptor&gt;
///         &lt;editing:NumericUpDownEditorDescriptor Minimum="1" Maximum="5" Step="1" DecimalPlaces="0" /&gt;
///     &lt;/core:DataGridColumn.EditorDescriptor&gt;
/// &lt;/core:DataGridColumn&gt;
/// </code>
/// </summary>
public class NumericUpDownEditorDescriptor : EditorDescriptor
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

    public override DrawnComponent? CreateEditor(object? currentValue, GridRect cellBounds)
    {
        double value = 0;
        if (currentValue != null)
        {
            try { value = Convert.ToDouble(currentValue); }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) { /* use default */ }
        }

        return new DrawnNumericUpDown
        {
            Bounds = cellBounds,
            Minimum = Minimum,
            Maximum = Maximum,
            Step = Step,
            DecimalPlaces = DecimalPlaces,
            Format = Format,
            IsReadOnly = IsReadOnly,
            Value = value
        };
    }
}
