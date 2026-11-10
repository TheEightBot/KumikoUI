namespace KumikoUI.Core.Editing;

/// <summary>
/// An editor that holds a value for its cell. <see cref="EditSession.CommitEdit"/> writes
/// <see cref="Value"/> only when the editor <see cref="HasValue">has one</see> and the user
/// <see cref="IsDirty">changed it</see>, so opening an editor and leaving (a tap elsewhere, Tab, Enter)
/// never overwrites the cell with a value the user didn't choose. Every built-in editor implements it;
/// a custom editor (<see cref="Models.KumikoColumn.EditorFactory"/>, <see cref="Rendering.ICellEditorProvider"/>)
/// implements it to have its value committed. Editors that don't implement it never write.
/// </summary>
public interface IValueEditor
{
    /// <summary>
    /// The value to commit, as the editor holds it (text, a number, a date, the selected item's value).
    /// The edit session converts it to the property type.
    /// </summary>
    object? Value { get; }

    /// <summary>
    /// Whether the editor holds a value: false when nothing is selected (a combo box or picker whose cell
    /// value isn't in its list, a date picker opened on an empty cell).
    /// </summary>
    bool HasValue { get; }

    /// <summary>Whether the user changed the value since the editor opened.</summary>
    bool IsDirty { get; }
}
