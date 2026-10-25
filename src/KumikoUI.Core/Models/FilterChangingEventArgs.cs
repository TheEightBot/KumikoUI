namespace KumikoUI.Core.Models;

/// <summary>
/// Event args for FilterChanging. Set Cancel=true to prevent.
/// </summary>
public class FilterChangingEventArgs : EventArgs
{
    public GridColumn Column { get; }
    public FilterDescription? NewFilter { get; }
    public bool Cancel { get; set; }

    public FilterChangingEventArgs(GridColumn column, FilterDescription? newFilter)
    {
        Column = column;
        NewFilter = newFilter;
    }
}
