namespace KumikoUI.Core.Models;

/// <summary>The texts of the selection column (#321): the names of its check boxes and their announcements.</summary>
public sealed partial record KumikoStrings
{
    /// <summary>The accessible name of the check-all box in a selection column's header. Default: "Select all rows".</summary>
    public string SelectAllRows { get; init; } = "Select all rows";

    /// <summary>The accessible name of a row's box in a selection column. Default: "Select row".</summary>
    public string SelectRow { get; init; } = "Select row";

    /// <summary>The announcement when the check-all box selected every row. <c>{0}</c> is the number of rows. Default: "All {0} rows selected".</summary>
    public string AllRowsSelected { get; init; } = "All {0} rows selected";

    /// <summary>The announcement when the check-all box cleared the selection. Default: "Selection cleared".</summary>
    public string SelectionCleared { get; init; } = "Selection cleared";

    /// <summary>The state of a checked selection column box, said after its name. Default: "checked".</summary>
    public string CheckStateChecked { get; init; } = "checked";

    /// <summary>The state of an unchecked selection column box, said after its name. Default: "not checked".</summary>
    public string CheckStateUnchecked { get; init; } = "not checked";

    /// <summary>The state of the check-all box when some, not all, rows are selected, said after its name. Default: "partially checked".</summary>
    public string CheckStateMixed { get; init; } = "partially checked";
}
