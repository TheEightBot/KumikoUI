using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

/// <summary>The empty view (#326): the message drawn in the data area while the view has no rows.</summary>
public sealed partial class KumikoRenderer
{
    private readonly KumikoPaint _emptyViewPaint = new() { IsAntiAlias = true };
    private bool _emptyViewHadRows; // the previous frame drew rows: a pending rebuild that empties the view must not flash the message

    /// <summary>
    /// Draws the empty view in place of the built-in centered text, or null (the default) for the text. The renderer gets
    /// the data area, the kind and the text the built-in view would draw; see <see cref="IKumikoEmptyViewRenderer"/>.
    /// </summary>
    public IKumikoEmptyViewRenderer? EmptyViewRenderer { get; set; }

    /// <summary>
    /// Replaces both of the strings table's empty view texts (<see cref="KumikoStrings.EmptyViewNoItems"/>,
    /// <see cref="KumikoStrings.EmptyViewNoMatches"/>) when set. Null (the default) uses the table.
    /// </summary>
    public string? EmptyViewText { get; set; }

    /// <summary>
    /// The one check for whether this frame draws the empty view, so a later condition (a busy overlay) extends one place:
    /// the view shows an empty view (<see cref="EmptyViewState.IsShown"/>) and no background rebuild that started from a
    /// view with rows is still pending.
    /// </summary>
    private bool ShouldDrawEmptyView(KumikoSource source, KumikoStyle style)
    {
        bool hadRows = _emptyViewHadRows;
        _emptyViewHadRows = source.RowCount > 0;
        if (!EmptyViewState.IsShown(source, style)) return false;
        return !(hadRows && source.IsViewUpdatePending);
    }

    /// <summary>Draws the empty view over the data area <paramref name="bounds"/>, when this frame shows one.</summary>
    private void DrawEmptyView(IDrawingContext ctx, KumikoSource source, KumikoStyle style, KumikoRect bounds)
    {
        if (!ShouldDrawEmptyView(source, style) || bounds.Width <= 0 || bounds.Height <= 0) return;

        var kind = source.EmptyViewKind;
        string text = EmptyViewState.TextFor(source.Strings, kind, EmptyViewText);
        if (EmptyViewRenderer is { } custom)
        {
            custom.Render(ctx, bounds, kind, text, style);
            return;
        }

        _emptyViewPaint.Color = style.EmptyViewTextColor;
        _emptyViewPaint.Font = style.EmptyViewFont ?? style.CellFont;
        float pad = Math.Max(style.CellPadding, 0f) * 2f;
        var area = new KumikoRect(bounds.X + pad, bounds.Y, Math.Max(0f, bounds.Width - 2f * pad), bounds.Height);
        ctx.DrawTextInRect(text, area, _emptyViewPaint, KumikoTextAlignment.Center, KumikoVerticalAlignment.Center, true,
            KumikoTextWrap.Word);
    }
}
