using KumikoUI.Core.Data;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>The load-more row (#318): a tap loads or retries, and so does a key that moves past the last row.</summary>
public sealed partial class KumikoInputController
{
    /// <summary>
    /// Down, Page Down or Ctrl+End with the current cell on the last row (or no rows at all) activates a Manual
    /// "Load more" row or a failed load's retry row; the key's own navigation still runs. In Automatic mode the
    /// near-end check already loads, so nothing more happens here.
    /// </summary>
    private void TryLoadMoreFromKey(KumikoKeyEventArgs e, KumikoSelectionModel selection, KumikoSource dataSource)
    {
        if (dataSource.IncrementalLoaderOrNull is not { } loader) return;
        if (e.Key is not (KumikoKey.Down or KumikoKey.PageDown) && !(e.Key == KumikoKey.End && e.HasControl)) return;

        int rows = dataSource.RowCount;
        bool onLastRow = selection.CurrentCell.IsValid ? selection.CurrentCell.Row >= rows - 1 : rows == 0;
        if (onLastRow && loader.Activate(KumikoLoadMoreTrigger.Keyboard))
            Redraw();
    }

    /// <summary>A release on the load-more row that was pressed too: load, or retry. Never touches the selection.</summary>
    private void TapLoadMoreRow(KumikoSource dataSource)
    {
        if (_pressRegion == HitRegion.LoadMoreRow &&
            dataSource.IncrementalLoaderOrNull?.Activate(KumikoLoadMoreTrigger.Tap) == true)
            Redraw();
    }

    /// <summary>Whether the load-more row reacts to a click: it shows "Load more" or a failed load's retry.</summary>
    private static bool IsLoadMoreRowActionable(KumikoSource dataSource) =>
        dataSource.IncrementalLoaderOrNull?.RowKind is LoadMoreRowKind.LoadMore or LoadMoreRowKind.Failed;
}
