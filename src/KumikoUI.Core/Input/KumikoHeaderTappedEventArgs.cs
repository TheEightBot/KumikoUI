using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>Provides data for the <see cref="KumikoInputController.HeaderTapped"/> event.</summary>
public class KumikoHeaderTappedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="KumikoHeaderTappedEventArgs"/> class.</summary>
    /// <param name="columnIndex">The tapped column index.</param>
    /// <param name="column">The tapped column.</param>
    /// <param name="x">Horizontal position in grid viewport coordinates.</param>
    /// <param name="y">Vertical position in grid viewport coordinates.</param>
    /// <param name="modifiers">The modifier keys held during the tap.</param>
    public KumikoHeaderTappedEventArgs(int columnIndex, KumikoColumn column, float x, float y, InputModifiers modifiers)
    {
        ColumnIndex = columnIndex;
        Column = column;
        X = x;
        Y = y;
        Modifiers = modifiers;
    }

    /// <summary>Zero-based index of the tapped column.</summary>
    public int ColumnIndex { get; }

    /// <summary>The tapped column.</summary>
    public KumikoColumn Column { get; }

    /// <summary>Horizontal position of the tap in grid viewport coordinates.</summary>
    public float X { get; }

    /// <summary>Vertical position of the tap in grid viewport coordinates.</summary>
    public float Y { get; }

    /// <summary>The modifier keys held during the tap (Ctrl or Shift add the column to a multi-column sort).</summary>
    public InputModifiers Modifiers { get; }
}
