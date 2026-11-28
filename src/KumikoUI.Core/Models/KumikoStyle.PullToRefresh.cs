using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>The look of the pull-to-refresh indicator (#330).</summary>
public sealed partial class KumikoStyle
{
    /// <summary>
    /// Color of the pull-to-refresh indicator's arrow and spinner. <c>null</c> (the default) uses
    /// <see cref="AccentColor"/>, so it follows the theme.
    /// </summary>
    public KumikoColor? RefreshIndicatorColor { get; set => Set(ref field, value); }

    /// <summary>
    /// Fill of the pull-to-refresh indicator's disc, drawn over the rows below the header. Default: white; the Dark
    /// and High Contrast themes set their own.
    /// </summary>
    public KumikoColor RefreshIndicatorBackgroundColor { get; set => Set(ref field, value); } = KumikoColor.White;
}
