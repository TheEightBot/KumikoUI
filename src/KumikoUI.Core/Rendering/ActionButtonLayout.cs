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

    /// <summary>
    /// The index of the button that contains (<paramref name="x"/>, <paramref name="y"/>), or -1, for the
    /// layout <see cref="ComputeButtonRects"/> computes. Allocation-free, for hover hit-testing.
    /// </summary>
    public static int IndexAt(
        KumikoRect bounds, int count, float padding, float spacing, float x, float y)
    {
        if (count == 0 || bounds.Width <= 0 || bounds.Height <= 0) return -1;

        // Same arithmetic as ComputeButtonRects
        float totalSpacing = spacing * (count - 1);
        float btnW = (bounds.Width - padding * 2 - totalSpacing) / count;
        float btnH = bounds.Height - padding * 2;
        float btnY = bounds.Y + padding;
        float left = bounds.X + padding;

        for (int i = 0; i < count; i++)
        {
            if (new KumikoRect(left, btnY, MathF.Max(0, btnW), MathF.Max(0, btnH)).Contains(x, y)) return i;
            left += btnW + spacing;
        }
        return -1;
    }
}
