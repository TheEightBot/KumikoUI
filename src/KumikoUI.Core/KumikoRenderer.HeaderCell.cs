using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

/// <summary>The header cell renderer hook (#333): who draws the content of a column header.</summary>
public sealed partial class KumikoRenderer
{
    private static readonly TextHeaderCellRenderer DefaultHeaderCellRenderer = new();
    private IHeaderCellRenderer? _headerCellRenderer;

    /// <summary>
    /// Draws the content of every column header that has no <see cref="KumikoColumn.CustomHeaderRenderer"/> of its
    /// own, in place of the built-in <see cref="TextHeaderCellRenderer"/>; null (the default) uses the built-in one.
    /// The grid keeps the background, the hover feedback, the sort and filter glyphs, the resize grips and the drag
    /// ghost. The renderer also measures the header width for auto-fit
    /// (<see cref="IHeaderCellRenderer.MeasureWidth"/>), and must not allocate per frame. The host redraws after
    /// changing it.
    /// </summary>
    public IHeaderCellRenderer? HeaderCellRenderer
    {
        get => _headerCellRenderer;
        set
        {
            _headerCellRenderer = value;
            _layout.HeaderCellRenderer = value;
            _layout.HeaderPaints = _paintCache;
        }
    }

    /// <summary>
    /// The renderer that draws <paramref name="column"/>'s header: its own
    /// <see cref="KumikoColumn.CustomHeaderRenderer"/>, else the grid-wide <paramref name="gridWide"/>, else the
    /// built-in text renderer.
    /// </summary>
    internal static IHeaderCellRenderer ResolveHeaderCellRenderer(KumikoColumn column, IHeaderCellRenderer? gridWide) =>
        column.CustomHeaderRenderer ?? gridWide ?? DefaultHeaderCellRenderer;

    /// <summary>
    /// Draws the content of one header cell through its renderer, within <paramref name="contentRect"/>. A custom
    /// renderer is clipped to the rectangle; the built-in text renderer clips its own text.
    /// </summary>
    private void DrawHeaderContent(IDrawingContext ctx, KumikoColumn col, int columnIndex, KumikoRect cellRect,
        KumikoRect contentRect, KumikoStyle style, KumikoCellStyle? hStyle, KumikoPaint textPaint)
    {
        var renderer = ResolveHeaderCellRenderer(col, _headerCellRenderer);
        var context = new HeaderCellRenderContext(
            contentRect, cellRect, col, columnIndex, col.SortDirection, col.SortOrder, col.ActiveFilter != null,
            style, hStyle, _paintCache, textPaint, hStyle?.TextAlignment ?? KumikoTextAlignment.Left);

        if (ReferenceEquals(renderer, DefaultHeaderCellRenderer))
        {
            renderer.Render(ctx, in context);
            return;
        }

        ctx.Save();
        ctx.ClipRect(contentRect);
        renderer.Render(ctx, in context);
        ctx.Restore();
    }
}
