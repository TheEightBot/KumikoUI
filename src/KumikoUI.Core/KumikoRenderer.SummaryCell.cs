using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

public partial class KumikoRenderer
{
    /// <summary>
    /// Draws every summary value cell that has no <see cref="SummaryColumnDescription.CustomCellRenderer"/> (#334).
    /// Null (the default) draws the text with <see cref="TextSummaryCellRenderer"/>. A cell drawn by a custom renderer
    /// is never moved to the text after the row's title: it is drawn in its column, clipped, at any width.
    /// </summary>
    public ISummaryCellRenderer? SummaryCellRenderer { get; set; }

    /// <summary>
    /// The renderer of a summary row's cell for <paramref name="propertyName"/>: the first aggregate with a custom
    /// renderer, then <see cref="SummaryCellRenderer"/>, else null (the built-in text). Allocates nothing.
    /// </summary>
    private ISummaryCellRenderer? ResolveSummaryRenderer(ComputedSummaryRow summary, string propertyName)
    {
        var aggregates = summary.Aggregates;
        for (int i = 0; i < aggregates.Count; i++)
        {
            var a = aggregates[i];
            if (a.Description?.CustomCellRenderer is { } custom &&
                string.Equals(a.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
                return custom;
        }
        return SummaryCellRenderer;
    }
}
