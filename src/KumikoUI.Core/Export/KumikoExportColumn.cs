using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Export;

/// <summary>
/// A column of an export, as it was when the snapshot was taken: what a writer needs for the header, the number
/// format, the width and the alignment, without reading the live <see cref="KumikoColumn"/>.
/// </summary>
public sealed class KumikoExportColumn
{
    internal KumikoExportColumn(KumikoColumn? column, string header, int groupLevel)
    {
        Column = column;
        Header = header;
        GroupLevel = groupLevel;
        if (column != null)
        {
            IsFrozen = column.IsFrozen;
            PropertyName = column.PropertyName ?? string.Empty;
            Format = column.Format;
            Width = column.Width;
            TextAlignment = column.TextAlignment;
            ColumnType = column.ColumnType;
            Accessor = column.ValueAccessor;
        }
        else
        {
            PropertyName = string.Empty;
            Width = KumikoColumn.DefaultWidth;
            TextAlignment = KumikoTextAlignment.Left;
            ColumnType = KumikoColumnType.Text;
        }
    }

    /// <summary>The grid column this export column reads, or <c>null</c> for a group key column.</summary>
    public KumikoColumn? Column { get; }

    /// <summary>The header text.</summary>
    public string Header { get; }

    /// <summary>The column's property name (case as given), or empty.</summary>
    public string PropertyName { get; }

    /// <summary>The column's .NET format string (<see cref="KumikoColumn.Format"/>), or <c>null</c>.</summary>
    public string? Format { get; }

    /// <summary>The column's width in the grid, in device-independent units.</summary>
    public float Width { get; }

    /// <summary>The column's text alignment.</summary>
    public KumikoTextAlignment TextAlignment { get; }

    /// <summary>The column's type.</summary>
    public KumikoColumnType ColumnType { get; }

    /// <summary>Whether this is a group key column (<see cref="KumikoExportGroupLayout.Columns"/>) rather than a grid column.</summary>
    public bool IsGroupKey => GroupLevel >= 0;

    /// <summary>The grouping level of a group key column (0 is the outermost), otherwise -1.</summary>
    public int GroupLevel { get; }

    /// <summary>Whether the column is frozen at the left edge in the grid.</summary>
    public bool IsFrozen { get; }

    internal Func<object, object?>? Accessor { get; }
}
