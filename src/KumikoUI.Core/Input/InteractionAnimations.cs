using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// The short transitions the grid plays after an interaction (#101): a group header's rows sliding open or shut, a
/// dragged column header lifting and settling into its slot, and the current row's highlight fading in; and the clock
/// that shows an <see cref="KumikoScrollBarVisibility.Auto"/> scroll bar for a while after the grid scrolled, then fades
/// it (#316). The controller
/// starts them; the renderer reads their progress; the host's frame loop advances them through
/// <see cref="KumikoInputController.UpdateAnimations"/>. Nothing here allocates once constructed.
/// </summary>
internal sealed class InteractionAnimations
{
    /// <summary>Duration of a group expanding or collapsing, in milliseconds.</summary>
    public const float GroupToggleMs = 200f;

    /// <summary>Duration of a dragged column header lifting off the header row, in milliseconds.</summary>
    public const float ColumnLiftMs = 120f;

    /// <summary>Duration of a dropped column header settling into its slot, in milliseconds.</summary>
    public const float ColumnDropMs = 180f;

    /// <summary>Duration of the current row's highlight fading in, in milliseconds.</summary>
    public const float SelectionFadeMs = 120f;

    /// <summary>How long an <see cref="KumikoScrollBarVisibility.Auto"/> scroll bar stays fully shown after the grid last scrolled or the pointer left it, in milliseconds.</summary>
    public const float ScrollBarHoldMs = 1000f;

    /// <summary>How long an <see cref="KumikoScrollBarVisibility.Auto"/> scroll bar takes to fade out after the hold, in milliseconds.</summary>
    public const float ScrollBarFadeMs = 250f;

    // The animation clock: milliseconds advanced by Update. Each transition records when it started.
    private float _now;
    private float _groupStart = float.NaN, _liftStart = float.NaN, _dropStart = float.NaN, _selectionStart = float.NaN;
    private float _verticalBarStart = float.NaN, _horizontalBarStart = float.NaN;

    /// <summary>The group header row whose rows are sliding (the rows after it move), or -1.</summary>
    public int GroupAnchorRow { get; private set; } = -1;

    /// <summary>The number of rows the toggle added (positive: expanded) or removed (negative: collapsed).</summary>
    public int GroupRowDelta { get; private set; }

    /// <summary>The column whose header is settling after a drop, or null.</summary>
    public KumikoColumn? DropColumn { get; private set; }

    /// <summary>The viewport x the dropped header leaves from.</summary>
    public float DropFromX { get; private set; }

    /// <summary>The viewport x the dropped header settles at (its slot's left edge).</summary>
    public float DropToX { get; private set; }

    /// <summary>The item whose row highlight is fading in, or null.</summary>
    public object? SelectionItem { get; private set; }

    /// <summary>Whether any transition is still running.</summary>
    public bool IsActive => IsTransitioning || !float.IsNaN(_verticalBarStart) || !float.IsNaN(_horizontalBarStart);

    /// <summary>Whether a transition other than a scroll bar's hold and fade is running.</summary>
    private bool IsTransitioning => !float.IsNaN(_groupStart) || !float.IsNaN(_liftStart) || !float.IsNaN(_dropStart) ||
                                    !float.IsNaN(_selectionStart);

    /// <summary>
    /// Whether the last <see cref="Update"/> changed what is drawn: a transition moved, or a scroll bar started or went
    /// on fading. A bar held fully shown changes nothing, so its hold needs no redraw.
    /// </summary>
    public bool ChangedVisibly { get; private set; }

    /// <summary>The group slide's eased progress (0 just toggled, 1 done), or 1 when none is running.</summary>
    public float GroupProgress => Progress(_groupStart, GroupToggleMs);

    /// <summary>The lift's eased progress (0 just picked up, 1 lifted).</summary>
    public float LiftProgress => Progress(_liftStart, ColumnLiftMs);

    /// <summary>The drop's eased progress (0 just released, 1 settled).</summary>
    public float DropProgress => Progress(_dropStart, ColumnDropMs);

    /// <summary>The highlight's eased progress (0 just selected, 1 fully shown).</summary>
    public float SelectionProgress => Progress(_selectionStart, SelectionFadeMs);

    /// <summary>Whether the group slide is running.</summary>
    public bool IsGroupSliding => !float.IsNaN(_groupStart);

    /// <summary>Whether a dropped header is settling.</summary>
    public bool IsDropping => !float.IsNaN(_dropStart) && DropColumn != null;

    /// <summary>Whether the current row's highlight is fading in.</summary>
    public bool IsSelectionFading => !float.IsNaN(_selectionStart) && SelectionItem != null;

