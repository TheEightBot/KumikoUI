namespace KumikoUI.Core.Models;

/// <summary>A column filter in a <see cref="KumikoLayoutState"/> (a <see cref="FilterDescription"/>).</summary>
public sealed class KumikoFilterLayoutState
{
    /// <summary>The <see cref="FilterDescription.SelectedValues"/> (the checked values), or null for no value list.</summary>
    public List<string>? SelectedValues { get; set; }

    /// <summary>The <see cref="FilterDescription.Condition1"/>, or null.</summary>
    public KumikoFilterConditionLayoutState? Condition1 { get; set; }

    /// <summary>The <see cref="FilterDescription.Condition2"/>, or null.</summary>
    public KumikoFilterConditionLayoutState? Condition2 { get; set; }

    /// <summary>The <see cref="FilterDescription.Combination"/> of the two conditions. Default: And.</summary>
    public FilterCombination Combination { get; set; } = FilterCombination.And;

    /// <summary>The state of <paramref name="filter"/>, or null when there is none.</summary>
    internal static KumikoFilterLayoutState? From(FilterDescription? filter)
    {
        if (filter == null) return null;
        List<string>? values = null;
        if (filter.SelectedValues != null)
        {
            values = new List<string>(filter.SelectedValues);
            values.Sort(StringComparer.Ordinal); // a set has no order; sorting keeps the JSON stable
        }
        return new KumikoFilterLayoutState
        {
            SelectedValues = values,
            Condition1 = KumikoFilterConditionLayoutState.From(filter.Condition1),
            Condition2 = KumikoFilterConditionLayoutState.From(filter.Condition2),
            Combination = filter.Combination,
        };
    }

    /// <summary>A new filter for <paramref name="column"/> with this state.</summary>
    internal FilterDescription ToFilterDescription(KumikoColumn column) => new(column)
    {
        SelectedValues = SelectedValues != null ? new HashSet<string>(SelectedValues) : null,
        Condition1 = Condition1?.ToFilterCondition(),
        Condition2 = Condition2?.ToFilterCondition(),
        Combination = Combination,
    };
}
