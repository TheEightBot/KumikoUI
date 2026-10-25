namespace KumikoUI.Core.Models;

/// <summary>
/// Event args for FilterChanged.
/// </summary>
public class FilterChangedEventArgs : EventArgs
{
    public DataGridColumn Column { get; }
    public IReadOnlyList<FilterDescription> ActiveFilters { get; }

    public FilterChangedEventArgs(DataGridColumn column, IReadOnlyList<FilterDescription> activeFilters)
    {
        Column = column;
        ActiveFilters = activeFilters;
    }
}
