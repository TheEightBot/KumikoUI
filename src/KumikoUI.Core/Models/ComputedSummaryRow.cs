namespace KumikoUI.Core.Models;

/// <summary>
/// Holds the computed summary values for a single summary row.
/// </summary>
public class ComputedSummaryRow
{
    /// <summary>The summary row definition this was computed from.</summary>
    public string Name { get; init; } = "";

    /// <summary>Optional title for the row.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// Computed values keyed by column property name.
    /// Value is the formatted display string for each column.
    /// </summary>
    public Dictionary<string, string> Values { get; init; } = new();

    /// <summary>
    /// Raw computed values keyed by column property name (before formatting).
    /// </summary>
    public Dictionary<string, object?> RawValues { get; init; } = new();

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
