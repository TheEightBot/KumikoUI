namespace KumikoUI.Core.Models;

/// <summary>The texts of the column chooser (#335, spec #106): what it draws, its accessible names and its announcements.</summary>
public sealed partial record KumikoStrings
{
    /// <summary>The column chooser's title and accessible name. Default: "Columns".</summary>
    public string ColumnChooserTitle { get; init; } = "Columns";

    /// <summary>The placeholder in the column chooser's search box. Default: "Search columns".</summary>
    public string ColumnChooserSearchPlaceholder { get; init; } = "Search columns";

    /// <summary>The column chooser's button that shows every listed column. Default: "Show all".</summary>
    public string ColumnChooserShowAll { get; init; } = "Show all";

    /// <summary>The column chooser's button that restores the default column layout. Default: "Reset".</summary>
    public string ColumnChooserReset { get; init; } = "Reset";

    /// <summary>The accessible name of the column chooser's close button. Default: "Close".</summary>
    public string ColumnChooserClose { get; init; } = "Close";

    /// <summary>What the column chooser shows when no column matches the search. Default: "No matching columns".</summary>
    public string ColumnChooserNoMatches { get; init; } = "No matching columns";

    /// <summary>
    /// Why a column's box in the chooser is disabled: it is the last visible column, or its
    /// <see cref="KumikoColumn.AllowHide"/> is false. Default: "This column cannot be hidden".
    /// </summary>
    public string ColumnChooserCannotHide { get; init; } = "This column cannot be hidden";

    /// <summary>The position of a column in the chooser's list. <c>{0}</c> is the position, <c>{1}</c> the count. Default: "{0} of {1}".</summary>
    public string ColumnChooserPosition { get; init; } = "{0} of {1}";

    /// <summary>The column menu item that opens the column chooser. Default: "Columns...".</summary>
    public string ColumnMenuColumns { get; init; } = "Columns...";

    /// <summary>The announcement when a column was shown. <c>{0}</c> is the column. Default: "{0} shown".</summary>
    public string AnnounceColumnShown { get; init; } = "{0} shown";

    /// <summary>The announcement when a column was hidden. <c>{0}</c> is the column. Default: "{0} hidden".</summary>
    public string AnnounceColumnHidden { get; init; } = "{0} hidden";

    /// <summary>
    /// The announcement when a column moved in the chooser. <c>{0}</c> is the column, <c>{1}</c> its new position and
    /// <c>{2}</c> the number of columns listed. Default: "{0} moved to position {1} of {2}".
    /// </summary>
    public string AnnounceColumnMoved { get; init; } = "{0} moved to position {1} of {2}";

    /// <summary>The announcement when the chooser's Show all button showed the columns. Default: "All columns shown".</summary>
    public string AnnounceAllColumnsShown { get; init; } = "All columns shown";

    /// <summary>The announcement when the chooser's Reset button restored the default layout. Default: "Column layout reset".</summary>
    public string AnnounceColumnLayoutReset { get; init; } = "Column layout reset";
}
