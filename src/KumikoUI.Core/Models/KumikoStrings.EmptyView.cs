namespace KumikoUI.Core.Models;

/// <summary>The texts of the empty view drawn when the view has no rows (#326).</summary>
public sealed partial record KumikoStrings
{
    /// <summary>The empty view's text when the bound collection has no items. Default: "No items to show".</summary>
    public string EmptyViewNoItems { get; init; } = "No items to show";

    /// <summary>The empty view's text when the collection has items but the filters or the search hide all of them. Default: "No rows match the current filters".</summary>
    public string EmptyViewNoMatches { get; init; } = "No rows match the current filters";
}
