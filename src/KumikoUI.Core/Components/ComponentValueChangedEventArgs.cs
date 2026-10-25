using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Event args for component value changes.</summary>
public class ComponentValueChangedEventArgs : EventArgs
{
    public object? OldValue { get; }
    public object? NewValue { get; }

    public ComponentValueChangedEventArgs(object? oldValue, object? newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }
}
