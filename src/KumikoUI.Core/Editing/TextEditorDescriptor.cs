using KumikoUI.Core.Components;

namespace KumikoUI.Core.Editing;

/// <summary>
/// XAML-declarable editor descriptor for text box editors.
/// </summary>
public sealed class TextEditorDescriptor : EditorDescriptor
{
    /// <summary>Restrict to numeric characters only.</summary>
    public bool NumericOnly { get; set; }

    /// <summary>Creates the text box; a character typed to open the edit replaces the text.</summary>
    /// <param name="context">The cell's value, bounds and the character that opened the edit.</param>
    public override DrawnComponent? CreateEditor(CellEditorContext context)
    {
        var textBox = new DrawnTextBox
        {
            Bounds = context.Bounds,
            Text = context.Value?.ToString() ?? string.Empty,
            NumericOnly = NumericOnly,
            IsVisible = true,
            Padding = 4f
        };
        if (context.InitialCharacter is { } typed)
        {
            // The typed character replaces the value: a change by the user
            textBox.InitialSelectionMode = EditTextSelectionMode.CursorAtEnd;
            textBox.StartTrackingChanges();
            textBox.Text = typed.ToString();
        }
        return textBox;
    }
}
