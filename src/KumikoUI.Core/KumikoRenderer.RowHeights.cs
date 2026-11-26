using System.Diagnostics;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

// Variable row heights (#320): the frame step that measures rows before they are drawn. Heights live in the source's
// KumikoRowLayout; this is the only place they are measured, because only a frame has a drawing context.
public sealed partial class KumikoRenderer
{
    // Rows measured beyond each end of the visible scrolling rows, so the rows a scroll or arrow key reveals next
    // are already exact.
    private const int MeasureOverscanRows = 2;

    // Times the measure-and-anchor step repeats when measuring changed which rows are visible.
    private const int MaxMeasurePasses = 3;

    // Per-frame measuring budget: once a frame has measured MinMeasuredRowsPerFrame rows (a screenful, so the rows on
    // screen are exact), it stops after this many measured cells or this much time; the remaining rows draw at their
    // estimate and another frame is requested.
    private const int MinMeasuredRowsPerFrame = 48;
    private const int MeasureCellBudget = 256;
    private static readonly long s_measureTimeBudget = Stopwatch.Frequency * 4 / 1000;

    private RowMeasurer? _rowMeasurer;

    /// <summary>
    /// Measures the rows this frame shows whose height is not current, keeping the visible rows still (scroll
    /// anchoring) or finishing a pending programmatic scroll, within a per-frame budget (spec #102 §7.1). Does nothing
    /// with uniform rows. A steady frame (every visible row measured and current) allocates nothing.
    /// </summary>
    private void MeasureRowHeights(IDrawingContext ctx, KumikoSource source, ScrollState scroll, KumikoStyle style,
        float groupPanelOffset, float topSummaryHeight, float bottomSummaryHeight)
    {
        var rows = source.RowLayout;
        if (!rows.BeginFrame(style, source.Columns)) return;

        _paintCache.Update(style);
        var measurer = _rowMeasurer ??= new RowMeasurer(this);
        measurer.Begin(ctx, source, style);
        try
        {
            int count = rows.Count;
            int frozen = source.EffectiveFrozenRowCount;
            bool exhausted = false;

            float offset = scroll.OffsetY;
            var pending = rows.PendingScrollTarget;
            rows.PendingScrollTarget = null;
            bool targeted = pending is { } p && p.OffsetY == scroll.OffsetY && p.Row >= frozen && p.Row < count;

            // The anchor: the first visible scrolling row, as the last frame laid it out, stays where it is on screen
            // while the rows above it change height (or the estimate they use changes).
            int anchor = Math.Clamp(rows.GetScrollRowAt(offset, style), frozen, Math.Max(frozen, count - 1));
            float anchorDelta = offset - rows.GetScrollTop(anchor, style);

            // Frozen rows are always exact: they are measured whenever they are stale.
            bool frozenChanged = false;
            for (int row = 0; row < frozen && !exhausted; row++)
                frozenChanged |= measurer.Measure(row, ref exhausted);
            if (frozenChanged && !targeted) offset = rows.GetScrollTop(anchor, style) + anchorDelta;

            bool settled = false;
            for (int pass = 0; pass < MaxMeasurePasses && !exhausted; pass++)
            {
                if (targeted)
                    offset = KumikoScroller.GetRowOffsetY(source, style, scroll, pending!.Value.Row, pending.Value.Position);
                offset = ClampMeasureOffset(offset, source, scroll, style, groupPanelOffset, topSummaryHeight,
                    bottomSummaryHeight);

                float viewport = KumikoScroller.GetScrollableRowsViewportHeight(scroll, style, source);
                int first = Math.Max(frozen, rows.GetScrollRowAt(offset, style));
                int last = Math.Min(count - 1, rows.GetScrollRowAt(offset + Math.Max(0, viewport), style));
                if (first > last)
                {
                    settled = true;
                    break;
                }

                bool changed = false;
                int from = Math.Max(frozen, first - MeasureOverscanRows);
                int to = Math.Min(count - 1, last + MeasureOverscanRows);
                for (int row = from; row <= to && !exhausted; row++)
                    changed |= measurer.Measure(row, ref exhausted);

                if (!changed)
                {
                    settled = true;
                    break;
                }
                if (!targeted) offset = rows.GetScrollTop(anchor, style) + anchorDelta;
            }

            if (offset != scroll.OffsetY) scroll.OffsetY = offset;
            // Rows left at their estimate (budget spent, or the visible rows still changing): finish next frame.
            if (exhausted || !settled) RedrawRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            measurer.End();
        }
    }

    /// <summary>The offset clamped as the frame will clamp it, with the heights known now.</summary>
    private static float ClampMeasureOffset(float offset, KumikoSource source, ScrollState scroll, KumikoStyle style,
        float groupPanelOffset, float topSummaryHeight, float bottomSummaryHeight)
    {
        float content = style.HeaderHeight + RowsHeight(source, style) + groupPanelOffset + topSummaryHeight
            + bottomSummaryHeight + LoadMoreRowLayout.GetHeight(source, style);
        float max = Math.Max(0, content + scroll.BottomInset - scroll.ViewportHeight);
        return Math.Clamp(offset, 0, max);
    }

