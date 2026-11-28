namespace KumikoUI.Core.Models;

public partial class KumikoSource
{
    /// <summary>The group a group summary row belongs to, or null when the row is not one.</summary>
    internal GroupHeaderInfo? GetGroupOfSummaryRow(int visibleRow)
    {
        EnsureView();
        var flat = _view.FlatView;
        if (flat == null || visibleRow < 0 || visibleRow >= flat.Count) return null;
        var entry = flat[visibleRow];
        return entry.Kind == FlatRowKind.GroupSummary ? entry.GroupInfo : null;
    }
}
