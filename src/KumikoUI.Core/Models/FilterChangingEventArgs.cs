namespace KumikoUI.Core.Models;

/// <summary>
/// Event args for FilterChanging. Set Cancel=true to prevent.
/// </summary>
public class FilterChangingEventArgs : EventArgs
{
    public KumikoColumn Column { get; }
    public FilterDescription? NewFilter { get; }
    public bool Cancel { get; set; }

    public FilterChangingEventArgs(KumikoColumn column, FilterDescription? newFilter)
    {
        Column = column;
        NewFilter = newFilter;
    }
}
