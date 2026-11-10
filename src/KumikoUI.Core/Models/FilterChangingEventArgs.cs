namespace KumikoUI.Core.Models;

/// <summary>
/// Event args for FilterChanging. Set Cancel=true to prevent.
/// </summary>
public class FilterChangingEventArgs : EventArgs
{
    /// <summary>Column whose filter is changing.</summary>
    public KumikoColumn Column { get; }
    /// <summary>The filter about to be applied, or null when the column filter is being cleared.</summary>
    public FilterDescription? NewFilter { get; }
    /// <summary>Set to true to keep the current filter and skip the change.</summary>
    public bool Cancel { get; set; }

    /// <summary>Initializes the event data.</summary>
    /// <param name="column">Column whose filter is changing.</param>
    /// <param name="newFilter">The filter about to be applied, or null when clearing.</param>
    public FilterChangingEventArgs(KumikoColumn column, FilterDescription? newFilter)
    {
        Column = column;
        NewFilter = newFilter;
    }
}
