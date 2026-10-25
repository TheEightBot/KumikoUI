using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

using KumikoUI.Core.Components;
using KumikoUI.Core.Models;

/// <summary>
/// Optional companion to <see cref="ICellRenderer"/> that allows a renderer to
/// also act as the cell editor factory for <see cref="GridColumnType.Template"/>
/// columns.
/// </summary>
/// <remarks>
/// When a <c>Template</c> column's <see cref="GridColumn.CustomCellRenderer"/>
/// implements this interface <em>and</em> no explicit
/// <see cref="GridColumn.CustomEditorFactory"/> or
/// <see cref="GridColumn.EditorDescriptor"/> is set, the grid automatically
/// calls <see cref="CreateEditor"/> to produce the interactive overlay editor.
/// This lets a single object (e.g. <see cref="ActionButtonsCellRenderer"/>)
/// handle both display and editing without any code-behind wiring.
/// </remarks>
public interface ICellEditorProvider
{
    /// <summary>
    /// Creates the interactive editor for a cell.
    /// </summary>
    /// <param name="value">The cell's current value (row item for empty <c>PropertyName</c>).</param>
    /// <param name="bounds">The cell rectangle in which the editor should be placed.</param>
    /// <returns>A <see cref="DrawnComponent"/> editor, or <see langword="null"/> to skip editing.</returns>
    DrawnComponent? CreateEditor(object? value, GridRect bounds);
}
