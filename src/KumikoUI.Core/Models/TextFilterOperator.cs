namespace KumikoUI.Core.Models;

/// <summary>
/// Filter operator for text columns.
/// </summary>
public enum TextFilterOperator
{
    /// <summary>Text equals the filter value (case-insensitive).</summary>
    Equals,
    /// <summary>Text does not equal the filter value.</summary>
    NotEquals,
    /// <summary>Text contains the filter value.</summary>
    Contains,
    /// <summary>Text does not contain the filter value.</summary>
    NotContains,
    /// <summary>Text starts with the filter value.</summary>
    StartsWith,
    /// <summary>Text ends with the filter value.</summary>
    EndsWith,
    /// <summary>Text is null or empty.</summary>
    Empty,
    /// <summary>Text is not null or empty.</summary>
    NotEmpty
}
