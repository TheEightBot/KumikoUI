namespace KumikoUI.Core.Models;

/// <summary>
/// One computed aggregate of a summary row: a single <see cref="SummaryColumnDescription"/> evaluated.
/// Several of these can share one property (for example a Sum and an Average of the same column).
/// </summary>
public sealed class ComputedSummaryValue
{
    /// <summary>The property that was aggregated.</summary>
    public string PropertyName { get; init; } = "";

    /// <summary>The aggregate type.</summary>
    public SummaryType SummaryType { get; init; }

    /// <summary>The label shown before the value.</summary>
    public string Label { get; init; } = "";

    /// <summary>The raw result before formatting: <c>decimal</c> for exact sums and averages of decimals.</summary>
    public object? Value { get; init; }

    /// <summary>The label and the formatted value, as shown.</summary>
    public string Text { get; init; } = "";
}
