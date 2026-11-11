namespace KumikoUI.Core.Models;

/// <summary>
/// Every piece of text the grid draws or reports on its own: the group panel label, group and summary
/// labels, the filter popup, and the validation messages for edits that do not convert. The defaults are
/// English; localize by replacing the table with <c>with</c>:
/// <code>
/// source.Strings = KumikoStrings.Default with { GroupPanelLabel = "Gruppiert nach:", FilterOk = "OK" };
/// </code>
/// Day and month names and AM/PM in the date picker come from the grid's <see cref="KumikoSource.Culture"/>,
/// not from this table.
/// </summary>
/// <remarks>
/// A property holding a <c>{0}</c> placeholder is a format string; keep the placeholder in a translation.
/// </remarks>
public sealed record KumikoStrings
{
    /// <summary>The English strings. Start a translation from this instance with <c>with</c>.</summary>
    public static KumikoStrings Default { get; } = new();

    /// <summary>The label in front of the group chips in the group panel. Default: "Grouped by:".</summary>
    public string GroupPanelLabel { get; init; } = "Grouped by:";

    /// <summary>The header text of the group whose key is null. Default: "(Blank)".</summary>
    public string BlankGroupText { get; init; } = "(Blank)";

    /// <summary>The default label of a <see cref="SummaryType.Sum"/> summary. Default: "Sum: ".</summary>
    public string SummarySum { get; init; } = "Sum: ";

    /// <summary>The default label of a <see cref="SummaryType.Average"/> summary. Default: "Avg: ".</summary>
    public string SummaryAverage { get; init; } = "Avg: ";

    /// <summary>The default label of a <see cref="SummaryType.Count"/> summary. Default: "Count: ".</summary>
    public string SummaryCount { get; init; } = "Count: ";

    /// <summary>The default label of a <see cref="SummaryType.Min"/> summary. Default: "Min: ".</summary>
    public string SummaryMin { get; init; } = "Min: ";

    /// <summary>The default label of a <see cref="SummaryType.Max"/> summary. Default: "Max: ".</summary>
    public string SummaryMax { get; init; } = "Max: ";

    /// <summary>The filter popup's entry for empty values. Default: "(Blanks)".</summary>
    public string FilterBlanks { get; init; } = "(Blanks)";

    /// <summary>The placeholder in the filter popup's search box. Default: "Search...".</summary>
    public string FilterSearchPlaceholder { get; init; } = "Search...";

    /// <summary>The filter popup's "select every value shown" row. Default: "Select All".</summary>
    public string FilterSelectAll { get; init; } = "Select All";

    /// <summary>The filter popup's "clear every value shown" row. Default: "Deselect All".</summary>
    public string FilterDeselectAll { get; init; } = "Deselect All";

    /// <summary>The filter popup's button that applies the filter. Default: "OK".</summary>
    public string FilterOk { get; init; } = "OK";

    /// <summary>The filter popup's button that closes it without applying. Default: "Cancel".</summary>
    public string FilterCancel { get; init; } = "Cancel";

    /// <summary>The filter popup's button that removes the column's filter. Default: "Clear".</summary>
    public string FilterClear { get; init; } = "Clear";

    /// <summary>The filter popup's ascending sort row for text columns. Default: "Sort A to Z".</summary>
    public string SortTextAscending { get; init; } = "Sort A to Z";

    /// <summary>The filter popup's descending sort row for text columns. Default: "Sort Z to A".</summary>
    public string SortTextDescending { get; init; } = "Sort Z to A";

    /// <summary>The filter popup's ascending sort row for <see cref="KumikoColumnType.Numeric"/> columns. Default: "Sort Smallest to Largest".</summary>
    public string SortNumericAscending { get; init; } = "Sort Smallest to Largest";

    /// <summary>The filter popup's descending sort row for <see cref="KumikoColumnType.Numeric"/> columns. Default: "Sort Largest to Smallest".</summary>
    public string SortNumericDescending { get; init; } = "Sort Largest to Smallest";

