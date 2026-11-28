using KumikoUI.Core.Models;

namespace KumikoUI.Core.Layout;

/// <summary>
/// When the empty view (#326) applies and which text it shows. The renderer and both hosts' accessibility code ask here,
/// so every condition (the busy indicator, #327, among them) lives in one place.
/// </summary>
internal static class EmptyViewState
{
    /// <summary>
    /// Whether the view shows an empty view: it has no rows, the style asks for one, and no load-more row is taking the
    /// data area (the first batch of incremental loading, #318, or a "Load more" button), and the grid is not busy (#327):
    /// "no data" would be wrong while the data is still coming.
    /// </summary>
    public static bool IsShown(KumikoSource source, KumikoStyle style, bool isBusy) =>
        !isBusy && style.ShowEmptyView && source.RowCount == 0 && LoadMoreRowLayout.GetHeight(source, style) <= 0f;

    /// <summary>The text for <paramref name="kind"/>: <paramref name="overrideText"/> when set, else the localized string. Allocates nothing.</summary>
    public static string TextFor(KumikoStrings strings, KumikoEmptyViewKind kind, string? overrideText) =>
        KumikoAnnouncements.EmptyView(strings, kind, overrideText);

    /// <summary>The text to show now, or null when no empty view is shown.</summary>
    public static string? CurrentText(KumikoSource source, KumikoStyle style, string? overrideText, bool isBusy) =>
        IsShown(source, style, isBusy) ? TextFor(source.Strings, source.EmptyViewKind, overrideText) : null;
}

/// <summary>
/// Decides when a screen reader hears the empty text: once when a filter or search change empties a view that had rows.
/// The hosts call <see cref="Observe"/> after every structural view change.
/// </summary>
internal sealed class EmptyViewAnnouncer
{
    private bool _hadRows;

    /// <summary>
    /// The text to announce for this change, or null. It is non-null when the view just went from rows to none because
    /// of a filter or search, and an empty view is shown.
    /// </summary>
    public string? Observe(KumikoSource source, KumikoStyle style, string? overrideText, bool isBusy)
    {
        bool hasRows = source.RowCount > 0;
        bool emptied = _hadRows && !hasRows;
        _hadRows = hasRows;
        if (!emptied || !source.IsFiltered || source.ItemCount == 0) return null;
        return EmptyViewState.CurrentText(source, style, overrideText, isBusy);
    }
}
