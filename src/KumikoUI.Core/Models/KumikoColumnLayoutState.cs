namespace KumikoUI.Core.Models;

/// <summary>One column of a <see cref="KumikoLayoutState"/>.</summary>
public sealed class KumikoColumnLayoutState
{
    /// <summary>The column's name: <see cref="KumikoColumn.Key"/>, else <see cref="KumikoColumn.PropertyName"/>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The column's <see cref="KumikoColumn.IsVisible"/>. Default: true.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>The column's <see cref="KumikoColumn.FreezeMode"/>: the pane it is drawn in.</summary>
    public ColumnFreezeMode FreezeMode { get; set; }

    /// <summary>The column's <see cref="KumikoColumn.SizeMode"/>.</summary>
    public ColumnSizeMode SizeMode { get; set; }

    /// <summary>
    /// The width of a <see cref="ColumnSizeMode.Fixed"/> column whose width was set (in code, in XAML, by a resize
    /// drag or by auto-fit), or null to leave the width alone. Star and Auto widths are computed by layout.
    /// </summary>
    public float? Width { get; set; }

    /// <summary>The <see cref="KumikoColumn.StarWeight"/> of a <see cref="ColumnSizeMode.Star"/> column, or null.</summary>
    public float? StarWeight { get; set; }

    /// <summary>The column's sort direction; <see cref="KumikoSortDirection.None"/> when it is not sorted.</summary>
    public KumikoSortDirection SortDirection { get; set; }

    /// <summary>The column's place in the sort (1 = primary), or 0 when it is not sorted.</summary>
    public int SortOrder { get; set; }

    /// <summary>The column's filter (<see cref="KumikoColumn.ActiveFilter"/>), or null for none.</summary>
    public KumikoFilterLayoutState? Filter { get; set; }
}