    /// <summary>The filter popup's ascending sort row for <see cref="KumikoColumnType.Date"/> columns. Default: "Sort Oldest to Newest".</summary>
    public string SortDateAscending { get; init; } = "Sort Oldest to Newest";

    /// <summary>The filter popup's descending sort row for <see cref="KumikoColumnType.Date"/> columns. Default: "Sort Newest to Oldest".</summary>
    public string SortDateDescending { get; init; } = "Sort Newest to Oldest";

    /// <summary>The filter popup's row that sorts <see cref="KumikoColumnType.Boolean"/> columns checked rows first. Default: "Sort Checked First".</summary>
    public string SortBooleanCheckedFirst { get; init; } = "Sort Checked First";

    /// <summary>The filter popup's row that sorts <see cref="KumikoColumnType.Boolean"/> columns unchecked rows first. Default: "Sort Unchecked First".</summary>
    public string SortBooleanUncheckedFirst { get; init; } = "Sort Unchecked First";

    /// <summary>The message for an empty value in a column that cannot be empty. Default: "A value is required.".</summary>
    public string ValueRequired { get; init; } = "A value is required.";

    /// <summary>The message for text that is not a number. Default: "Enter a number.".</summary>
    public string EnterNumber { get; init; } = "Enter a number.";

    /// <summary>The message for a number with a fraction where a whole number is required. Default: "Enter a whole number.".</summary>
    public string EnterWholeNumber { get; init; } = "Enter a whole number.";

    /// <summary>The message for a number outside the range of the property type. Default: "The number is out of range.".</summary>
    public string NumberOutOfRange { get; init; } = "The number is out of range.";

    /// <summary>The message for text that is not a boolean. Default: "Enter true or false.".</summary>
    public string EnterBoolean { get; init; } = "Enter true or false.";

    /// <summary>The message for text that is not a single character. Default: "Enter a single character.".</summary>
    public string EnterCharacter { get; init; } = "Enter a single character.";

    /// <summary>The message for text that is not a date. Default: "Enter a valid date.".</summary>
    public string EnterDate { get; init; } = "Enter a valid date.";

    /// <summary>The message for text that is not a time of day. Default: "Enter a valid time.".</summary>
    public string EnterTime { get; init; } = "Enter a valid time.";

    /// <summary>The message for text that is not a duration. Default: "Enter a valid duration.".</summary>
    public string EnterDuration { get; init; } = "Enter a valid duration.";

    /// <summary>The message for text that is not a GUID. Default: "Enter a valid identifier.".</summary>
    public string EnterIdentifier { get; init; } = "Enter a valid identifier.";

    /// <summary>The message for an enum value that is not defined. <c>{0}</c> is the comma-separated list of the names. Default: "Enter one of: {0}.".</summary>
    public string EnterOneOf { get; init; } = "Enter one of: {0}.";

    /// <summary>The message for a value that cannot be converted to the property type. Default: "Enter a valid value.".</summary>
    public string EnterValidValue { get; init; } = "Enter a valid value.";

    /// <summary>The message when writing an edited value fails without a reason. Default: "The value could not be saved.".</summary>
    public string ValueCouldNotBeSaved { get; init; } = "The value could not be saved.";

    /// <summary>The grid's accessible name for screen readers, unless the host sets its own. Default: "Data grid".</summary>
    public string AccessibleGridName { get; init; } = "Data grid";

    /// <summary>The accessible hint for a grid without a current cell. <c>{0}</c> is the row count and <c>{1}</c> the column count. Default: "{0} rows, {1} columns".</summary>
    public string AccessibleGridHint { get; init; } = "{0} rows, {1} columns";

    /// <summary>The accessible hint for a grid with a current cell. <c>{0}</c> and <c>{1}</c> are the row and column counts, <c>{2}</c> and <c>{3}</c> the current cell's one-based row and column. Default: "{0} rows, {1} columns, current cell row {2} column {3}".</summary>
    public string AccessibleGridHintWithCell { get; init; } = "{0} rows, {1} columns, current cell row {2} column {3}";

