using KumikoUI.Core.Models;

namespace KumikoUI.Core.Components;

/// <summary>The condition editor a column type gets in the filter popup (#325).</summary>
internal enum FilterConditionKind
{
    /// <summary>No condition filters: the popup shows only the value checklist (Boolean, Image, Template).</summary>
    None,

    /// <summary>Text operators and a text box.</summary>
    Text,

    /// <summary>Numeric operators and a text box parsed in the source's culture.</summary>
    Number,

    /// <summary>Date operators and a date picker.</summary>
    Date,
}

/// <summary>
/// The filter popup's condition editors by column type: which operators a column offers, in which order, and
/// which value editor it gets. <see cref="KindOf(KumikoColumnType)"/> is the only place a column type is mapped,
/// so a new type joins an existing path (a date-like type takes <see cref="FilterConditionKind.Date"/>) with one line.
/// </summary>
internal static class FilterConditionTypes
{
    private static readonly object[] TextOperators =
    {
        TextFilterOperator.Contains, TextFilterOperator.NotContains, TextFilterOperator.Equals, TextFilterOperator.NotEquals,
        TextFilterOperator.StartsWith, TextFilterOperator.EndsWith, TextFilterOperator.Empty, TextFilterOperator.NotEmpty,
    };

    private static readonly object[] NumberOperators =
    {
        NumericFilterOperator.Equals, NumericFilterOperator.NotEquals, NumericFilterOperator.GreaterThan,
        NumericFilterOperator.GreaterThanOrEqual, NumericFilterOperator.LessThan, NumericFilterOperator.LessThanOrEqual,
    };

    private static readonly object[] DateOperators =
    {
        DateFilterOperator.Equals, DateFilterOperator.NotEquals, DateFilterOperator.Before, DateFilterOperator.After,
        DateFilterOperator.BeforeOrEqual, DateFilterOperator.AfterOrEqual,
    };

    /// <summary>The condition editor of a column type.</summary>
    /// <param name="type">The column type.</param>
    internal static FilterConditionKind KindOf(KumikoColumnType type) => type switch
    {
        KumikoColumnType.Text or KumikoColumnType.ComboBox or KumikoColumnType.Picker => FilterConditionKind.Text,
        KumikoColumnType.Numeric or KumikoColumnType.ProgressBar => FilterConditionKind.Number,
        KumikoColumnType.Date => FilterConditionKind.Date,
        _ => FilterConditionKind.None,
    };

    /// <summary>The condition editor of a column: none when it turns condition filters off.</summary>
    /// <param name="column">The column.</param>
    internal static FilterConditionKind KindOf(KumikoColumn column) =>
        column.AllowConditionFilter ? KindOf(column.ColumnType) : FilterConditionKind.None;

    /// <summary>The operators of <paramref name="kind"/>, boxed, in the order the drop-down lists them (the first is the default).</summary>
    /// <param name="kind">The condition editor.</param>
    internal static IReadOnlyList<object> Operators(FilterConditionKind kind) => kind switch
    {
        FilterConditionKind.Text => TextOperators,
        FilterConditionKind.Number => NumberOperators,
        FilterConditionKind.Date => DateOperators,
        _ => Array.Empty<object>(),
    };

    /// <summary>The operator of <paramref name="condition"/> that <paramref name="kind"/> evaluates.</summary>
    /// <param name="kind">The condition editor.</param>
    /// <param name="condition">The condition.</param>
    internal static object? OperatorOf(FilterConditionKind kind, FilterCondition condition) => kind switch
    {
        FilterConditionKind.Text => condition.TextOperator,
        FilterConditionKind.Number => condition.NumericOperator,
        FilterConditionKind.Date => condition.DateOperator,
        _ => null,
    };

    /// <summary>Whether a condition with <paramref name="op"/> compares with a value (Is empty and Is not empty do not).</summary>
    /// <param name="op">The boxed operator.</param>
    internal static bool NeedsValue(object? op) => op is not (TextFilterOperator.Empty or TextFilterOperator.NotEmpty);

    /// <summary>A condition with <paramref name="op"/> and <paramref name="value"/>.</summary>
    /// <param name="op">The boxed operator.</param>
    /// <param name="value">The typed value (a string, a double or a date), or null.</param>
    internal static FilterCondition Create(object op, object? value) => op switch
    {
        TextFilterOperator t => new FilterCondition { TextOperator = t, Value = value },
        NumericFilterOperator n => new FilterCondition { NumericOperator = n, Value = value },
        DateFilterOperator d => new FilterCondition { DateOperator = d, Value = value },
        _ => new FilterCondition { Value = value },
    };
}
