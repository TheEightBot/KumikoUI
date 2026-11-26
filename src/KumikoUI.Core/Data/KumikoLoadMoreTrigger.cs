namespace KumikoUI.Core.Data;

/// <summary>What started a batch (<see cref="KumikoLoadMoreRequestedEventArgs.Trigger"/>).</summary>
public enum KumikoLoadMoreTrigger
{
    /// <summary>Scrolling, a resize, a view change or the first paint brought the end of the rows within the threshold.</summary>
    NearEnd,

    /// <summary>Down, Page Down or Ctrl+End on the last row, while the "Load more" or the error row is shown.</summary>
    Keyboard,

    /// <summary>A tap or click on the "Load more" or the error row.</summary>
    Tap,

    /// <summary>A call to <see cref="KumikoIncrementalLoader.LoadMoreAsync"/>.</summary>
    Programmatic,
}
