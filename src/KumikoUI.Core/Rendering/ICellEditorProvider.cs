namespace KumikoUI.Core.Rendering;

using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;

/// <summary>
/// Optional companion to <see cref="ICellRenderer"/> that allows a renderer to
/// also act as the cell editor factory for <see cref="KumikoColumnType.Template"/>
/// columns.
/// </summary>
/// <remarks>
/// When a <c>Template</c> column's <see cref="KumikoColumn.CustomCellRenderer"/>
/// implements this interface <em>and</em> no explicit
/// <see cref="KumikoColumn.EditorFactory"/> or
/// <see cref="KumikoColumn.EditorDescriptor"/> is set, the grid automatically
/// calls <see cref="CreateEditor(CellEditorContext)"/> to produce the interactive overlay editor.
/// This lets a single object (e.g. <see cref="ActionButtonsCellRenderer"/>)
/// handle both display and editing without any code-behind wiring.
/// </remarks>
public interface ICellEditorProvider
{
    /// <summary>
    /// Creates the interactive editor for the cell described by <paramref name="context"/>: its value
    /// (<see cref="CellEditorContext.Value"/>, the row item for an empty <c>PropertyName</c>), the rectangle
    /// to place the editor in (<see cref="CellEditorContext.Bounds"/>), the grid's culture and the character
    /// typed to open the edit (<see cref="CellEditorContext.InitialCharacter"/>).
    /// </summary>
    /// <param name="context">The cell the editor is created for.</param>
    /// <returns>A <see cref="DrawnComponent"/> editor, or <see langword="null"/> to skip editing.</returns>
    DrawnComponent? CreateEditor(CellEditorContext context);
}
