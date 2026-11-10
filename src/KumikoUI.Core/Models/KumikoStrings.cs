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

    /// <summary>The text of the ascending sort row for a column of <paramref name="type"/>.</summary>
    /// <param name="type">The column type.</param>
    internal string SortAscending(KumikoColumnType type) => type switch
    {
        KumikoColumnType.Numeric => SortNumericAscending,
        KumikoColumnType.Date => SortDateAscending,
        _ => SortTextAscending
    };

    /// <summary>The text of the descending sort row for a column of <paramref name="type"/>.</summary>
    /// <param name="type">The column type.</param>
    internal string SortDescending(KumikoColumnType type) => type switch
    {
        KumikoColumnType.Numeric => SortNumericDescending,
        KumikoColumnType.Date => SortDateDescending,
        _ => SortTextDescending
    };
}
