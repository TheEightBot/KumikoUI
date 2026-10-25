namespace KumikoUI.Core.Models;

/// <summary>
/// Event args for FilterChanged.
/// </summary>
public class FilterChangedEventArgs : EventArgs
{
    public KumikoColumn Column { get; }
    public IReadOnlyList<FilterDescription> ActiveFilters { get; }

    public FilterChangedEventArgs(KumikoColumn column, IReadOnlyList<FilterDescription> activeFilters)
    {
        Column = column;
        ActiveFilters = activeFilters;
    }
}
