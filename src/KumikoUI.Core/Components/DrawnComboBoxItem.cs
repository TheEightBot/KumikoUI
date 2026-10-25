using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>An item in a combo box.</summary>
public class DrawnComboBoxItem
{
    /// <summary>Text shown in the dropdown list.</summary>
    public string DisplayText { get; }
    /// <summary>Underlying value associated with this item.</summary>
    public object? Value { get; }

    /// <summary>Initializes a new <see cref="DrawnComboBoxItem"/>.</summary>
    /// <param name="displayText">Text displayed in the dropdown list.</param>
    /// <param name="value">The value associated with this item; defaults to <paramref name="displayText"/> when <see langword="null"/>.</param>
    public DrawnComboBoxItem(string displayText, object? value = null)
    {
        DisplayText = displayText;
        Value = value ?? displayText;
    }
}
