using KumikoUI.Core.Models;

namespace KumikoUI.Core.Export;

/// <summary>
/// Which columns an export (or a copy) writes and in what order, so the clipboard and the exporters agree on the
/// frozen-pane ordering.
/// </summary>
internal static class KumikoExportColumns
{
    /// <summary>
    /// <paramref name="columns"/> in the order the grid shows them: the left-frozen pane, the scrolling columns, then
    /// the right-frozen pane; list order inside each. Visibility is not looked at.
    /// </summary>
    public static List<KumikoColumn> InPaneOrder(IEnumerable<KumikoColumn> columns)
    {
        var all = columns as IList<KumikoColumn> ?? columns.ToList();
        return all.Where(c => c.IsFrozen)
            .Concat(all.Where(c => !c.IsFrozen && !c.IsFrozenRight))
            .Concat(all.Where(c => c.IsFrozenRight))
            .ToList();
    }

    /// <summary>Whether the column has a text representation, so it is exported unless the caller lists it explicitly.</summary>
    public static bool IsExportable(KumikoColumn column)
    {
        if (column.ColumnType is KumikoColumnType.Image or KumikoColumnType.Template or KumikoColumnType.Selection) return false;
        // A column drawn by a custom renderer with nothing to read has no value to write.
        if (column.CustomCellRenderer != null && column.ValueAccessor == null && string.IsNullOrEmpty(column.PropertyName))
            return false;
        return true;
    }

    /// <summary>The columns <paramref name="options"/> select from <paramref name="source"/>, in output order.</summary>
    public static List<KumikoColumn> Resolve(KumikoSource source, KumikoExportOptions options)
    {
        if (options.Columns != null) return options.Columns.Where(c => c != null).ToList();

        var chosen = InPaneOrder(source.Columns.Where(c => (options.IncludeHiddenColumns || c.IsVisible) && IsExportable(c)));
        return options.ColumnFilter == null ? chosen : chosen.Where(options.ColumnFilter).ToList();
    }
}
