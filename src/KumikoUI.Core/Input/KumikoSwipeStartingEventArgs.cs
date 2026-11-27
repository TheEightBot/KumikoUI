namespace KumikoUI.Core.Input;

/// <summary>
/// Data for <see cref="KumikoInputController.SwipeStarting"/>: a finger started to swipe a row sideways. Set
/// <see cref="Cancel"/> to keep that row from swiping; the gesture then pans the grid instead.
/// </summary>
public sealed class KumikoSwipeStartingEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="dataItem">The item of the row being swiped.</param>
    /// <param name="rowIndex">The visible row being swiped.</param>
    /// <param name="direction">The side the swipe reveals items on.</param>
    public KumikoSwipeStartingEventArgs(object dataItem, int rowIndex, KumikoSwipeDirection direction)
    {
        DataItem = dataItem;
        RowIndex = rowIndex;
        Direction = direction;
    }

    /// <summary>The item of the row being swiped.</summary>
    public object DataItem { get; }

    /// <summary>The zero-based visible row being swiped.</summary>
    public int RowIndex { get; }

    /// <summary>The side the swipe reveals items on.</summary>
    public KumikoSwipeDirection Direction { get; }

    /// <summary>Set to true to keep this row from swiping (a per-row opt-out); the finger pans the grid instead.</summary>
    public bool Cancel { get; set; }
}
