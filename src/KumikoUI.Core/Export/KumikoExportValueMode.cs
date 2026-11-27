namespace KumikoUI.Core.Export;

/// <summary>How an export writes cell values.</summary>
public enum KumikoExportValueMode
{
    /// <summary>The format decides: display text for CSV and PDF, typed values for Excel.</summary>
    Auto,

    /// <summary>The text the grid shows (the column's format applied in the export culture).</summary>
    DisplayText,

    /// <summary>The value itself: a typed cell where the format has them, and invariant, round-trippable text (numbers, ISO 8601 dates, <c>true</c> and <c>false</c>) where it does not.</summary>
    RawValue,
}
