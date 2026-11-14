using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Event args for component value changes.</summary>
public sealed class ComponentValueChangedEventArgs : EventArgs
{
    /// <summary>Value before the change.</summary>
    public object? OldValue { get; }
    /// <summary>Value after the change.</summary>
    public object? NewValue { get; }

    /// <summary>Initializes the event data.</summary>
    /// <param name="oldValue">Value before the change.</param>
    /// <param name="newValue">Value after the change.</param>
    public ComponentValueChangedEventArgs(object? oldValue, object? newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }
}
