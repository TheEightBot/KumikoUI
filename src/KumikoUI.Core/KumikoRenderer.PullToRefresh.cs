using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

/// <summary>
/// The pull-to-refresh indicator (#330): a disc that slides down from under the header (and the grouping panel) over
/// the rows, with an arrow that grows and turns with the pull and becomes a spinner while refreshing. It is drawn
/// over the rows, after the scroll bars and before the editor and the popups; the rows themselves never move.
/// </summary>
public sealed partial class KumikoRenderer
{
    /// <summary>The diameter of the indicator's disc, in pixels.</summary>
    internal const float PullIndicatorDiameter = 36f;

    // The arrow and the spinner are an arc of up to three quarters of a turn, drawn as a polyline of this many segments
    private const int PullArcSegments = 24;
    private const float PullArcSweep = 1.5f * MathF.PI;

    // Set just before each draw from the style, so the indicator allocates nothing per frame
    private readonly KumikoPaint _pullDisc = new() { IsAntiAlias = true };
    private readonly KumikoPaint _pullArc = new() { IsAntiAlias = true, StrokeWidth = 2.5f };
    private readonly KumikoPaint _pullHead = new() { IsAntiAlias = true };

    /// <summary>Where the last frame drew the indicator's disc, in viewport coordinates; empty when it was not drawn.</summary>
    internal KumikoRect PullIndicatorBounds { get; private set; }

    /// <summary>
    /// Draws the indicator of the connected controller's pull (<see cref="KumikoInputController.IsPullToRefreshEnabled"/>)
    /// or refresh (<see cref="KumikoInputController.IsRefreshing"/>), clipped to below the header, in viewport coordinates.
    /// </summary>
    private void DrawPullToRefresh(IDrawingContext ctx, ScrollState scroll, KumikoStyle style, float groupPanelOffset)
    {
        PullIndicatorBounds = KumikoRect.Empty;
        if (_inputController is not { } input) return;
        input.ObservePullToRefreshStyle(style);
        var pull = input.PullToRefresh;
        if (!pull.IsVisible) return;

        float top = groupPanelOffset + style.HeaderHeight;
        if (scroll.ViewportHeight <= top || scroll.ViewportWidth <= 0) return;

        const float diameter = PullIndicatorDiameter;
        float centerX = scroll.ViewportWidth / 2;
        float centerY = top + pull.Distance - diameter / 2;
        var disc = new KumikoRect(centerX - diameter / 2, centerY - diameter / 2, diameter, diameter);
        PullIndicatorBounds = disc;

        var color = style.RefreshIndicatorColor ?? style.AccentColor;
        bool animate = input.CanAnimate(style);

        ctx.Save();
        ctx.ClipRect(new KumikoRect(0, top, scroll.ViewportWidth, scroll.ViewportHeight - top)); // out from under the header
        _pullDisc.Color = style.RefreshIndicatorBackgroundColor;
        ctx.FillRoundRect(disc, diameter / 2, _pullDisc);
        ctx.DrawRoundRect(disc, diameter / 2, _paintCache.GridLine);

        float radius = diameter * 0.27f;
        if (pull.IsRefreshing)
        {
            // A spinner: three quarters of a turn, turning once per period; standing still with reduced motion.
            float start = animate ? pull.SpinMs / PullToRefreshState.SpinPeriodMs * 2f * MathF.PI : 0f;
            _pullArc.Color = color;
            DrawPullArc(ctx, centerX, centerY, radius, start - MathF.PI / 2, PullArcSweep, arrowHead: false);
        }
        else
        {
            // An arrow that grows and turns with the pull, fainter until the threshold; a fixed arrow with reduced motion.
            float progress = pull.Progress;
            float sweep = animate ? Math.Max(0.15f, progress) * PullArcSweep : PullArcSweep;
            float rotation = animate ? progress * MathF.PI : 0f;
            var faded = new KumikoColor(color.R, color.G, color.B, (byte)(color.A * (0.35f + 0.65f * progress)));
            _pullArc.Color = faded;
            _pullHead.Color = faded;
            DrawPullArc(ctx, centerX, centerY, radius, rotation - MathF.PI / 2, sweep, arrowHead: true);
        }
        ctx.Restore();
    }

    /// <summary>An arc around (<paramref name="centerX"/>, <paramref name="centerY"/>), clockwise from <paramref name="start"/>, with an optional arrow head at its end.</summary>
    private void DrawPullArc(IDrawingContext ctx, float centerX, float centerY, float radius, float start, float sweep,
        bool arrowHead)
    {
        Span<KumikoPoint> arc = stackalloc KumikoPoint[PullArcSegments + 1];
        for (int i = 0; i <= PullArcSegments; i++)
        {
            float angle = start + sweep * i / PullArcSegments;
            arc[i] = new KumikoPoint(centerX + radius * MathF.Cos(angle), centerY + radius * MathF.Sin(angle));
        }
        ctx.DrawPolyline(arc, _pullArc);
        if (!arrowHead) return;

        // A triangle on the arc's end, pointing along it (clockwise on screen)
        float end = start + sweep;
        float cos = MathF.Cos(end), sin = MathF.Sin(end);
        float endX = centerX + radius * cos, endY = centerY + radius * sin;
        const float length = 5f, halfWidth = 4f;
        Span<KumikoPoint> head = stackalloc KumikoPoint[3];
        head[0] = new KumikoPoint(endX - sin * length, endY + cos * length);
        head[1] = new KumikoPoint(endX + cos * halfWidth, endY + sin * halfWidth);
        head[2] = new KumikoPoint(endX - cos * halfWidth, endY - sin * halfWidth);
        ctx.FillPolygon(head, _pullHead);
    }
}
