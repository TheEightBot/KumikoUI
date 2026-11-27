using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>Data for <see cref="KumikoInputController.RowsDeleted"/>, raised after the grid tried to remove rows.</summary>
public sealed class KumikoRowsDeletedEventArgs : EventArgs
{
    /// <summary>The data items the grid asked <see cref="KumikoSource.RemoveItems"/> to remove, in the order of the rows in the view.</summary>
    public IReadOnlyList<object> Items { get; }

    /// <summary>
    /// What <see cref="KumikoSource.RemoveItems"/> did: <see cref="KumikoRemoveResult.Removed"/>, or the reason nothing
    /// changed (<see cref="KumikoRemoveResult.NotSupportedSource"/> for a read-only or fixed-size source).
    /// </summary>
    public KumikoRemoveResult Result { get; }

    /// <summary>Initializes a new instance of the <see cref="KumikoRowsDeletedEventArgs"/> class.</summary>
    /// <param name="items">The data items the delete covered.</param>
    /// <param name="result">What the removal did.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    public KumikoRowsDeletedEventArgs(IReadOnlyList<object> items, KumikoRemoveResult result)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
        Result = result;
    }
}
