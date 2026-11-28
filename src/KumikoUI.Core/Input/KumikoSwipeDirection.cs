namespace KumikoUI.Core.Input;

/// <summary>Which side of a row a swipe reveals items on (see <see cref="KumikoInputController.AllowSwiping"/>).</summary>
public enum KumikoSwipeDirection
{
    /// <summary>
    /// The start edge: the finger moves toward the end edge (to the right in a left-to-right layout) and reveals
    /// <see cref="KumikoInputController.LeadingSwipeItems"/>.
    /// </summary>
    Leading,

    /// <summary>
    /// The end edge: the finger moves toward the start edge (to the left in a left-to-right layout) and reveals
    /// <see cref="KumikoInputController.TrailingSwipeItems"/>.
    /// </summary>
    Trailing,
}
