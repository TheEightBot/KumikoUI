using System.Collections.ObjectModel;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui;

/// <summary>
/// The selection column of <see cref="KumikoDataGrid"/> (#321): <see cref="ShowSelectionColumn"/> adds a
/// <see cref="KumikoSelectionColumn"/> the grid owns, <see cref="SelectOnRowClick"/> turns off selecting by tapping
/// a row, and the check-all box announces its toggles to the screen reader.
/// </summary>
public partial class KumikoDataGrid
{
    // The column ShowSelectionColumn inserted, kept so it can be taken out again
    private KumikoSelectionColumn? _ownedSelectionColumn;
    private bool _syncingSelectionColumn;

    // ── ShowSelectionColumn ──

    /// <summary>Identifies the <see cref="ShowSelectionColumn"/> bindable property.</summary>
    public static readonly BindableProperty ShowSelectionColumnProperty = BindableProperty.Create(
        nameof(ShowSelectionColumn), typeof(bool), typeof(KumikoDataGrid), false,
        propertyChanged: (b, _, _) => ((KumikoDataGrid)b).UpdateSelectionColumn());

    /// <summary>
    /// Whether the grid shows a <see cref="KumikoSelectionColumn"/> of row check boxes, with a tri-state check-all
    /// box in its header, as the first column. Default <c>false</c>. The grid owns that column: setting the property
    /// adds it to <see cref="Columns"/> at index 0 and clearing it removes it again; it is added again when
    /// <see cref="Columns"/> is replaced or cleared. When <see cref="Columns"/> already holds a
    /// <see cref="KumikoSelectionColumn"/> you declared, the property does nothing (no second column). The selection
    /// follows <see cref="GridSelectionMode"/>; <see cref="SelectedItems"/> stays in sync.
    /// </summary>
    public bool ShowSelectionColumn
    {
        get => (bool)GetValue(ShowSelectionColumnProperty);
        set => SetValue(ShowSelectionColumnProperty, value);
    }

    // ── SelectOnRowClick ──

    /// <summary>Identifies the <see cref="SelectOnRowClick"/> bindable property.</summary>
    public static readonly BindableProperty SelectOnRowClickProperty = BindableProperty.Create(
        nameof(SelectOnRowClick), typeof(bool), typeof(KumikoDataGrid), true,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._selection.SelectOnRowClick = (bool)n);

    /// <summary>
    /// Whether tapping a row selects it. Default <c>true</c>. Set <c>false</c> for checkbox-only selection: a tap on
    /// a row, outside a selection column's check box, then only moves the current cell (see
    /// <see cref="KumikoSelectionModel.SelectOnRowClick"/>). Keyboard selection is not affected.
    /// </summary>
    public bool SelectOnRowClick
    {
        get => (bool)GetValue(SelectOnRowClickProperty);
        set => SetValue(SelectOnRowClickProperty, value);
    }

    /// <summary>
    /// Adds or removes the grid's own selection column to match <see cref="ShowSelectionColumn"/>, and keeps it first.
    /// The caller pushes the columns to the data source afterwards when the change did not come from the collection.
    /// </summary>
    private void UpdateSelectionColumn()
    {
        if (GetValue(ColumnsProperty) is not ObservableCollection<KumikoColumn> columns) return;

        bool declared = false;
        foreach (var column in columns)
        {
            if (column.ColumnType == KumikoColumnType.Selection && !ReferenceEquals(column, _ownedSelectionColumn))
                declared = true;
        }

        _syncingSelectionColumn = true;
        try
        {
            if (ShowSelectionColumn && !declared)
            {
                _ownedSelectionColumn ??= new KumikoSelectionColumn();
                int index = columns.IndexOf(_ownedSelectionColumn);
                if (index < 0) columns.Insert(0, _ownedSelectionColumn);
                else if (index > 0) columns.Move(index, 0);
            }
            else if (_ownedSelectionColumn != null)
            {
                columns.Remove(_ownedSelectionColumn);
            }
        }
        finally
        {
            _syncingSelectionColumn = false;
        }

        _dataSource.SetColumns(columns);
        SyncActionButtonDefinitions();
        InvalidateSurface();
    }

    // ── Accessibility ──

    private void OnCheckAllToggled(object? sender, bool allSelected) =>
        Announce(KumikoAnnouncements.CheckAllToggled(_dataSource.Strings, allSelected, _dataSource.DataRowCount,
            _dataSource.Culture));
}
