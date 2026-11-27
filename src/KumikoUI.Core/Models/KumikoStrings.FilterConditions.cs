namespace KumikoUI.Core.Models;

/// <summary>The texts of the filter popup's condition filters (#325): the mode switch, And/Or and every operator.</summary>
public sealed partial record KumikoStrings
{
    /// <summary>The filter popup's tab with the value checklist. Default: "Values".</summary>
    public string FilterValuesTab { get; init; } = "Values";

    /// <summary>The filter popup's tab with the condition filters. Default: "Conditions".</summary>
    public string FilterConditionsTab { get; init; } = "Conditions";

    /// <summary>The filter popup's choice that keeps rows matching both conditions. Default: "And".</summary>
    public string FilterAnd { get; init; } = "And";

    /// <summary>The filter popup's choice that keeps rows matching either condition. Default: "Or".</summary>
    public string FilterOr { get; init; } = "Or";

    /// <summary>The placeholder of a condition's value box. Default: "Value".</summary>
    public string FilterValuePlaceholder { get; init; } = "Value";

    /// <summary>The <see cref="TextFilterOperator.Equals"/> operator. Default: "Equals".</summary>
    public string FilterTextEquals { get; init; } = "Equals";

    /// <summary>The <see cref="TextFilterOperator.NotEquals"/> operator. Default: "Does not equal".</summary>
    public string FilterTextNotEquals { get; init; } = "Does not equal";

    /// <summary>The <see cref="TextFilterOperator.Contains"/> operator. Default: "Contains".</summary>
    public string FilterTextContains { get; init; } = "Contains";

    /// <summary>The <see cref="TextFilterOperator.NotContains"/> operator. Default: "Does not contain".</summary>
    public string FilterTextNotContains { get; init; } = "Does not contain";

    /// <summary>The <see cref="TextFilterOperator.StartsWith"/> operator. Default: "Begins with".</summary>
    public string FilterTextStartsWith { get; init; } = "Begins with";

    /// <summary>The <see cref="TextFilterOperator.EndsWith"/> operator. Default: "Ends with".</summary>
    public string FilterTextEndsWith { get; init; } = "Ends with";

    /// <summary>The <see cref="TextFilterOperator.Empty"/> operator. Default: "Is empty".</summary>
    public string FilterTextEmpty { get; init; } = "Is empty";

    /// <summary>The <see cref="TextFilterOperator.NotEmpty"/> operator. Default: "Is not empty".</summary>
    public string FilterTextNotEmpty { get; init; } = "Is not empty";

    /// <summary>The <see cref="NumericFilterOperator.Equals"/> operator. Default: "Equals".</summary>
    public string FilterNumberEquals { get; init; } = "Equals";

    /// <summary>The <see cref="NumericFilterOperator.NotEquals"/> operator. Default: "Does not equal".</summary>
    public string FilterNumberNotEquals { get; init; } = "Does not equal";

    /// <summary>The <see cref="NumericFilterOperator.LessThan"/> operator. Default: "Less than".</summary>
    public string FilterNumberLessThan { get; init; } = "Less than";

    /// <summary>The <see cref="NumericFilterOperator.LessThanOrEqual"/> operator. Default: "Less than or equal to".</summary>
    public string FilterNumberLessThanOrEqual { get; init; } = "Less than or equal to";

    /// <summary>The <see cref="NumericFilterOperator.GreaterThan"/> operator. Default: "Greater than".</summary>
    public string FilterNumberGreaterThan { get; init; } = "Greater than";

    /// <summary>The <see cref="NumericFilterOperator.GreaterThanOrEqual"/> operator. Default: "Greater than or equal to".</summary>
    public string FilterNumberGreaterThanOrEqual { get; init; } = "Greater than or equal to";

    /// <summary>The <see cref="DateFilterOperator.Equals"/> operator. Default: "On".</summary>
    public string FilterDateEquals { get; init; } = "On";

    /// <summary>The <see cref="DateFilterOperator.NotEquals"/> operator. Default: "Not on".</summary>
    public string FilterDateNotEquals { get; init; } = "Not on";

    /// <summary>The <see cref="DateFilterOperator.Before"/> operator. Default: "Before".</summary>
    public string FilterDateBefore { get; init; } = "Before";

    /// <summary>The <see cref="DateFilterOperator.After"/> operator. Default: "After".</summary>
    public string FilterDateAfter { get; init; } = "After";

    /// <summary>The <see cref="DateFilterOperator.BeforeOrEqual"/> operator. Default: "On or before".</summary>
    public string FilterDateBeforeOrEqual { get; init; } = "On or before";

    /// <summary>The <see cref="DateFilterOperator.AfterOrEqual"/> operator. Default: "On or after".</summary>
    public string FilterDateAfterOrEqual { get; init; } = "On or after";

    /// <summary>The label of a filter operator (a <see cref="TextFilterOperator"/>, <see cref="NumericFilterOperator"/> or <see cref="DateFilterOperator"/>).</summary>
    /// <param name="op">The boxed operator.</param>
    internal string FilterOperatorLabel(object? op) => op switch
    {
        TextFilterOperator text => TextOperatorLabel(text),
        NumericFilterOperator number => NumericOperatorLabel(number),
        DateFilterOperator date => DateOperatorLabel(date),
        _ => op?.ToString() ?? string.Empty,
    };

    private string TextOperatorLabel(TextFilterOperator op) => op switch
    {
        TextFilterOperator.Equals => FilterTextEquals,
        TextFilterOperator.NotEquals => FilterTextNotEquals,
        TextFilterOperator.Contains => FilterTextContains,
        TextFilterOperator.NotContains => FilterTextNotContains,
        TextFilterOperator.StartsWith => FilterTextStartsWith,
        TextFilterOperator.EndsWith => FilterTextEndsWith,
        TextFilterOperator.Empty => FilterTextEmpty,
        TextFilterOperator.NotEmpty => FilterTextNotEmpty,
        _ => op.ToString(),
    };

    private string NumericOperatorLabel(NumericFilterOperator op) => op switch
    {
        NumericFilterOperator.Equals => FilterNumberEquals,
        NumericFilterOperator.NotEquals => FilterNumberNotEquals,
        NumericFilterOperator.LessThan => FilterNumberLessThan,
        NumericFilterOperator.LessThanOrEqual => FilterNumberLessThanOrEqual,
        NumericFilterOperator.GreaterThan => FilterNumberGreaterThan,
        NumericFilterOperator.GreaterThanOrEqual => FilterNumberGreaterThanOrEqual,
        _ => op.ToString(),
    };

    private string DateOperatorLabel(DateFilterOperator op) => op switch
    {
        DateFilterOperator.Equals => FilterDateEquals,
        DateFilterOperator.NotEquals => FilterDateNotEquals,
        DateFilterOperator.Before => FilterDateBefore,
        DateFilterOperator.After => FilterDateAfter,
        DateFilterOperator.BeforeOrEqual => FilterDateBeforeOrEqual,
        DateFilterOperator.AfterOrEqual => FilterDateAfterOrEqual,
        _ => op.ToString(),
    };
}
