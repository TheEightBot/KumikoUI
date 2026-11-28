using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>The look of the busy indicator the grid draws while rows load or a background rebuild runs (#327).</summary>
public sealed partial class KumikoStyle
{
    /// <summary>
    /// How the grid shows it is busy: <see cref="KumikoBusyIndicatorKind.Bar"/> (the default), a thin indeterminate bar
    /// under the header, or <see cref="KumikoBusyIndicatorKind.Overlay"/>, a scrim over the data area with a spinner.
    /// With <see cref="EnableAnimations"/> false the bar and the spinner are drawn still.
    /// </summary>
    public KumikoBusyIndicatorKind BusyIndicatorKind { get; set => Set(ref field, value); } = KumikoBusyIndicatorKind.Bar;

    /// <summary>
    /// Color of the busy bar's sweep and of the spinner. Default: the accent blue; the themes pick the accent that
    /// fits their background.
    /// </summary>
    public KumikoColor BusyIndicatorColor { get; set => Set(ref field, value); } = new(0, 120, 215);

    /// <summary>
    /// Color of the scrim the <see cref="KumikoBusyIndicatorKind.Overlay"/> kind draws over the data area; the
    /// <see cref="KumikoBusyIndicatorKind.Bar"/> kind ignores it. Default: white at 63% opacity (the Dark theme uses
    /// the dark background).
    /// </summary>
    public KumikoColor BusyOverlayColor { get; set => Set(ref field, value); } = new(255, 255, 255, 160);
}
