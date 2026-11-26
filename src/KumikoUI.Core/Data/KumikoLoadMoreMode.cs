namespace KumikoUI.Core.Data;

/// <summary>When a grid with an incremental source (<see cref="IKumikoIncrementalSource"/>) loads the next batch.</summary>
public enum KumikoLoadMoreMode
{
    /// <summary>
    /// Never on its own. <see cref="KumikoIncrementalLoader.LoadMoreAsync"/> still loads, and its loading and error
    /// rows are still drawn.
    /// </summary>
    None,

    /// <summary>When the user nears the end of the loaded rows (see <see cref="KumikoIncrementalLoader.Threshold"/>). The default.</summary>
    Automatic,

    /// <summary>A "Load more" row is drawn after the last row; a tap on it (or Down on the last row) loads the next batch.</summary>
    Manual,
}
