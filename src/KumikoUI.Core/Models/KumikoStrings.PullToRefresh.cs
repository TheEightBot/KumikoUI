namespace KumikoUI.Core.Models;

/// <summary>The texts of pull-to-refresh (#330): its announcements.</summary>
public sealed partial record KumikoStrings
{
    /// <summary>The announcement when a refresh starts (from a pull or from code). Default: "Refreshing".</summary>
    public string AnnounceRefreshing { get; init; } = "Refreshing";

    /// <summary>The announcement when a refresh ended. Default: "Updated".</summary>
    public string AnnounceRefreshed { get; init; } = "Updated";
}
