using KumikoUI.Core.Models;

namespace KumikoUI.Core.Export;

/// <summary>
/// One row of the export stream. The enumerator of <see cref="KumikoExportSnapshot.Rows"/> hands out the same
/// instance for every row and overwrites its cells, so read what you need before asking for the next row.
/// </summary>
public sealed class KumikoExportRow
{
    private KumikoExportCell[] _cells;
    private int _count;

    internal KumikoExportRow(int capacity)
    {
        _cells = new KumikoExportCell[capacity];
    }

    /// <summary>What kind of row this is.</summary>
    public KumikoExportRowKind Kind { get; internal set; }

    /// <summary>
    /// The outline depth: 0 for the header and table summary rows (and for every row unless the layout is
    /// <see cref="KumikoExportGroupLayout.Rows"/>); a group header's level (0 is the outermost group); and for the rows
    /// inside a group (data and group summary rows) one more than that group's level.
    /// </summary>
    public int Level { get; internal set; }

    /// <summary>The item of a <see cref="KumikoExportRowKind.Data"/> row; <c>null</c> for every other kind.</summary>
    public object? DataItem { get; internal set; }

    /// <summary>The number of cells: one per column of <see cref="KumikoExportSnapshot.Columns"/>.</summary>
    public int Count => _count;

    /// <summary>The cell of the column at <paramref name="index"/> of <see cref="KumikoExportSnapshot.Columns"/>.</summary>
    /// <param name="index">The column index.</param>
    public KumikoExportCell this[int index] => (uint)index < (uint)_count ? _cells[index] : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>The cells, one per column.</summary>
    public ReadOnlySpan<KumikoExportCell> Cells => new(_cells, 0, _count);

    internal void Begin(KumikoExportRowKind kind, int level, object? item, int count)
    {
        Kind = kind;
        Level = level;
        DataItem = item;
        if (_cells.Length < count) _cells = new KumikoExportCell[count];
        _count = count;
        Array.Clear(_cells, 0, count);
    }

    internal void Set(int index, object? value, string text, SummaryType? summaryType = null) =>
        _cells[index] = new KumikoExportCell(value, text, summaryType);
}
