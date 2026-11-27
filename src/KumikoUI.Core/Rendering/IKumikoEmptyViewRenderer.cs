using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Draws the empty view in place of the built-in centered text (<see cref="KumikoRenderer.EmptyViewRenderer"/>, #326).
/// </summary>
/// <remarks>
/// <see cref="Render"/> runs on every frame while the view has no rows. To keep frames free of allocations, draw with
/// paints created once and reused; do not create a <see cref="KumikoPaint"/>, <see cref="KumikoFont"/> or string per
/// call (the same rule as <see cref="ICellRenderer"/>).
/// </remarks>
public interface IKumikoEmptyViewRenderer
{
    /// <summary>Draws the empty view.</summary>
    /// <param name="ctx">Drawing context.</param>
    /// <param name="bounds">The data area: below the header, grouping panel and top summaries, above a pinned footer, across the full viewport width.</param>
    /// <param name="kind">Why there are no rows.</param>
    /// <param name="text">The text the built-in view would draw for <paramref name="kind"/> (the host's empty view text when one is set), for reuse.</param>
    /// <param name="style">The grid's style.</param>
    void Render(IDrawingContext ctx, KumikoRect bounds, KumikoEmptyViewKind kind, string text, KumikoStyle style);
}
