using KumikoUI.Core.Components;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// XAML-declarable editor descriptor for text box editors.
/// </summary>
public class TextEditorDescriptor : EditorDescriptor
{
    /// <summary>Restrict to numeric characters only.</summary>
    public bool NumericOnly { get; set; }

    public override DrawnComponent? CreateEditor(object? currentValue, GridRect cellBounds)
    {
        return new DrawnTextBox
        {
            Bounds = cellBounds,
            Text = currentValue?.ToString() ?? string.Empty,
            NumericOnly = NumericOnly,
            IsVisible = true,
            Padding = 4f
        };
    }
}
