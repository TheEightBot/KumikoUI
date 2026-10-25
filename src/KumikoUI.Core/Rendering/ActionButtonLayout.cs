using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Shared layout computation for action-button cells, usable by both
/// <see cref="ActionButtonsCellRenderer"/> (display) and
/// <see cref="KumikoUI.Core.Components.DrawnActionButtons"/> (interactive editor).
/// </summary>
public static class ActionButtonLayout
{
    /// <summary>
    /// Computes equal-width button rects for <paramref name="count"/> buttons
    /// within <paramref name="bounds"/>, applying <paramref name="padding"/> inset
    /// and <paramref name="spacing"/> between buttons.
    /// </summary>
    public static IReadOnlyList<KumikoRect> ComputeButtonRects(
        KumikoRect bounds, int count, float padding, float spacing)
    {
        var rects = new List<KumikoRect>(count);
        if (count == 0 || bounds.Width <= 0 || bounds.Height <= 0)
            return rects;

        float totalSpacing = spacing * (count - 1);
        float btnW = (bounds.Width - padding * 2 - totalSpacing) / count;
        float btnH = bounds.Height - padding * 2;
        float btnY = bounds.Y + padding;
        float x = bounds.X + padding;

        for (int i = 0; i < count; i++)
        {
            rects.Add(new KumikoRect(x, btnY, MathF.Max(0, btnW), MathF.Max(0, btnH)));
            x += btnW + spacing;
        }
        return rects;
    }
}
