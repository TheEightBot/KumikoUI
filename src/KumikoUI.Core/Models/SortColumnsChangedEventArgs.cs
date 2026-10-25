using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace KumikoUI.Core.Models;

/// <summary>
/// Event args for the SortColumnsChanged event.
/// </summary>
public class SortColumnsChangedEventArgs : EventArgs
{
    /// <summary>The column whose sort state was toggled.</summary>
    public KumikoColumn Column { get; }

    /// <summary>All columns that currently have an active sort, ordered by SortOrder.</summary>
    public IReadOnlyList<KumikoColumn> ActiveSortColumns { get; }

    public SortColumnsChangedEventArgs(KumikoColumn column, IReadOnlyList<KumikoColumn> activeSortColumns)
    {
        Column = column;
        ActiveSortColumns = activeSortColumns;
    }
}
