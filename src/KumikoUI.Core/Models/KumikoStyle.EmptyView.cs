using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>The look of the empty view, the message drawn in the data area when the view has no rows (#326).</summary>
public sealed partial class KumikoStyle
{
    /// <summary>
    /// Whether the grid draws an empty view (a centered message, see <see cref="KumikoStrings.EmptyViewNoItems"/> and
    /// <see cref="KumikoStrings.EmptyViewNoMatches"/>) in the data area while the view has no rows. Default: true.
    /// </summary>
    public bool ShowEmptyView { get; set => Set(ref field, value); } = true;

    /// <summary>Color of the empty view's text. Default: a muted gray; each built-in theme sets its own.</summary>
    public KumikoColor EmptyViewTextColor { get; set => Set(ref field, value); } = new(110, 110, 110);

    /// <summary>Font of the empty view's text. <c>null</c> (the default) uses <see cref="CellFont"/>.</summary>
    public KumikoFont? EmptyViewFont { get; set => Set(ref field, value); }
}
