namespace KumikoUI.Core.Models;

/// <summary>
/// Built-in aggregate function types for summary calculations.
/// </summary>
public enum SummaryType
{
    /// <summary>Sum of numeric values.</summary>
    Sum,
    /// <summary>Average of numeric values.</summary>
    Average,
    /// <summary>Count of items.</summary>
    Count,
    /// <summary>Minimum value.</summary>
    Min,
    /// <summary>Maximum value.</summary>
    Max,
    /// <summary>Custom aggregate using a user-supplied function.</summary>
    Custom
}