    /// <summary>The height of every row of the view as laid out now.</summary>
    private static float RowsHeight(KumikoSource source, KumikoStyle style) => LoadMoreRowLayout.GetRowsHeight(source, style);

    /// <summary>
    /// How far the rows after a toggled group header slide (#101): the height of the <paramref name="delta"/> rows that
    /// appear below <paramref name="anchor"/> (expanding), or that many rows at <see cref="KumikoStyle.RowHeight"/>
    /// (collapsing, whose rows have left the view).
    /// </summary>
    private static float SlideDistance(KumikoSource source, KumikoStyle style, int anchor, int delta)
    {
        var rows = source.RowLayout;
        if (rows.IsUniform(style) || delta <= 0) return Math.Abs(delta) * style.RowHeight;
        int end = Math.Min(source.RowCount, anchor + 1 + delta);
        return rows.GetScrollTop(end, style) - rows.GetScrollTop(anchor + 1, style);
    }

    /// <summary>
    /// Measures data rows for one frame: the cells of every visible measurable column, resolved and styled exactly as
    /// <c>DrawRowCells</c> draws them, then the row height resolver. Also answers
    /// <see cref="KumikoRowHeightContext.MeasureContentHeight"/>.
    /// </summary>
    private sealed class RowMeasurer : IRowContentMeasurer
    {
        private readonly KumikoRenderer _renderer;
        private IDrawingContext? _ctx;
        private KumikoSource? _source;
        private KumikoStyle? _style;
        private long _start;
        private int _cells;
        private int _rowsMeasured;

        public RowMeasurer(KumikoRenderer renderer) => _renderer = renderer;

        public void Begin(IDrawingContext ctx, KumikoSource source, KumikoStyle style)
        {
            _ctx = ctx;
            _source = source;
            _style = style;
            _start = Stopwatch.GetTimestamp();
            _cells = 0;
            _rowsMeasured = 0;
        }

        public void End()
        {
            _ctx = null;
            _source = null;
            _style = null;
        }

        /// <summary>
        /// Measures <paramref name="row"/> when its height is not current; returns whether the layout changed. Sets
        /// <paramref name="exhausted"/> once the frame's budget is spent (the row is left for a later frame).
        /// </summary>
        public bool Measure(int row, ref bool exhausted)
        {
            var source = _source!;
            var rows = source.RowLayout;
            if (rows.IsCurrent(row)) return false;
            if (_rowsMeasured >= MinMeasuredRowsPerFrame &&
                (_cells >= MeasureCellBudget || Stopwatch.GetTimestamp() - _start > s_measureTimeBudget))
            {
                exhausted = true;
                return false;
            }

            _rowsMeasured++;
            object item = source.GetItem(row);
            return rows.SetMeasuredHeight(row, item, ComputeRowHeight(row, item));
        }

        private float ComputeRowHeight(int row, object item)
        {
            var style = _style!;
            if (style.RowHeightResolver is { } resolver)
            {
                var context = new KumikoRowHeightContext(item, row, style.RowHeightMode, style.RowHeight, this);
                if (resolver(in context) is { } resolved && float.IsFinite(resolved) && resolved >= 1f)
                    return resolved;
            }
            return style.RowHeightMode == KumikoRowHeightMode.Auto ? MeasureContentHeight(row, item) : style.RowHeight;
        }

        /// <inheritdoc />
        public float MeasureContentHeight(int row, object item)
        {
            var style = _style;
            var source = _source;
            var ctx = _ctx;
            if (style == null || source == null || ctx == null) return 0f;

            float rowHeight = Math.Max(1f, style.RowHeight);
            float min = Math.Max(1f, style.MinRowHeight ?? rowHeight);
            float max = style.MaxRowHeight is { } limit && limit >= min ? limit : float.PositiveInfinity;

            var renderer = _renderer;
            var rowStyle = style.RowStyleResolver?.Invoke(item);
            float content = 0f;
            bool any = false;
            var columns = source.Columns;
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (!col.IsVisible) continue;
                var cellRenderer = col.CustomCellRenderer ?? renderer.GetCellRenderer(col.ColumnType);
                if (cellRenderer is not IMeasurableCellRenderer measurable) continue;
                // The built-in text renderer measures only wrapping columns: skip the others without reading the text.
                if (col.TextWrap == KumikoTextWrap.None && cellRenderer.GetType() == typeof(TextCellRenderer)) continue;

                var cellStyle = renderer.ResolveCellStyle(style, col, item, rowStyle);
                float padding = cellStyle?.Padding ?? style.CellPadding;
                string text = source.GetCachedCellText(row, item, col, out object? value);
                var cell = new CellRenderContext(
                    new KumikoRect(0, 0, Math.Max(0, col.Width - padding * 2), rowHeight), row, value, text, col, style,
                    cellStyle, false, renderer._paintCache, renderer._paintCache.CellTextPaint(cellStyle, style),
                    cellStyle?.TextAlignment ?? col.TextAlignment, renderer.ImageResolver);
                _cells++;
                if (measurable.MeasureHeight(ctx, in cell) is { } height && float.IsFinite(height))
                {
                    content = Math.Max(content, height);
                    any = true;
                }
            }

            float total = any ? content + 2 * Math.Max(0, style.CellVerticalPadding) : min;
            return Math.Clamp(total, min, max);
        }
    }
}
