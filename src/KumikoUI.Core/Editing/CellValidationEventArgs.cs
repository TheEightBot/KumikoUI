using KumikoUI.Core.Models;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Reports a validation result that is not plainly valid when an edit is committed: a conversion
/// failure, an error from <see cref="EditSession.CellValidator"/> or from the item
/// (<see cref="System.ComponentModel.IDataErrorInfo"/>, <see cref="System.ComponentModel.INotifyDataErrorInfo"/>),
/// a setter that threw, or a warning.
/// </summary>
public sealed class CellValidationEventArgs : EventArgs
{
    /// <summary>Creates the args.</summary>
    public CellValidationEventArgs(int rowIndex, int columnIndex, KumikoColumn column, object item,
        object? value, CellValidationResult result, bool committed)
    {
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        Column = column;
        Item = item;
        Value = value;
        Result = result;
        Committed = committed;
    }

    /// <summary>The visible row of the edited cell.</summary>
    public int RowIndex { get; }

    /// <summary>The column index of the edited cell.</summary>
    public int ColumnIndex { get; }

    /// <summary>The edited column.</summary>
    public KumikoColumn Column { get; }

    /// <summary>The edited item.</summary>
    public object Item { get; }

    /// <summary>The value that was validated: converted when the conversion succeeded, otherwise as the editor held it.</summary>
    public object? Value { get; }

    /// <summary>The validation result, with its message and severity.</summary>
    public CellValidationResult Result { get; }

    /// <summary>The message for the user.</summary>
    public string? Message => Result.ErrorMessage;

    /// <summary>The severity.</summary>
    public ValidationSeverity Severity => Result.Severity;

    /// <summary>
    /// Whether the value was written: false for an error (the editor stays open) and for a warning shown
    /// for the first time; true when a warning or informational message accompanies a committed value.
    /// </summary>
    public bool Committed { get; }
}
