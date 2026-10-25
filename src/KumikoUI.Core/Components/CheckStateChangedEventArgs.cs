using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Check state changed event args.</summary>
public class CheckStateChangedEventArgs : EventArgs
{
    /// <summary>The previous check state before the change.</summary>
    public DrawnCheckState OldState { get; }
    /// <summary>The new check state after the change.</summary>
    public DrawnCheckState NewState { get; }
    /// <summary>Initializes a new instance of the <see cref="CheckStateChangedEventArgs"/> class.</summary>
    /// <param name="oldState">The previous check state.</param>
    /// <param name="newState">The new check state.</param>
    public CheckStateChangedEventArgs(DrawnCheckState oldState, DrawnCheckState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}
