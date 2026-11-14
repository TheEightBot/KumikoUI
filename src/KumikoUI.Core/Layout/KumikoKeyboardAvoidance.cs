using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Layout;

/// <summary>
/// The geometry of keeping an edited cell above an on-screen keyboard (#258), shared by every host: how much of the
/// grid the keyboard covers, and how far the grid must scroll to bring the edited cell above it. The hosts only
/// supply the keyboard's place and the cell's bounds in their own units; every value here is in the same logical
/// units (DIPs or points) and, for the vertical axis, in one coordinate space per call.
/// </summary>
/// <remarks>
/// A host reads the keyboard's top from the platform (UIKit's keyboard frame, Android's IME window insets, Uno's
/// <c>InputPane.OccludedRect</c>), calls <see cref="GetObscuredHeight"/> for the part of the grid it covers, gives
/// that as <see cref="Models.ScrollState.BottomInset"/> so the last rows can scroll above the keyboard, and then
/// scrolls by <see cref="GetScrollDelta"/>. When the keyboard hides or editing ends it sets the inset back to 0.
/// </remarks>
public static class KumikoKeyboardAvoidance
{
    /// <summary>The space kept between the edited cell and the keyboard's top edge, in logical units.</summary>
    public const float DefaultMargin = 4f;

    /// <summary>
    /// How much of the bottom of the grid an on-screen keyboard covers. A keyboard that does not reach the grid's
    /// bottom edge (a floating or split one, or one over another part of the screen) covers none of it, because the
    /// rows below such a keyboard stay visible and the grid cannot scroll them out from under it.
    /// </summary>
    /// <param name="gridTop">The grid's top edge in a coordinate space shared with the keyboard (the window's).</param>
    /// <param name="gridBottom">The grid's bottom edge in the same space.</param>
    /// <param name="keyboardTop">The keyboard's top edge in the same space.</param>
    /// <param name="keyboardBottom">The keyboard's bottom edge in the same space; use
    /// <see cref="float.PositiveInfinity"/> when the keyboard extends to the end of the screen.</param>
    /// <returns>The covered height from the grid's bottom edge up, between 0 and the grid's height.</returns>
    public static float GetObscuredHeight(float gridTop, float gridBottom, float keyboardTop, float keyboardBottom)
    {
        if (float.IsNaN(gridTop) || float.IsNaN(gridBottom) || float.IsNaN(keyboardTop) || float.IsNaN(keyboardBottom))
            return 0;
        float height = gridBottom - gridTop;
        if (height <= 0 || keyboardBottom < gridBottom || keyboardTop >= gridBottom) return 0;
        return Math.Clamp(gridBottom - Math.Max(keyboardTop, gridTop), 0, height);
    }

    /// <summary>
    /// How far to scroll down so the bottom edge of the edited cell sits above the keyboard, without pushing the
    /// cell's top above <paramref name="minTop"/> (a cell taller than the visible area keeps its top in view) and
    /// without scrolling past the end of the content.
    /// </summary>
    /// <param name="viewportHeight">The grid's height.</param>
    /// <param name="obscuredHeight">The height the keyboard covers (<see cref="GetObscuredHeight"/>).</param>
    /// <param name="targetTop">The edited cell's top edge in viewport coordinates.</param>
    /// <param name="targetBottom">The edited cell's bottom edge in viewport coordinates.</param>
    /// <param name="offsetY">The current vertical scroll offset.</param>
    /// <param name="maxOffsetY">The largest vertical scroll offset, counting any
    /// <see cref="Models.ScrollState.BottomInset"/> the host applies for this keyboard.</param>
    /// <param name="minTop">The lowest top edge the cell may be scrolled to (the bottom of the header and any frozen
    /// rows); 0 for none.</param>
    /// <param name="margin">The space kept between the cell and the keyboard.</param>
    /// <returns>The amount to add to the scroll offset: 0 when the cell is already clear of the keyboard.</returns>
    public static float GetScrollDelta(
        float viewportHeight, float obscuredHeight, float targetTop, float targetBottom,
        float offsetY, float maxOffsetY, float minTop = 0, float margin = DefaultMargin)
    {
        if (obscuredHeight <= 0 || viewportHeight <= 0) return 0;
        float visibleBottom = viewportHeight - obscuredHeight;
        if (visibleBottom <= 0) return 0;

        float needed = targetBottom + margin - visibleBottom;
        if (needed <= 0) return 0;

        float room = Math.Max(0, maxOffsetY - offsetY);
        float keepTop = Math.Max(0, targetTop - minTop);
        return Math.Min(needed, Math.Min(room, keepTop));
    }

    /// <summary>
    /// Scrolls the grid so the open cell editor sits above a keyboard covering <paramref name="obscuredHeight"/> of
    /// the grid's bottom, and moves the editor's bounds with it so no frame draws it in the old place. A cell in a
    /// frozen row does not scroll, so it is left alone. Does nothing when no editor is open and visible.
    /// </summary>
    /// <param name="scroll">The scroll state to move.</param>
    /// <param name="source">The rows, for the frozen row count and the top of the scrolling rows.</param>
    /// <param name="style">Row and header heights.</param>
    /// <param name="edit">The edit session whose editor is kept visible.</param>
    /// <param name="obscuredHeight">The height the keyboard covers (<see cref="GetObscuredHeight"/>).</param>
    /// <param name="applyInset">Whether to set <see cref="Models.ScrollState.BottomInset"/> to
    /// <paramref name="obscuredHeight"/> first, so the last rows can scroll above the keyboard. A host whose window
    /// already shrinks for the keyboard passes false and leaves the inset alone.</param>
    /// <param name="margin">The space kept between the cell and the keyboard.</param>
    /// <returns>How far the grid scrolled down, 0 when it did not move.</returns>
    public static float ScrollEditorIntoView(
        ScrollState scroll, KumikoSource source, KumikoStyle style, EditSession edit,
        float obscuredHeight, bool applyInset, float margin = DefaultMargin)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(edit);
        if (applyInset) scroll.BottomInset = obscuredHeight;
        if (edit is not { IsEditing: true, ActiveEditor: { IsVisible: true } editor }) return 0;

        // A frozen row does not scroll
        int frozen = source.EffectiveFrozenRowCount;
        if (edit.EditCell.Row < frozen) return 0;

        // The scrolling rows start below the header, summaries and frozen rows: a tall cell keeps its top there
        float minTop = KumikoScroller.GetRowY(source, style, scroll, frozen) + scroll.OffsetY;
        var bounds = editor.Bounds;
        float delta = GetScrollDelta(scroll.ViewportHeight, obscuredHeight, bounds.Y, bounds.Bottom,
            scroll.OffsetY, scroll.MaxOffsetY, minTop, margin);
        if (delta <= 0) return 0;

        float before = scroll.OffsetY;
        scroll.ScrollTo(scroll.OffsetX, before + delta);
        float moved = scroll.OffsetY - before;
        if (moved > 0)
            editor.Bounds = new KumikoRect(bounds.X, bounds.Y - moved, bounds.Width, bounds.Height);
        return moved;
    }
}
