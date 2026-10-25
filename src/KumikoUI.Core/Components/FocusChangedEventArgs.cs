using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Provides data for the <see cref="DrawnFocusManager.FocusChanged"/> event.</summary>
public class FocusChangedEventArgs : EventArgs
{
    /// <summary>The component that previously held focus, or <c>null</c> if none.</summary>
    public DrawnComponent? OldFocus { get; }
    /// <summary>The component that now holds focus, or <c>null</c> if focus was cleared.</summary>
    public DrawnComponent? NewFocus { get; }

    /// <summary>Initializes a new instance of the <see cref="FocusChangedEventArgs"/> class.</summary>
    /// <param name="oldFocus">The previously focused component.</param>
    /// <param name="newFocus">The newly focused component.</param>
    public FocusChangedEventArgs(DrawnComponent? oldFocus, DrawnComponent? newFocus)
    {
        OldFocus = oldFocus;
        NewFocus = newFocus;
    }
}
