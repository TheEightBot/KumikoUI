using System.Collections;
using KumikoUI.Core.Export;

namespace KumikoUI.Core.Models;

// Export (#323): the view an export snapshot is taken from. The snapshot copies what it needs right away, on the
// source's thread, so the view returned here is never kept.
public partial class KumikoSource
{
    /// <summary>
    /// The view an export of <paramref name="rows"/> in <paramref name="layout"/> reads: the published view when it
    /// already is what the export needs, otherwise a view built on the side (the live view is not touched) with every
    /// group expanded and, for <see cref="KumikoExportGroupLayout.None"/>, without grouping. For
    /// <see cref="KumikoExportRows.All"/> the view is null and <paramref name="allItems"/> is the bound collection.
    /// </summary>
    internal ViewSnapshot? CaptureExportView(KumikoExportRows rows, KumikoExportGroupLayout layout, out IList? allItems)
    {
        VerifyAccess();
        EnsureView();
        allItems = null;
        if (rows == KumikoExportRows.All)
        {
            allItems = _sourceItems;
            return null;
        }

        var live = _view;
        if (rows == KumikoExportRows.Visible || live.FlatView == null) return live;

        // Grouping is active. The flat view hides the rows of collapsed groups, so use it only when nothing is
        // collapsed and the layout keeps the groups.
        if (layout != KumikoExportGroupLayout.None && live.Groups.All(g => g.IsExpanded)) return live;

        return ViewBuilder.Build(
            CaptureBuildSettings(_sourceItems, itemsAreCopy: false, forWorker: false,
                forceExpanded: true, noGroups: layout == KumikoExportGroupLayout.None),
            reuse: null, CancellationToken.None);
    }
}
