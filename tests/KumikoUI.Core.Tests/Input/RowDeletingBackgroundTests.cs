using System.Collections.ObjectModel;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Deleting rows (#328) while the view rebuilds on a worker (#254): the removal coalesces into one rebuild and the
/// selection and the current cell land on the row that takes the deleted row's place once the view is swapped in.
/// </summary>
public class RowDeletingBackgroundTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private sealed class Item
    {
        public int Id { get; init; }
    }

    [Fact]
    public async Task TheCurrentCell_FollowsTheSuccessorThroughTheBackgroundRebuild()
    {
        using var ui = new TestUiThread();
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 5000).Select(i => new Item { Id = i }));
        var (source, selection, input, scroll, style) = ui.Invoke(() =>
        {
            var s = new KumikoSource { BackgroundRebuildThreshold = 1000 };
            s.SetColumns(new[] { new KumikoColumn { PropertyName = nameof(Item.Id), ColumnType = KumikoColumnType.Numeric } });
            s.SetItems(items);
            return (s, new KumikoSelectionModel(s), new KumikoInputController { AllowDeleting = true },
                new ScrollState { ViewportWidth = 300, ViewportHeight = 300 }, new KumikoStyle());
        });
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);
        int structural = 0;
        ui.Invoke(() => source.ViewChanged += (_, e) => { if (e.IsStructural) Interlocked.Increment(ref structural); });
        var doomed = new object[] { items[100], items[101], items[102] };
        var successor = items[103];

        bool deleted = ui.Invoke(() =>
        {
            selection.SetSelectedItems(doomed);
            selection.SetCurrentCell(100, 0);
            return input.DeleteSelectedRows(scroll, selection, style, source);
        });
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(Timeout);

        Assert.True(deleted);
        var (current, currentItem, rows, count) = ui.Invoke(() =>
            (selection.CurrentCell, selection.CurrentItem, selection.SelectedRows.ToArray(), source.RowCount));
        Assert.Equal(4997, count);
        Assert.Same(successor, currentItem);
        Assert.Equal(new CellPosition(100, 0), current);
        Assert.Equal(new[] { 100 }, rows);
        Assert.Equal(1, Volatile.Read(ref structural));
    }
}
