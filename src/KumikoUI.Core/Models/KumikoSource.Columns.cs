namespace KumikoUI.Core.Models;

// Column visibility and order notifications, and the default layout the column chooser resets to (#335, spec #106).
public partial class KumikoSource
{
    private static readonly KumikoColumnsChangedEventArgs ResetColumnsArgs = new(KumikoColumnsChangeKind.Reset, null);

    // Inside a BeginUpdate batch: the one column change so far, or Reset once there were several.
    private KumikoColumnsChangedEventArgs? _pendingColumnsChange;

    // ResetColumnLayout raises one Reset itself, not one event per column it touches.
    private bool _suppressColumnsChanged;

    // The layout ResetColumnLayout restores: the columns in their default order, and whether each was visible.
    private KumikoColumn[] _defaultColumnOrder = Array.Empty<KumikoColumn>();
    private bool[] _defaultColumnVisibility = Array.Empty<bool>();

    /// <summary>
    /// Raised when a column was shown or hidden (<see cref="KumikoColumn.IsVisible"/>, <see cref="SetColumnVisible"/>),
    /// moved to another pane (<see cref="KumikoColumn.FreezeMode"/>) or moved in <see cref="Columns"/>
    /// (<see cref="ReorderColumn"/>, a header drag, the column chooser), and once by <see cref="ResetColumnLayout"/>.
    /// Not raised for a width or sort change, nor by <see cref="SetColumns"/>. Inside
    /// <see cref="BeginUpdate"/> and <see cref="EndUpdate"/> the changes are raised once, at the end of the batch: as
    /// they were when there was one, as <see cref="KumikoColumnsChangeKind.Reset"/> when there were several. Save a
    /// layout (<see cref="KumikoLayoutState"/>) from here to keep what the user chose.
    /// </summary>
    public event EventHandler<KumikoColumnsChangedEventArgs>? ColumnsChanged;

    /// <summary>
    /// Incremented by every change a column list drawn from <see cref="Columns"/> must follow: the columns replaced or
    /// moved, or any column property changed (a header, visibility, freeze mode, <see cref="KumikoColumn.ShowInChooser"/>).
    /// </summary>
    internal int ColumnsVersion { get; private set; }

    /// <summary>The number of columns whose <see cref="KumikoColumn.IsVisible"/> is true.</summary>
    public int VisibleColumnCount => DisplayColumns.Count;

    /// <summary>
    /// Shows or hides <paramref name="column"/>, as the column chooser does. Hiding is refused when the column's
    /// <see cref="KumikoColumn.AllowHide"/> is false, and for the last visible column (a selection column does not
    /// count, so the grid always keeps a data column). A change raises <see cref="ColumnsChanged"/> and repaints, as
    /// setting <see cref="KumikoColumn.IsVisible"/> does; a current cell on a hidden column moves to the nearest
    /// visible column.
    /// </summary>
    /// <param name="column">One of <see cref="Columns"/>.</param>
    /// <param name="visible">True to show the column, false to hide it.</param>
    /// <returns>False when hiding was refused; true otherwise, also when the column already had that state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="column"/> is null.</exception>
    /// <exception cref="ArgumentException">The column is not one of <see cref="Columns"/>.</exception>
    public bool SetColumnVisible(KumikoColumn column, bool visible)
    {
        ArgumentNullException.ThrowIfNull(column);
        VerifyAccess();
        if (IndexOfColumnReference(column) < 0)
            throw new ArgumentException("The column is not one of this source's columns.", nameof(column));
        if (column.IsVisible == visible) return true;
        if (!visible && !CanHideColumn(column)) return false;
        column.IsVisible = visible; // OnColumnPropertyChanged raises ColumnsChanged and repaints
        return true;
    }

    /// <summary>
    /// Whether <paramref name="column"/> may be hidden by the user: it is visible, its
    /// <see cref="KumikoColumn.AllowHide"/> is true, and another visible column that is not a selection column remains.
    /// </summary>
    internal bool CanHideColumn(KumikoColumn column) =>
        column.IsVisible && column.AllowHide && HasOtherVisibleDataColumn(column);

