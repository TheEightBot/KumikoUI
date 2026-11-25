using KumikoUI.Core.Components;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Base class for XAML-declarable editor descriptors.
/// Subclasses configure how a cell editor is created without
/// requiring code-behind lambdas or factory delegates.
/// </summary>
public abstract class EditorDescriptor
{
    /// <summary>
    /// Create an editor component for the cell described by <paramref name="context"/>: its value
    /// (<see cref="CellEditorContext.Value"/>), the rectangle to place the editor in
    /// (<see cref="CellEditorContext.Bounds"/>), the grid's culture and the character typed to open the edit
    /// (<see cref="CellEditorContext.InitialCharacter"/>).
    /// </summary>
    /// <param name="context">The cell the editor is created for.</param>
    /// <returns>The editor, or <see langword="null"/> to skip editing.</returns>
    public abstract DrawnComponent? CreateEditor(CellEditorContext context);
}
