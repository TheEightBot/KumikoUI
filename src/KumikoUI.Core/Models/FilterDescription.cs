namespace KumikoUI.Core.Models;

/// <summary>
/// Complete filter description for a column.
/// Supports both a value list (Excel-style checkboxes) and
/// condition-based filtering (operator + value).
/// </summary>
public class FilterDescription
{
    /// <summary>The column this filter applies to.</summary>
    public KumikoColumn Column { get; }

    /// <summary>
    /// When non-null, items are filtered to only show rows whose value
    /// (as display text) is in this set. This is the Excel-style checkbox filter.
    /// Null means "no value-list filter" (show all).
    /// </summary>
    public HashSet<string>? SelectedValues { get; set; }

    /// <summary>
    /// Advanced condition-based filter. Null means no condition filter.
    /// </summary>
    public FilterCondition? Condition1 { get; set; }

    /// <summary>
    /// Second condition for compound filters. Null means single condition.
    /// </summary>
    public FilterCondition? Condition2 { get; set; }

    /// <summary>
    /// How to combine Condition1 and Condition2.
    /// </summary>
    public FilterCombination Combination { get; set; } = FilterCombination.And;

    /// <summary>Whether this filter has any active criteria.</summary>
    public bool IsActive =>
        SelectedValues != null || Condition1 != null;

    /// <summary>Creates an empty filter (no criteria) for a column.</summary>
    /// <param name="column">Column the filter applies to.</param>
    public FilterDescription(KumikoColumn column)
    {
        Column = column;
    }

    /// <summary>
    /// Evaluate this filter against a cell value and its display text, with the current culture and
    /// case-insensitive text rules (<see cref="KumikoComparer.Default"/>).
    /// </summary>
    public bool Evaluate(object? cellValue, string displayText) =>
        Evaluate(cellValue, displayText, SelectedValues is { } selected
            ? new HashSet<string>(selected, KumikoComparer.Default.TextComparer)
            : null, KumikoComparer.Default);

    /// <summary>
    /// A copy whose conditions are copies too, for a view build on a worker (#254): later changes to this
    /// filter's conditions do not reach a build that is running.
    /// </summary>
    internal FilterDescription CloneForBuild() => new(Column)
    {
        SelectedValues = SelectedValues,
        Condition1 = Condition1?.Clone(),
        Condition2 = Condition2?.Clone(),
        Combination = Combination,
    };

    /// <summary>
    /// Evaluate with a source's text rules: <paramref name="values"/> is <see cref="SelectedValues"/> as a set
    /// using <paramref name="comparer"/>'s text equality.
    /// </summary>
    internal bool Evaluate(object? cellValue, string displayText, ISet<string>? values, KumikoComparer comparer)
    {
        // Value list filter (Excel-style checkboxes)
        if (values != null && !values.Contains(displayText))
            return false;

        // Condition filter
        if (Condition1 != null)
        {
            bool result1 = Condition1.Evaluate(cellValue, Column.ColumnType, comparer);

            if (Condition2 != null)
            {
                bool result2 = Condition2.Evaluate(cellValue, Column.ColumnType, comparer);
                bool combined = Combination == FilterCombination.And
                    ? result1 && result2
                    : result1 || result2;
                if (!combined) return false;
            }
            else
            {
                if (!result1) return false;
            }
        }

        return true;
    }
}