    /// <summary>Whether a visible column other than <paramref name="column"/>, and not a selection column, remains.</summary>
    internal bool HasOtherVisibleDataColumn(KumikoColumn column)
    {
        for (int i = 0; i < _columns.Count; i++)
        {
            var other = _columns[i];
            if (!ReferenceEquals(other, column) && other.IsVisible && other.ColumnType != KumikoColumnType.Selection)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Captures the current order of <see cref="Columns"/> and the visibility of each column as the layout
    /// <see cref="ResetColumnLayout"/> restores (the column chooser's Reset button). <see cref="SetColumns"/> captures it
    /// for new columns, so the default is the columns as the app defined them; call this after changing them in code
    /// if Reset should keep the change. Applying a saved <see cref="KumikoLayoutState"/> does not capture it, so Reset
    /// returns to the app's layout, not the restored one.
    /// </summary>
    public void MarkDefaultColumnLayout()
    {
        VerifyAccess();
        CaptureDefaultColumnLayout();
    }

    /// <summary>
    /// Restores the layout captured by <see cref="MarkDefaultColumnLayout"/> (or by <see cref="SetColumns"/>): the order
    /// of <see cref="Columns"/> and the visibility of each column. Width, freeze mode, sort, filter and grouping are left
    /// as they are. When anything changed, raises <see cref="ColumnOrderChanged"/> (if the order changed), one
    /// <see cref="ColumnsChanged"/> with <see cref="KumikoColumnsChangeKind.Reset"/> and one repaint. Does nothing when the
    /// layout already is the default.
    /// </summary>
    public void ResetColumnLayout()
    {
        VerifyAccess();
        if (IsDefaultColumnLayout) return;

        // The default order of the columns the source still has; any column added since keeps its relative place, last.
        var present = new HashSet<KumikoColumn>(_columns, ReferenceEqualityComparer.Instance);
        var desired = new List<KumikoColumn>(_columns.Count);
        var visibility = new List<(KumikoColumn Column, bool Visible)>(_defaultColumnOrder.Length);
        for (int i = 0; i < _defaultColumnOrder.Length; i++)
        {
            var column = _defaultColumnOrder[i];
            if (!present.Remove(column)) continue;
            desired.Add(column);
            visibility.Add((column, _defaultColumnVisibility[i]));
        }
        desired.AddRange(_columns.Where(present.Contains));

        BeginUpdate();
        _suppressColumnsChanged = true;
        try
        {
            if (!desired.SequenceEqual(_columns))
            {
                _columns.Clear();
                _columns.AddRange(desired);
                ColumnOrderChanged?.Invoke(this, EventArgs.Empty);
                NotifyRedraw();
            }
            foreach (var (column, visible) in visibility)
                column.IsVisible = visible;
        }
        finally
        {
            _suppressColumnsChanged = false;
        }
        RaiseColumnsChanged(ResetColumnsArgs);
        EndUpdate();
    }

    /// <summary>Whether the order and visibility of the columns are those <see cref="ResetColumnLayout"/> restores.</summary>
    internal bool IsDefaultColumnLayout
    {
        get
        {
            if (_defaultColumnOrder.Length != _columns.Count) return false;
            for (int i = 0; i < _columns.Count; i++)
            {
                if (!ReferenceEquals(_defaultColumnOrder[i], _columns[i]) ||
                    _defaultColumnVisibility[i] != _columns[i].IsVisible)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// The visible column nearest to the hidden <c>Columns[columnIndex]</c> in display order (pane by pane, as
    /// <see cref="DisplayColumns"/>): the first visible column after its slot, else the last one before it; -1 when no
    /// column is visible.
    /// </summary>
    internal int NearestVisibleColumnIndex(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _columns.Count) return -1;
        int targetPane = PaneOf(_columns[columnIndex]);
        int before = -1;
        bool passed = false;
        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < _columns.Count; i++)
            {
                if (i == columnIndex && pass == targetPane)
                {
                    passed = true;
                    continue;
                }
                var column = _columns[i];
                if (!column.IsVisible || PaneOf(column) != pass) continue;
                if (passed) return i;
                before = i;
            }
        }
        return before;
    }

    // ── Hooks from KumikoSource.cs ───────────────────────────────

    /// <summary>A column property changed: column lists follow, and a visibility or pane change raises <see cref="ColumnsChanged"/>.</summary>
    private void OnColumnLayoutPropertyChanged(KumikoColumn column, string? propertyName)
    {
        ColumnsVersion++;
        if (propertyName == nameof(KumikoColumn.IsVisible))
            RaiseColumnsChanged(new KumikoColumnsChangedEventArgs(KumikoColumnsChangeKind.Visibility, column));
        else if (propertyName == nameof(KumikoColumn.FreezeMode))
            RaiseColumnsChanged(new KumikoColumnsChangedEventArgs(KumikoColumnsChangeKind.Freeze, column));
    }

    /// <summary>A column moved in <see cref="Columns"/> (<see cref="ReorderColumn"/>).</summary>
    private void OnColumnMoved(KumikoColumn column, int fromIndex, int toIndex) =>
        RaiseColumnsChanged(new KumikoColumnsChangedEventArgs(KumikoColumnsChangeKind.Order, column, fromIndex, toIndex));

    /// <summary>
    /// <see cref="SetColumns"/> replaced the columns: capture the default layout, unless the same columns came back (a
    /// grid reconnecting to its page hands the source its collection again, and that must not make the user's layout
    /// the default).
    /// </summary>
    private void OnColumnsReplaced()
    {
        ColumnsVersion++;
        if (!HasSameColumnsAsDefault()) CaptureDefaultColumnLayout();
    }

    /// <summary>The outermost <see cref="EndUpdate"/>: raise the batch's column change, if any.</summary>
    private void FlushColumnsChanged()
    {
        var pending = _pendingColumnsChange;
        if (pending == null) return;
        _pendingColumnsChange = null;
        ColumnsChanged?.Invoke(this, pending);
    }

    private void RaiseColumnsChanged(KumikoColumnsChangedEventArgs args)
    {
        ColumnsVersion++;
        if (_suppressColumnsChanged) return;
        if (_updateDepth > 0)
        {
            _pendingColumnsChange = _pendingColumnsChange == null ? args : ResetColumnsArgs;
            return;
        }
        ColumnsChanged?.Invoke(this, args);
    }

    private void CaptureDefaultColumnLayout()
    {
        _defaultColumnOrder = _columns.ToArray();
        _defaultColumnVisibility = new bool[_defaultColumnOrder.Length];
        for (int i = 0; i < _defaultColumnOrder.Length; i++)
            _defaultColumnVisibility[i] = _defaultColumnOrder[i].IsVisible;
        ColumnsVersion++;
    }

    private bool HasSameColumnsAsDefault()
    {
        if (_defaultColumnOrder.Length != _columns.Count || _columns.Count == 0) return false;
        var defaults = new HashSet<KumikoColumn>(_defaultColumnOrder, ReferenceEqualityComparer.Instance);
        return _columns.All(defaults.Contains);
    }

    private int IndexOfColumnReference(KumikoColumn column)
    {
        for (int i = 0; i < _columns.Count; i++)
            if (ReferenceEquals(_columns[i], column)) return i;
        return -1;
    }
}
