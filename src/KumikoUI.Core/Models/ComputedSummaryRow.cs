namespace KumikoUI.Core.Models;

/// <summary>
/// Holds the computed summary values for a single summary row.
/// </summary>
public sealed class ComputedSummaryRow
{
    /// <summary>The summary row definition this was computed from.</summary>
    public string Name { get; init; } = "";

    /// <summary>Optional title for the row.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// Computed values keyed by column property name (case-insensitive). The value is the formatted display
    /// string for the column; when several aggregates share a property they are joined in declaration order.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Raw computed values keyed by column property name (case-insensitive, before formatting). When several
    /// aggregates share a property this holds the first; use <see cref="Aggregates"/> for all of them.
    /// </summary>
    public IReadOnlyDictionary<string, object?> RawValues { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every computed aggregate in declaration order, keyed by property and summary type or label
    /// (several can share one property).
    /// </summary>
    public IReadOnlyList<ComputedSummaryValue> Aggregates { get; init; } = Array.Empty<ComputedSummaryValue>();

    /// <summary>
    /// The raw result of the first aggregate of <paramref name="propertyName"/> (case-insensitive) with
    /// <paramref name="summaryType"/>, or null when there is none.
    /// </summary>
    public object? GetRawValue(string propertyName, SummaryType summaryType)
    {
        foreach (var a in Aggregates)
        {
            if (a.SummaryType == summaryType &&
                string.Equals(a.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
                return a.Value;
        }
        return null;
    }

    // The values last drawn after the title (they did not fit their columns), and their joined text.
    private string[]? _overflowValues;
    private string? _overflowText;

    /// <summary>
    /// The text drawn after the row's title for values that do not fit their columns: each value preceded by
    /// "  |  ". Joined when the values differ from the last call, then reused, so redrawing the row allocates
    /// nothing.
    /// </summary>
    internal string GetOverflowText(List<string> values)
    {
        if (_overflowText != null && _overflowValues!.Length == values.Count)
        {
            bool same = true;
            for (int i = 0; i < values.Count && same; i++)
                same = ReferenceEquals(_overflowValues[i], values[i]);
            if (same) return _overflowText;
        }

        _overflowValues = values.ToArray();
        _overflowText = "  |  " + string.Join("  |  ", _overflowValues);
        return _overflowText;
    }
}
