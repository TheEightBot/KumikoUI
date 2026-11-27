using KumikoUI.Core.Components;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// The one way the grid draws a check box: the border and the check or indeterminate mark through the style's
/// <see cref="KumikoStyle.IconProvider"/>. The Boolean cells and the selection column (cells and header) share it.
/// Allocation-free.
/// </summary>
internal static class CheckBoxGlyph
{
    /// <summary>The check box drawn in <paramref name="area"/>: at most 16 px square, centered.</summary>
    internal static KumikoRect BoxBounds(KumikoRect area)
    {
        float boxSize = Math.Min(16f, area.Height - 8);
        float centerY = area.Y + (area.Height - boxSize) / 2;
        float centerX = area.X + (area.Width - boxSize) / 2;
        return new KumikoRect(centerX, centerY, boxSize, boxSize);
    }

    /// <summary>Draws a box of the given state in <paramref name="box"/>.</summary>
    /// <param name="ctx">Drawing context.</param>
    /// <param name="box">The box rectangle.</param>
    /// <param name="state">What the box shows.</param>
    /// <param name="style">The grid style, for the icon provider.</param>
    /// <param name="paints">The grid's shared paints.</param>
    /// <param name="border">The border paint; null for the grid's check box border.</param>
    /// <param name="mark">The mark paint; null for the grid's check mark or indeterminate paint.</param>
    internal static void Draw(IDrawingContext ctx, KumikoRect box, DrawnCheckState state, KumikoStyle style,
        PaintCache paints, KumikoPaint? border = null, KumikoPaint? mark = null)
    {
        ctx.DrawRect(box, border ?? paints.CheckBoxBorder);
        if (state == DrawnCheckState.Indeterminate)
            style.IconProvider.DrawIcon(ctx, KumikoIcon.Indeterminate, box, mark ?? paints.CheckIndeterminate);
        else if (state == DrawnCheckState.Checked)
            style.IconProvider.DrawIcon(ctx, KumikoIcon.Check, box, mark ?? paints.CheckMark);
    }
}
