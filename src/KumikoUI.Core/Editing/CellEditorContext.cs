using System.Globalization;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// What a custom editor factory is told about the cell it creates an editor for. Used by
/// <see cref="Models.KumikoColumn.EditorFactory"/>, <see cref="EditorDescriptor.CreateEditor(CellEditorContext)"/>
/// and <see cref="ICellEditorProvider.CreateEditor(CellEditorContext)"/>. New information is added here
/// rather than to the factory signatures.
/// </summary>
/// <param name="Value">The cell's current value (the row item for an empty <c>PropertyName</c>).</param>
/// <param name="Bounds">The cell rectangle in which the editor is placed.</param>
/// <param name="InitialCharacter">The character typed to open the edit, or null when the edit was opened
/// another way (double-tap, F2, tap). A text-like editor shows it in place of the value.</param>
/// <param name="Culture">The culture the grid shows and parses numbers and dates in.</param>
public sealed record CellEditorContext(
    object? Value,
    KumikoRect Bounds,
    char? InitialCharacter = null,
    CultureInfo? Culture = null);
