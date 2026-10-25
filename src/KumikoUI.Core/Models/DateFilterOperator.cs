namespace KumikoUI.Core.Models;

/// <summary>
/// Filter operator for date columns.
/// </summary>
public enum DateFilterOperator
{
    /// <summary>Date equals the filter value.</summary>
    Equals,
    /// <summary>Date does not equal the filter value.</summary>
    NotEquals,
    /// <summary>Date is before the filter value.</summary>
    Before,
    /// <summary>Date is after the filter value.</summary>
    After,
    /// <summary>Date is on or before the filter value.</summary>
    BeforeOrEqual,
    /// <summary>Date is on or after the filter value.</summary>
    AfterOrEqual
}
