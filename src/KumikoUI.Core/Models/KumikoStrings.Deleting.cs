namespace KumikoUI.Core.Models;

/// <summary>The texts of deleting rows (#328).</summary>
public sealed partial record KumikoStrings
{
    /// <summary>The announcement after several rows were deleted. <c>{0}</c> is the number of rows. Default: "{0} rows deleted".</summary>
    public string AnnounceRowsDeleted { get; init; } = "{0} rows deleted";

    /// <summary>The announcement after one row was deleted. Default: "Row deleted".</summary>
    public string AnnounceRowDeleted { get; init; } = "Row deleted";
}
