using KumikoUI.Core.Models;

namespace KumikoUI.Maui;

/// <summary>
/// Column order and saved layouts: <see cref="GetLayoutState"/> and <see cref="ApplyLayoutState"/>, and keeping
/// <see cref="Columns"/> and <see cref="GroupDescriptions"/> in step when the data source changes their order.
/// </summary>
public partial class KumikoDataGrid
{
    // True while the grid copies the data source's column or group order into its own collections, so their
    // CollectionChanged handlers do not copy it back.
    private bool _syncingFromSource;

    /// <summary>
    /// The grid's current layout: column order, width, visibility and freeze mode, the sort, the column filters
    /// and the grouping. Save it with <see cref="KumikoLayoutState.ToJson"/>.
    /// </summary>
    /// <returns>A new <see cref="KumikoLayoutState"/>, independent of the grid.</returns>
    public KumikoLayoutState GetLayoutState() => KumikoLayoutState.Capture(_dataSource);

    /// <summary>
    /// Restore a layout taken with <see cref="GetLayoutState"/> (or read with
    /// <see cref="KumikoLayoutState.FromJson"/>), as one change. Columns are matched by
    /// <see cref="KumikoColumn.Key"/> or <see cref="KumikoColumn.PropertyName"/>; see
    /// <see cref="KumikoLayoutState.ApplyTo"/>. <see cref="Columns"/> and <see cref="GroupDescriptions"/> follow.
    /// </summary>
    /// <param name="state">The layout to apply.</param>
    public void ApplyLayoutState(KumikoLayoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.ApplyTo(_dataSource);
        SyncGroupDescriptionsFromSource();
        InvalidateSurface();
    }

    // A header drag, KumikoSource.SetDisplayIndex or a layout moved a column: move it in Columns too, so the
    // collection keeps the on-screen order and copying it back (Reconnect, a later change) does not undo the move.
    private void OnDataSourceColumnOrderChanged(object? sender, EventArgs e)
    {
        var order = _dataSource.Columns;
        var columns = Columns;
        if (columns.Count != order.Count || columns.SequenceEqual(order) || !order.All(columns.Contains)) return;

        _syncingFromSource = true;
        try
        {
            for (int i = 0; i < order.Count; i++)
            {
                int from = columns.IndexOf(order[i]);
                if (from != i) columns.Move(from, i);
            }
        }
        finally
        {
            _syncingFromSource = false;
        }
    }

    private void SyncGroupDescriptionsFromSource()
    {
        var groups = GroupDescriptions;
        if (groups.SequenceEqual(_dataSource.GroupDescriptions)) return;

        _syncingFromSource = true;
        try
        {
            groups.Clear();
            foreach (var group in _dataSource.GroupDescriptions)
                groups.Add(group);
        }
        finally
        {
            _syncingFromSource = false;
        }
    }
}
