namespace KumikoUI.Core.Components;

/// <summary>
/// The <see cref="DrawnMenuItem.Key"/> values of the items the grid puts in a column menu, so a
/// <c>ColumnMenuOpening</c> handler can find, remove or move them whatever language their text is in.
/// </summary>
/// <remarks>
/// The default menu holds, in this order and as far as the column and the grid allow: sort ascending, sort
/// descending and clear sort; filter; group by this column or ungroup; hide, auto-fit width and columns (the column
/// chooser). Separators sit between the groups and have an empty key.
/// </remarks>
public static class KumikoColumnMenuItems
{
    /// <summary>Sorts the rows by the column, ascending, as the only sorted column.</summary>
    public const string SortAscending = "SortAscending";

    /// <summary>Sorts the rows by the column, descending, as the only sorted column.</summary>
    public const string SortDescending = "SortDescending";

    /// <summary>Removes the column from the sort; the other sorted columns keep theirs.</summary>
    public const string ClearSort = "ClearSort";

    /// <summary>Opens the column's filter popup.</summary>
    public const string Filter = "Filter";

    /// <summary>Groups the rows by the column, as the last group level.</summary>
    public const string GroupBy = "GroupBy";

    /// <summary>Stops grouping by the column.</summary>
    public const string Ungroup = "Ungroup";

    /// <summary>Hides the column (sets <see cref="Models.KumikoColumn.IsVisible"/> to false).</summary>
    public const string Hide = "Hide";

    /// <summary>Sizes the column to fit its header and its cells, as a double click on its resize grip does.</summary>
    public const string AutoFit = "AutoFit";

    /// <summary>Opens the column chooser below the column's header, to show, hide and reorder every column.</summary>
    public const string Columns = "Columns";
}
