using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Provides data for the <see cref="DrawnFocusManager.FocusChanged"/> event.</summary>
public class GridFocusChangedEventArgs : EventArgs
{
    /// <summary>The component that previously held focus, or <c>null</c> if none.</summary>
    public DrawnComponent? OldFocus { get; }
    /// <summary>The component that now holds focus, or <c>null</c> if focus was cleared.</summary>
    public DrawnComponent? NewFocus { get; }

    /// <summary>Initializes a new instance of the <see cref="GridFocusChangedEventArgs"/> class.</summary>
    /// <param name="oldFocus">The previously focused component.</param>
    /// <param name="newFocus">The newly focused component.</param>
    public GridFocusChangedEventArgs(DrawnComponent? oldFocus, DrawnComponent? newFocus)
    {
        OldFocus = oldFocus;
        NewFocus = newFocus;
    }
}
