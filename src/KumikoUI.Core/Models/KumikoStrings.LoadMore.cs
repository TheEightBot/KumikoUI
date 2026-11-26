namespace KumikoUI.Core.Models;

/// <summary>The texts of incremental "load more" loading (#318): the load-more row and its announcements.</summary>
public sealed partial record KumikoStrings
{
    /// <summary>The text next to the busy indicator on the load-more row while a batch loads. Default: "Loading more...".</summary>
    public string LoadMoreLoading { get; init; } = "Loading more...";

    /// <summary>The button text of the load-more row in Manual mode. Default: "Load more".</summary>
    public string LoadMoreButton { get; init; } = "Load more";

    /// <summary>The error text of the load-more row when a batch failed and no message was given. Default: "Could not load more rows.".</summary>
    public string LoadMoreFailed { get; init; } = "Could not load more rows.";

    /// <summary>The retry button text of the load-more row after a failure. Default: "Retry".</summary>
    public string LoadMoreRetry { get; init; } = "Retry";

    /// <summary>The announcement when a batch starts loading. Default: "Loading more rows.".</summary>
    public string AnnounceLoadMoreStarted { get; init; } = "Loading more rows.";

    /// <summary>The announcement when a batch added rows. <c>{0}</c> is the number of rows added. Default: "{0} more rows loaded.".</summary>
    public string AnnounceLoadMoreCompleted { get; init; } = "{0} more rows loaded.";

    /// <summary>The announcement when the last batch was loaded. Default: "All rows loaded.".</summary>
    public string AnnounceAllRowsLoaded { get; init; } = "All rows loaded.";

    /// <summary>The announcement when a batch failed. <c>{0}</c> is the error text. Default: "Could not load more rows. {0}".</summary>
    public string AnnounceLoadMoreFailed { get; init; } = "Could not load more rows. {0}";
}
