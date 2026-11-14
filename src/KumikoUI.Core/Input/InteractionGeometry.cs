using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>
/// Where the renderer draws the grid's small interactive glyphs (the header filter icon, a group header's chevron,
/// the grouping panel's chips) and how far around them a press still counts, for the hit tester (#101). The renderer
/// draws from the same numbers, so a target is exactly where its glyph is. Allocation-free.
/// </summary>
internal static class InteractionGeometry
{
    /// <summary>Side of the header filter icon as drawn.</summary>
    public const float FilterIconSize = 10f;

    /// <summary>Margin around the filter icon that still opens the filter, for a mouse or pen.</summary>
    public const float FilterIconMouseSlop = 5f;

    /// <summary>Margin around the filter icon that still opens the filter, for a finger (a 32 px target).</summary>
    public const float FilterIconTouchSlop = 11f;

    /// <summary>
    /// Half of the column resize grip (see <see cref="KumikoStyle.ColumnResizeGripWidth"/>) for
    /// <paramref name="device"/>, at most a quarter of the column's width (at least 3 px) so a press in the middle of
    /// a narrow header still sorts.
    /// </summary>
    public static float ResizeGripHalfWidth(KumikoColumn column, KumikoStyle style, KumikoPointerDeviceType device) =>
        ResizeGripReach(column.Width, style, device);

    /// <summary>
    /// How far the resize grip reaches into a column <paramref name="columnWidth"/> wide on one side of the edge: half
    /// the grip for <paramref name="device"/>, at most a quarter of that column (at least 3 px). The grip reaches into
    /// the column on its right by that column's width, so a narrow neighbor keeps its middle too.
    /// </summary>
    public static float ResizeGripReach(float columnWidth, KumikoStyle style, KumikoPointerDeviceType device)
    {
        float width = device == KumikoPointerDeviceType.Touch ? style.TouchColumnResizeGripWidth : style.ColumnResizeGripWidth;
        float half = Math.Max(0f, width) / 2f;
        return Math.Min(half, Math.Max(3f, columnWidth / 4f));
    }

    /// <summary>Space the header's right edge keeps for the sort arrow when the column is sorted.</summary>
    public static float SortGlyphSpace(KumikoColumn column) =>
        column.SortDirection != KumikoSortDirection.None ? KumikoLayoutEngine.SortGlyphSpace : 0f;

    /// <summary>The left edge of the filter icon, measured from the column's left edge: right of the header text, left of the sort arrow.</summary>
    public static float FilterIconOffset(KumikoColumn column, KumikoStyle style) =>
        column.Width - style.CellPadding - SortGlyphSpace(column) - FilterIconSize;

    /// <summary>The filter icon as drawn in a header whose column starts at <paramref name="columnLeft"/>.</summary>
    public static KumikoRect FilterIconRect(float columnLeft, KumikoColumn column, KumikoStyle style)
    {
        float x = columnLeft + FilterIconOffset(column, style);
        return new KumikoRect(x, style.HeaderHeight / 2 - FilterIconSize / 2, FilterIconSize, FilterIconSize);
    }

    /// <summary>
    /// Whether <paramref name="x"/> (in the same coordinates as <paramref name="columnLeft"/>) is on the filter icon as
    /// drawn. The drawn icon wins over a resize grip that reaches over it (a finger's grip is wider than the icon's
    /// distance from the column edge).
    /// </summary>
    public static bool IsOnDrawnFilterIcon(float x, float columnLeft, KumikoColumn column, KumikoStyle style)
    {
        float iconLeft = columnLeft + FilterIconOffset(column, style);
        return x >= iconLeft && x <= iconLeft + FilterIconSize;
    }

    /// <summary>
    /// Whether <paramref name="x"/> (in the same coordinates as <paramref name="columnLeft"/>) is on the filter icon's
    /// target: the icon plus a margin for the device, kept inside the column and left of its resize grip.
    /// </summary>
    public static bool IsOnFilterIcon(float x, float columnLeft, KumikoColumn column, KumikoStyle style,
        KumikoPointerDeviceType device, float gripHalfWidth)
    {
        float slop = device == KumikoPointerDeviceType.Touch ? FilterIconTouchSlop : FilterIconMouseSlop;
        float iconLeft = columnLeft + FilterIconOffset(column, style);
        float left = Math.Max(columnLeft, iconLeft - slop);
        float right = Math.Min(columnLeft + column.Width - gripHalfWidth, iconLeft + FilterIconSize + slop);
        return x >= left && x <= right;
    }

    // ── Group header chevron ──