    /// <summary>The announcement when the current cell moves. <c>{0}</c> is the column header, <c>{1}</c> the cell text, <c>{2}</c> and <c>{3}</c> the one-based row and column. Default: "{0}: {1}, row {2}, column {3}".</summary>
    public string AnnounceCurrentCell { get; init; } = "{0}: {1}, row {2}, column {3}";

    /// <summary>The text announced for a cell with no text. Default: "empty".</summary>
    public string AnnounceEmptyValue { get; init; } = "empty";

    /// <summary>The announcement for a column's header when it has no text. <c>{0}</c> is the one-based column number. Default: "Column {0}".</summary>
    public string AnnounceUnnamedColumn { get; init; } = "Column {0}";

    /// <summary>The announcement after a column was sorted ascending. <c>{0}</c> is the column header. Default: "Sorted by {0}, ascending".</summary>
    public string AnnounceSortedAscending { get; init; } = "Sorted by {0}, ascending";

    /// <summary>The announcement after a column was sorted descending. <c>{0}</c> is the column header. Default: "Sorted by {0}, descending".</summary>
    public string AnnounceSortedDescending { get; init; } = "Sorted by {0}, descending";

    /// <summary>The announcement after a column's sort was removed. <c>{0}</c> is the column header. Default: "Sort removed from {0}".</summary>
    public string AnnounceSortRemoved { get; init; } = "Sort removed from {0}";

    /// <summary>The announcement when editing starts. <c>{0}</c> is the column header and <c>{1}</c> the cell text. Default: "Editing {0}: {1}".</summary>
    public string AnnounceEditStarted { get; init; } = "Editing {0}: {1}";

    /// <summary>The announcement when an edit is committed. <c>{0}</c> is the column header and <c>{1}</c> the new text. Default: "{0} changed to {1}".</summary>
    public string AnnounceEditCommitted { get; init; } = "{0} changed to {1}";

    /// <summary>The announcement when an edit ends without a change. <c>{0}</c> is the column header. Default: "Editing {0} ended".</summary>
    public string AnnounceEditCanceled { get; init; } = "Editing {0} ended";

    /// <summary>The announcement when an edit fails validation. <c>{0}</c> is the column header and <c>{1}</c> the message. Default: "{0} is invalid: {1}".</summary>
    public string AnnounceValidationFailed { get; init; } = "{0} is invalid: {1}";

    /// <summary>The default summary label for <paramref name="type"/>, or an empty string.</summary>
    /// <param name="type">The kind of summary.</param>
    internal string SummaryLabel(SummaryType type) => type switch
    {
        SummaryType.Sum => SummarySum,
        SummaryType.Average => SummaryAverage,
        SummaryType.Count => SummaryCount,
        SummaryType.Min => SummaryMin,
        SummaryType.Max => SummaryMax,
        _ => ""
    };

    /// <summary>The text of the filter popup row that sorts a column of <paramref name="type"/> in <paramref name="direction"/>.</summary>
    /// <param name="type">The column type.</param>
    /// <param name="direction">The sort direction, in comparer terms (a checked Boolean sorts after an unchecked one, so checked first is descending).</param>
    internal string SortLabel(KumikoColumnType type, KumikoSortDirection direction) => direction == KumikoSortDirection.Descending
        ? type switch
        {
            KumikoColumnType.Numeric or KumikoColumnType.ProgressBar => SortNumericDescending,
            KumikoColumnType.Date => SortDateDescending,
            KumikoColumnType.Boolean => SortBooleanCheckedFirst,
            _ => SortTextDescending
        }
        : type switch
        {
            KumikoColumnType.Numeric or KumikoColumnType.ProgressBar => SortNumericAscending,
            KumikoColumnType.Date => SortDateAscending,
            KumikoColumnType.Boolean => SortBooleanUncheckedFirst,
            _ => SortTextAscending
        };
}
