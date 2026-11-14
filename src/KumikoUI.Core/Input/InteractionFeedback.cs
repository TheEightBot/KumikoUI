using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// Something the grid draws hover and pressed feedback on (#101): a header, its filter icon, a grouping panel chip or
/// its remove button, a group header's chevron, or a control inside a cell (a check box, an action button).
/// </summary>
/// <param name="Region">
/// <see cref="HitRegion.HeaderCell"/>, <see cref="HitRegion.HeaderFilterIcon"/>, <see cref="HitRegion.GroupPanelChip"/>,
/// <see cref="HitRegion.GroupPanelChipRemove"/>, <see cref="HitRegion.GroupHeaderRow"/> (its chevron) or
/// <see cref="HitRegion.Cell"/>; <see cref="HitRegion.None"/> for nothing.
/// </param>
/// <param name="Row">The visible row of a group header or cell, else -1.</param>
/// <param name="Column">The column index of a header, filter icon or cell, else -1.</param>
/// <param name="Group">The group description index of a chip, else -1.</param>
/// <param name="Control">The control inside a cell (an action button's index; 0 for a check box), else -1.</param>
internal readonly record struct FeedbackTarget(HitRegion Region, int Row, int Column, int Group, int Control)
{
    /// <summary>No target.</summary>
    public static readonly FeedbackTarget None = new(HitRegion.None, -1, -1, -1, -1);

    /// <summary>Whether this is no target.</summary>
    public bool IsNone => Region == HitRegion.None;
}

/// <summary>
/// The pointer feedback the controller keeps for the renderer to draw (#101): what the mouse hovers, what is pressed
/// and whether the pointer is still over it, and where the pointer is (for controls inside a cell). The controller
/// asks for a redraw only when a target changes, not on every move. Targets that depend on the scroll position (headers,
/// group rows, cells) are drawn only while the grid has not scrolled since they were set.
/// </summary>
internal sealed class InteractionFeedback
{
    /// <summary>What a mouse or pen hovers; never set by a finger.</summary>
    public FeedbackTarget Hover { get; private set; } = FeedbackTarget.None;

    /// <summary>What the press in progress started on.</summary>
    public FeedbackTarget Pressed { get; private set; } = FeedbackTarget.None;

    /// <summary>Whether the pointer of the press in progress is still over <see cref="Pressed"/>.</summary>
    public bool IsPressedOver { get; private set; }

    /// <summary>The last pointer position, in viewport coordinates.</summary>
    public float PointerX { get; private set; } = float.NaN;

    /// <summary>The last pointer position, in viewport coordinates.</summary>
    public float PointerY { get; private set; } = float.NaN;

    // The scroll offsets when the targets were set
    private float _scrollX, _scrollY;

    /// <summary>The target drawn as pressed: <see cref="Pressed"/> while the pointer is over it.</summary>
    public FeedbackTarget ActivePressed => IsPressedOver ? Pressed : FeedbackTarget.None;

    /// <summary>Whether the targets were set at these scroll offsets (else row and column targets are stale).</summary>
    public bool IsCurrent(ScrollState scroll) =>
        MathF.Abs(scroll.OffsetX - _scrollX) < 0.5f && MathF.Abs(scroll.OffsetY - _scrollY) < 0.5f;

    /// <summary>Sets the hovered target; true when it changed (a redraw is needed).</summary>
    public bool SetHover(FeedbackTarget target, float x, float y, ScrollState scroll)
    {
        PointerX = x;
        PointerY = y;
        bool changed = Hover != target || (!target.IsNone && !IsCurrent(scroll));
        Hover = target;
        _scrollX = scroll.OffsetX;
        _scrollY = scroll.OffsetY;
        return changed;
    }

    /// <summary>Starts a press on <paramref name="target"/>; true when that changed what is drawn.</summary>
    public bool BeginPress(FeedbackTarget target, float x, float y, ScrollState scroll)
    {
        PointerX = x;
        PointerY = y;
        bool changed = ActivePressed != target;
        Pressed = target;
        IsPressedOver = !target.IsNone;
        _scrollX = scroll.OffsetX;
        _scrollY = scroll.OffsetY;
        return changed;
    }

    /// <summary>
    /// The press moved: it is drawn pressed while <paramref name="under"/> (the target under the pointer now) is the
    /// pressed one. True when that changed.
    /// </summary>
    public bool TrackPress(FeedbackTarget under, float x, float y)
    {
        PointerX = x;
        PointerY = y;
        bool over = !Pressed.IsNone && under == Pressed;
        if (over == IsPressedOver) return false;
        IsPressedOver = over;
        return true;
    }

    /// <summary>Ends the press feedback; true when something drawn pressed is no longer.</summary>
    public bool EndPress()
    {
        bool changed = IsPressedOver;
        Pressed = FeedbackTarget.None;
        IsPressedOver = false;
        return changed;
    }

    /// <summary>Clears the hover (the pointer left the grid); true when something was drawn hovered.</summary>
    public bool ClearHover()
    {
        bool changed = !Hover.IsNone;
        Hover = FeedbackTarget.None;
        return changed;
    }
}
