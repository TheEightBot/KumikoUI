namespace KumikoUI.Core.Input;

/// <summary>
/// Which sides of a swiped row run their outermost item when the finger swipes the row most of the way across and
/// lifts (see <see cref="KumikoInputController.FullSwipeMode"/>).
/// </summary>
public enum KumikoFullSwipeMode
{
    /// <summary>No full swipe: the row stops at its items' width (with a little give past it) and opens.</summary>
    None,

    /// <summary>A full swipe toward the end edge runs the first of <see cref="KumikoInputController.LeadingSwipeItems"/>.</summary>
    Leading,

    /// <summary>A full swipe toward the start edge runs the first of <see cref="KumikoInputController.TrailingSwipeItems"/>.</summary>
    Trailing,

    /// <summary>A full swipe runs the outermost item on either side.</summary>
    Both,
}
