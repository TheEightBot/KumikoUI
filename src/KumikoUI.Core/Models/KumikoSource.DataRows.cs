namespace KumikoUI.Core.Models;

// The number of data rows in the view (#321): what a selection column's check-all state compares the selection to.
public partial class KumikoSource
{
    // The count and what it was counted for: the view's version, its row list and its length.
    private int _dataRowCount;
    private int _dataRowCountVersion = -1;
    private List<FlatViewRow>? _dataRowCountFlat;
    private int _dataRowCountRows = -1;

    /// <summary>
    /// The number of data rows in the current view: <see cref="RowCount"/> without the group header and group summary
    /// rows. Without grouping it equals <see cref="RowCount"/>. Rows inside collapsed groups are not part of the view
    /// and are not counted, like <see cref="KumikoSelectionModel.SelectAll"/> does not select them. Cheap to read:
    /// the view is counted once per change, not per call.
    /// </summary>
    public int DataRowCount
    {
        get
        {
            EnsureView();
            var view = _view;
            var flat = view.FlatView;
            if (flat == null) return view.ViewIndices.Count;

            if (_dataRowCountVersion != ViewVersion || !ReferenceEquals(_dataRowCountFlat, flat) ||
                _dataRowCountRows != flat.Count)
            {
                int count = 0;
                for (int i = 0; i < flat.Count; i++)
                {
                    if (flat[i].Kind == FlatRowKind.Data) count++;
                }
                _dataRowCount = count;
                _dataRowCountVersion = ViewVersion;
                _dataRowCountFlat = flat;
                _dataRowCountRows = flat.Count;
            }
            return _dataRowCount;
        }
    }
}
