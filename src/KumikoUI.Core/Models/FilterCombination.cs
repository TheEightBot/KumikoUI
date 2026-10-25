namespace KumikoUI.Core.Models;

/// <summary>
/// How to combine multiple filter conditions within a compound filter.
/// </summary>
public enum FilterCombination
{
    /// <summary>All conditions must match (logical AND).</summary>
    And,
    /// <summary>Any condition may match (logical OR).</summary>
    Or
}
