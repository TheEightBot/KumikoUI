using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>
/// Data for <see cref="KumikoInputController.SwipeEnded"/>: a swiped row closed again, after running one of its items or
/// without running anything.
/// </summary>
public sealed class KumikoSwipeEndedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="dataItem">The item of the row that was swiped.</param>
    /// <param name="direction">The side the swipe revealed items on.</param>
    /// <param name="invokedItem">The swipe item that ran, or null when the row closed without running one.</param>
    public KumikoSwipeEndedEventArgs(object dataItem, KumikoSwipeDirection direction, IActionButtonDefinition? invokedItem)
    {
        DataItem = dataItem;
        Direction = direction;
        InvokedItem = invokedItem;
    }

    /// <summary>The item of the row that was swiped. The row may be gone already, when the item that ran removed it.</summary>
    public object DataItem { get; }

    /// <summary>The side the swipe revealed items on.</summary>
    public KumikoSwipeDirection Direction { get; }

    /// <summary>
    /// The swipe item that ran (a tap on it, or a full swipe that ran the outermost one), or null when the row closed
    /// without running one: a tap elsewhere, a scroll, a change of the rows, a swipe released short of opening, or
    /// <see cref="KumikoInputController.CloseSwipe"/>.
    /// </summary>
    public IActionButtonDefinition? InvokedItem { get; }
}
