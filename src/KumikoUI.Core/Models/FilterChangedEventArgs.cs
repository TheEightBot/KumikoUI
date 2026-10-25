namespace KumikoUI.Core.Models;

/// <summary>
/// Event args for FilterChanged.
/// </summary>
public class FilterChangedEventArgs : EventArgs
{
    public GridColumn Column { get; }
    public IReadOnlyList<FilterDescription> ActiveFilters { get; }

    public FilterChangedEventArgs(GridColumn column, IReadOnlyList<FilterDescription> activeFilters)
    {
        Column = column;
        ActiveFilters = activeFilters;
    }
}
