using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>Provides data for the <see cref="KumikoInputController.CellTapped"/> event.</summary>
public sealed class KumikoCellTappedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="KumikoCellTappedEventArgs"/> class.</summary>
    /// <param name="rowIndex">The tapped visible row.</param>
    /// <param name="columnIndex">The tapped column index.</param>
    /// <param name="column">The tapped column.</param>
    /// <param name="dataItem">The row's data item.</param>
    /// <param name="x">Horizontal position in grid viewport coordinates.</param>
    /// <param name="y">Vertical position in grid viewport coordinates.</param>
    /// <param name="clickCount">1 for a single tap, 2 for the second tap of a double tap.</param>
    public KumikoCellTappedEventArgs(
        int rowIndex, int columnIndex, KumikoColumn column, object dataItem, float x, float y, int clickCount)
    {
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        Column = column;
        DataItem = dataItem;
        X = x;
        Y = y;
        ClickCount = clickCount;
    }

    /// <summary>Zero-based visible row of the tapped cell.</summary>
    public int RowIndex { get; }

    /// <summary>Zero-based column index of the tapped cell.</summary>
    public int ColumnIndex { get; }

    /// <summary>The column of the tapped cell.</summary>
    public KumikoColumn Column { get; }

    /// <summary>The data item of the tapped row.</summary>
    public object DataItem { get; }

    /// <summary>Horizontal position of the tap in grid viewport coordinates.</summary>
    public float X { get; }

    /// <summary>Vertical position of the tap in grid viewport coordinates.</summary>
    public float Y { get; }

    /// <summary>1 for a single tap, 2 for the second tap of a double tap.</summary>
    public int ClickCount { get; }
}
