namespace KumikoUI.Core.Models;

/// <summary>
/// Filter operator for numeric columns.
/// </summary>
public enum NumericFilterOperator
{
    /// <summary>Value equals the filter value.</summary>
    Equals,
    /// <summary>Value does not equal the filter value.</summary>
    NotEquals,
    /// <summary>Value is less than the filter value.</summary>
    LessThan,
    /// <summary>Value is less than or equal to the filter value.</summary>
    LessThanOrEqual,
    /// <summary>Value is greater than the filter value.</summary>
    GreaterThan,
    /// <summary>Value is greater than or equal to the filter value.</summary>
    GreaterThanOrEqual
}