    /// <summary>The chevron's pill (its background) on a group header row of level <paramref name="level"/> at <paramref name="rowY"/>.</summary>
    public static KumikoRect GroupChevronPill(float rowY, int level, KumikoStyle style, float handleOffset)
    {
        float chevronX = GroupChevronX(level, style, handleOffset);
        float size = style.GroupChevronSize;
        float centerY = rowY + style.RowHeight / 2;
        return new KumikoRect(chevronX - 4f, centerY - (size + 6f) / 2, size + 8f, size + 6f);
    }

    /// <summary>The left edge of the chevron glyph on a group header of level <paramref name="level"/>.</summary>
    public static float GroupChevronX(int level, KumikoStyle style, float handleOffset) =>
        handleOffset + style.CellPadding + level * style.GroupIndentWidth;

    /// <summary>
    /// Whether viewport <paramref name="x"/> is on a group header's chevron: its pill plus the cell padding after
    /// it (a touch gets twice that), over the whole row height.
    /// </summary>
    public static bool IsOnGroupChevron(float x, int level, KumikoStyle style, float handleOffset, KumikoPointerDeviceType device)
    {
        float chevronX = GroupChevronX(level, style, handleOffset);
        float slop = device == KumikoPointerDeviceType.Touch ? 2 * style.CellPadding : style.CellPadding;
        return x >= chevronX - 4f - (device == KumikoPointerDeviceType.Touch ? style.CellPadding : 0f)
            && x <= chevronX + style.GroupChevronSize + slop;
    }

    // ── Grouping panel chips ──

    /// <summary>Height of a grouping panel chip.</summary>
    public const float ChipHeight = 26f;
    /// <summary>Padding inside a chip, left of its text and around its buttons.</summary>
    public const float ChipPadding = 10f;
    /// <summary>Gap between chips (and around the separator between them).</summary>
    public const float ChipGap = 4f;
    /// <summary>Width of a chip's left accent bar.</summary>
    public const float ChipAccentWidth = 4f;
    /// <summary>Width of a chip's sort direction arrow.</summary>
    public const float ChipSortArrowWidth = 16f;
    /// <summary>Width of a chip's remove button.</summary>
    public const float ChipRemoveButtonSize = 16f;
    /// <summary>Width of the separator glyph between two chips.</summary>
    public const float ChipSeparatorWidth = 14f;
    /// <summary>Corner radius of a chip.</summary>
    public const float ChipCornerRadius = 6f;

    /// <summary>The x of the first chip, after the panel's label of width <paramref name="labelWidth"/>.</summary>
    public static float FirstChipX(float labelWidth, KumikoStyle style) => style.CellPadding + 2 + labelWidth + 10;

    /// <summary>A chip's top in a panel of height <paramref name="panelHeight"/>.</summary>
    public static float ChipY(float panelHeight) => (panelHeight - ChipHeight) / 2;

    /// <summary>A chip's width for a text <paramref name="textWidth"/> wide.</summary>
    public static float ChipWidth(float textWidth) =>
        ChipAccentWidth + ChipPadding + textWidth + 4 + ChipSortArrowWidth + ChipPadding + ChipRemoveButtonSize + ChipPadding / 2;

    /// <summary>The x of the chip after one at <paramref name="chipX"/>, past the separator between them.</summary>
    public static float NextChipX(float chipX, float chipWidth) => chipX + chipWidth + ChipGap + ChipSeparatorWidth + ChipGap;

    /// <summary>The part of <paramref name="chip"/> that removes the grouping: its remove button and the padding around it.</summary>
    public static KumikoRect ChipRemoveRect(KumikoRect chip)
    {
        float left = chip.Right - ChipRemoveButtonSize - ChipPadding / 2 - 4;
        return new KumikoRect(left, chip.Y, chip.Right - left, chip.Height);
    }

    /// <summary>The center of a chip's remove button (its circle and glyph).</summary>
    public static KumikoPoint ChipRemoveCenter(KumikoRect chip) =>
        new(chip.Right - ChipRemoveButtonSize - ChipPadding / 2 + ChipRemoveButtonSize / 2, chip.Y + chip.Height / 2);

    /// <summary>
    /// A chip's press target: the chip widened by half the gap on each side; for a finger also as tall as the panel.
    /// </summary>
    public static KumikoRect ChipTarget(KumikoRect chip, float panelHeight, KumikoPointerDeviceType device)
    {
        float top = device == KumikoPointerDeviceType.Touch ? 0 : chip.Y;
        float bottom = device == KumikoPointerDeviceType.Touch ? panelHeight : chip.Bottom;
        return new KumikoRect(chip.X - ChipGap / 2, top, chip.Width + ChipGap, bottom - top);
    }
}
