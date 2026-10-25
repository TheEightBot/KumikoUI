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
    public DataGridColumn Column { get; }

    /// <summary>All columns that currently have an active sort, ordered by SortOrder.</summary>
    public IReadOnlyList<DataGridColumn> ActiveSortColumns { get; }

    public SortColumnsChangedEventArgs(DataGridColumn column, IReadOnlyList<DataGridColumn> activeSortColumns)
    {
        Column = column;
        ActiveSortColumns = activeSortColumns;
    }
}
