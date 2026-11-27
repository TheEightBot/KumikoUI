using KumikoUI.Core.Models;

namespace KumikoUI.Core.Export;

/// <summary>
/// The rows an export of <see cref="KumikoExportRows.Selected"/> writes. Rows are matched to the grid's view by
/// item reference (or, for <see cref="FromRows"/>, by visible row when the export starts), written in view order,
/// and duplicates and group rows are ignored.
/// </summary>
public sealed class KumikoExportSelection
{
    private readonly IReadOnlyCollection<object>? _items;
    private readonly IReadOnlyCollection<int>? _rows;
    private readonly KumikoSelectionModel? _model;

    private KumikoExportSelection(IReadOnlyCollection<object>? items, IReadOnlyCollection<int>? rows, KumikoSelectionModel? model)
    {
        _items = items;
        _rows = rows;
        _model = model;
    }

    /// <summary>Selects the given items, matched by reference.</summary>
    /// <param name="items">The items to export.</param>
    /// <returns>The selection.</returns>
    public static KumikoExportSelection FromItems(IEnumerable<object> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new KumikoExportSelection(items.ToArray(), null, null);
    }

    /// <summary>Selects the given visible rows, the same input as <see cref="Input.KumikoTsvFormatter.Format"/>.</summary>
    /// <param name="visibleRows">Visible row indices; group rows and rows out of range are skipped.</param>
    /// <returns>The selection.</returns>
    public static KumikoExportSelection FromRows(IEnumerable<int> visibleRows)
    {
        ArgumentNullException.ThrowIfNull(visibleRows);
        return new KumikoExportSelection(null, visibleRows.ToArray(), null);
    }

    /// <summary>Selects the items selected in <paramref name="selection"/> when the export starts.</summary>
    /// <param name="selection">The grid's selection model.</param>
    /// <returns>The selection.</returns>
    public static KumikoExportSelection FromSelection(KumikoSelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return new KumikoExportSelection(null, null, selection);
    }

    /// <summary>The selected items, by reference, for an export of <paramref name="source"/> that starts now.</summary>
    internal HashSet<object> Resolve(KumikoSource source)
    {
        var set = new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (_items != null)
        {
            foreach (var item in _items)
                if (item != null) set.Add(item);
        }
        else if (_rows != null)
        {
            foreach (int row in _rows)
            {
                if (row < 0 || row >= source.RowCount || source.IsNonDataRow(row)) continue;
                set.Add(source.GetItem(row));
            }
        }
        else if (_model != null)
        {
            foreach (var item in _model.SelectedItems) set.Add(item);
        }
        return set;
    }
}