    /// <summary>The rows after <paramref name="headerRow"/> slide open (or shut) by <paramref name="rowDelta"/> rows.</summary>
    public void StartGroupToggle(int headerRow, int rowDelta)
    {
        if (rowDelta == 0) return;
        GroupAnchorRow = headerRow;
        GroupRowDelta = rowDelta;
        _groupStart = _now;
    }

    /// <summary>A column header was picked up.</summary>
    public void StartLift()
    {
        _liftStart = _now;
        StopDrop();
    }

    /// <summary>A dropped header settles from <paramref name="fromX"/> to <paramref name="toX"/>.</summary>
    public void StartDrop(KumikoColumn column, float fromX, float toX)
    {
        _liftStart = float.NaN;
        if (!float.IsFinite(fromX) || !float.IsFinite(toX)) return;
        DropColumn = column;
        DropFromX = fromX;
        DropToX = toX;
        _dropStart = _now;
    }

    /// <summary>The current row moved to <paramref name="item"/>'s row: its highlight fades in.</summary>
    public void StartSelection(object item)
    {
        SelectionItem = item;
        _selectionStart = _now;
    }

    /// <summary>
    /// The grid scrolled (or the pointer left a bar): the vertical (<paramref name="vertical"/> true) or horizontal bar
    /// shows fully for <see cref="ScrollBarHoldMs"/>, then fades out over <see cref="ScrollBarFadeMs"/>. Calling it
    /// again restarts the hold.
    /// </summary>
    public void ShowScrollBar(bool vertical)
    {
        if (vertical) _verticalBarStart = _now;
        else _horizontalBarStart = _now;
    }

    /// <summary>
    /// How visible a bar is on its clock, from 1 (shown) to 0 (hidden). With <paramref name="fade"/> false (animations
    /// off) the bar shows for the hold and then hides at once.
    /// </summary>
    public float ScrollBarOpacity(bool vertical, bool fade)
    {
        float start = vertical ? _verticalBarStart : _horizontalBarStart;
        if (float.IsNaN(start)) return 0f;
        float elapsed = _now - start;
        if (elapsed < ScrollBarHoldMs) return 1f;
        if (!fade) return 0f;
        return Math.Clamp(1f - (elapsed - ScrollBarHoldMs) / ScrollBarFadeMs, 0f, 1f);
    }

    /// <summary>Advances the clock by <paramref name="frameIntervalMs"/> and ends the transitions that finished.</summary>
    /// <returns>Whether any transition is still running.</returns>
    public bool Update(float frameIntervalMs)
    {
        ChangedVisibly = false;
        if (!IsActive) return false;
        bool transitioning = IsTransitioning;
        float verticalBar = ScrollBarOpacity(vertical: true, fade: true);
        float horizontalBar = ScrollBarOpacity(vertical: false, fade: true);
        if (float.IsFinite(frameIntervalMs) && frameIntervalMs > 0)
            _now += frameIntervalMs; // a stalled frame ends what it overran

        if (Done(_groupStart, GroupToggleMs)) { _groupStart = float.NaN; GroupAnchorRow = -1; GroupRowDelta = 0; }
        if (Done(_liftStart, ColumnLiftMs)) _liftStart = float.NaN;
        if (Done(_dropStart, ColumnDropMs)) StopDrop();
        if (Done(_selectionStart, SelectionFadeMs)) { _selectionStart = float.NaN; SelectionItem = null; }
        if (Done(_verticalBarStart, ScrollBarHoldMs + ScrollBarFadeMs)) _verticalBarStart = float.NaN;
        if (Done(_horizontalBarStart, ScrollBarHoldMs + ScrollBarFadeMs)) _horizontalBarStart = float.NaN;

        ChangedVisibly = transitioning
            || verticalBar != ScrollBarOpacity(vertical: true, fade: true)
            || horizontalBar != ScrollBarOpacity(vertical: false, fade: true);
        return IsActive;
    }

    /// <summary>Ends every transition at once (the grid was disabled, or animations were turned off).</summary>
    public void StopAll()
    {
        _groupStart = _liftStart = _selectionStart = float.NaN;
        _verticalBarStart = _horizontalBarStart = float.NaN;
        GroupAnchorRow = -1;
        GroupRowDelta = 0;
        SelectionItem = null;
        StopDrop();
    }

    private void StopDrop()
    {
        _dropStart = float.NaN;
        DropColumn = null;
    }

    private bool Done(float start, float duration) => !float.IsNaN(start) && _now - start >= duration;

    /// <summary>Ease-out cubic progress of a transition that started at <paramref name="start"/>; 1 when not running.</summary>
    private float Progress(float start, float duration)
    {
        if (float.IsNaN(start)) return 1f;
        float t = Math.Clamp((_now - start) / duration, 0f, 1f);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }
}
