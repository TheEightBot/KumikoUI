namespace KumikoUI.Core.Models;

/// <summary>How the grid shows that it is busy (#327): the look of <see cref="KumikoStyle.BusyIndicatorKind"/>.</summary>
public enum KumikoBusyIndicatorKind
{
    /// <summary>
    /// A thin indeterminate bar at the top of the data area, below the header (and the top summaries). The rows stay
    /// readable and the grid stays usable underneath it.
    /// </summary>
    Bar = 0,

    /// <summary>
    /// A translucent scrim (<see cref="KumikoStyle.BusyOverlayColor"/>) over the data area with a spinner centered in
    /// it. The header, the grouping panel and a pinned footer stay visible.
    /// </summary>
    Overlay = 1,
}
