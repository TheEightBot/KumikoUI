using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace KumikoUI.Core.Models;

/// <summary>
/// Event args for the SortColumnsChanging event. Set Cancel=true to prevent the sort.
/// </summary>
public class SortColumnsChangingEventArgs : EventArgs
{
    /// <summary>The column whose sort state is being toggled.</summary>
    public GridColumn Column { get; }

    /// <summary>Whether this is a multi-sort operation (modifier key held).</summary>
    public bool IsMultiSort { get; }

    /// <summary>The new sort direction that will be applied.</summary>
    public GridSortDirection NewDirection { get; }

    /// <summary>Set to true to cancel the sort operation.</summary>
    public bool Cancel { get; set; }

    public SortColumnsChangingEventArgs(GridColumn column, bool isMultiSort, GridSortDirection newDirection)
    {
        Column = column;
        IsMultiSort = isMultiSort;
        NewDirection = newDirection;
    }
}
