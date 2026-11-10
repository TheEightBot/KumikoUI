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

    public override DrawnComponent? CreateEditor(object? currentValue, KumikoRect cellBounds)
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

    /// <summary>Creates the text box; a character typed to open the edit replaces the text.</summary>
    public override DrawnComponent? CreateEditor(CellEditorContext context)
    {
        var editor = CreateEditor(context.Value, context.Bounds);
        if (editor is DrawnTextBox textBox && context.InitialCharacter is { } typed)
        {
            // The typed character replaces the value: a change by the user
            textBox.InitialSelectionMode = EditTextSelectionMode.CursorAtEnd;
            textBox.StartTrackingChanges();
            textBox.Text = typed.ToString();
        }
        return editor;
    }
}
