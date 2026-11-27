namespace KumikoUI.Core.Input;

/// <summary>
/// Data for <see cref="KumikoInputController.RowsDeleting"/>, raised before the grid removes rows from its items
/// source: set <see cref="Cancel"/> to keep them.
/// </summary>
public sealed class KumikoRowsDeletingEventArgs : EventArgs
{
    /// <summary>The data items about to be removed, in the order of the rows in the view.</summary>
    public IReadOnlyList<object> Items { get; }

    /// <summary>What asked for the delete.</summary>
    public KumikoRowsDeleteTrigger Trigger { get; }

    /// <summary>
    /// Set to <c>true</c> to keep the rows: nothing is removed and <see cref="KumikoInputController.RowsDeleted"/> is not
    /// raised. The event is synchronous, so to ask the user first, cancel here, show the dialog and then call
    /// <see cref="Models.KumikoSource.RemoveItems"/> yourself with <see cref="Items"/> once they agree.
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>Initializes a new instance of the <see cref="KumikoRowsDeletingEventArgs"/> class.</summary>
    /// <param name="items">The data items about to be removed.</param>
    /// <param name="trigger">What asked for the delete.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    public KumikoRowsDeletingEventArgs(IReadOnlyList<object> items, KumikoRowsDeleteTrigger trigger)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
        Trigger = trigger;
    }
}
