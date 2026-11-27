using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

/// <summary>The busy indicator (#327): drawn over the data area while <see cref="IsBusy"/> is true.</summary>
public sealed partial class KumikoRenderer
{
    /// <summary>How often the hosts repaint while an animated busy indicator shows, in milliseconds.</summary>
    internal const int BusyFrameIntervalMs = BusyIndicatorPainter.FrameIntervalMs;

    private readonly BusyIndicatorPainter _busyPainter = new();

    /// <summary>
    /// Whether the grid draws its busy indicator (<see cref="KumikoStyle.BusyIndicatorKind"/>) over the data area, below
    /// the header, after the rows, the sticky group headers and a pinned footer and before the editor and the popups.
    /// The indicator does not block input. The MAUI and Uno grids drive this from their <c>IsBusy</c> property and, by
    /// default, from a background rebuild that outlasts <c>BusyIndicatorDelay</c>; a custom host sets it itself. Default
    /// false.
    /// </summary>
    public bool IsBusy { get; set; }

    /// <summary>
    /// Whether the last frame drew an animated busy indicator. A host keeps requesting frames (about one every 33 ms)
    /// while this is true and stops as soon as it is false. It stays false when nothing is drawn and with reduced
    /// motion (<see cref="KumikoStyle.EnableAnimations"/> false), which draws the indicator still.
    /// </summary>
    public bool IsBusyIndicatorAnimating { get; private set; }

    /// <summary>The monotonic clock, in milliseconds, the busy indicator's phase is derived from. Tests replace it.</summary>
    internal Func<long> BusyClock { get; set; } = static () => Environment.TickCount64;

    /// <summary>Draws the busy indicator over the data area (in the frame's grouping-panel-translated space), when busy.</summary>
    private void DrawBusyIndicator(IDrawingContext ctx, KumikoStyle style, float viewportWidth, float dataAreaTop,
        float dataAreaBottom)
    {
        IsBusyIndicatorAnimating = false;
        if (!IsBusy || dataAreaBottom <= dataAreaTop) return;

        bool animate = _inputController?.CanAnimate(style) ?? style.EnableAnimations;
        _busyPainter.Draw(ctx, style, new KumikoRect(0, dataAreaTop, viewportWidth, dataAreaBottom - dataAreaTop),
            animate, animate ? BusyClock() : 0);
        IsBusyIndicatorAnimating = animate;
    }
}
