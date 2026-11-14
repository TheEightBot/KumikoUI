namespace KumikoUI.Core.Models;

/// <summary>
/// Event args for FilterChanged.
/// </summary>
public sealed class FilterChangedEventArgs : EventArgs
{
    /// <summary>Column whose filter changed.</summary>
    public KumikoColumn Column { get; }
    /// <summary>Every filter that is active after the change.</summary>
    public IReadOnlyList<FilterDescription> ActiveFilters { get; }

    /// <summary>Initializes the event data.</summary>
    /// <param name="column">Column whose filter changed.</param>
    /// <param name="activeFilters">Every filter that is active after the change.</param>
    public FilterChangedEventArgs(KumikoColumn column, IReadOnlyList<FilterDescription> activeFilters)
    {
        Column = column;
        ActiveFilters = activeFilters;
    }
}
