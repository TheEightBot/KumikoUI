using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>
/// Swipe row actions (#329): a finger drags a data row sideways to reveal drawn items beside it (leading items at the
/// start edge, trailing items at the end edge), the row opens or shuts when the finger lifts, a tap on an item runs it,
/// and a full swipe runs the outermost item. State and geometry live in <see cref="RowSwipe"/>, which the hit tester
/// tests first; the renderer draws it (<c>KumikoRenderer.Swipe.cs</c>). <see cref="HandleSwipePointer"/> is the one
/// hook in the pointer pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Gesture arbitration: only a finger swipes (a mouse or pen drag on a cell reorders rows or selects, as before). Once a
/// touch press on a data cell moves past the pan threshold, a horizontal-dominant move (more than
/// <see cref="RowSwipe.DirectionRatio"/> times the vertical one) toward a side that has items, with
/// <see cref="AllowSwiping"/> on and <see cref="SwipeStarting"/> not canceled, swipes the row; any other move pans as
/// before. So with swiping on, a grid wider than its viewport scrolls sideways from the header row, by a vertical-first or
/// diagonal pan, by the wheel or trackpad and by the scroll bars. A long press before the move still starts a row drag
/// or asks for the context menu.
/// </para>
/// <para>
/// While a row is open, a press anywhere else closes it (and acts as usual); a tap on the open row's cells only closes
/// it; a horizontal drag on them moves it on; a scroll closes it.
/// </para>
/// </remarks>
public sealed partial class KumikoInputController
{
    /// <summary>The width of each swipe item when <see cref="SwipeItemWidth"/> is not set (or not positive).</summary>
    internal const float DefaultSwipeItemWidth = 72f;

    // How long a finger may rest before lifting for the release to still fling, in milliseconds.
    private const long SwipeFlingStaleMs = 100;

    private float _swipeItemWidth = DefaultSwipeItemWidth;
    private bool _allowSwiping;

    // The press in progress may still become a swipe (a touch press on a data cell, not decided yet).
    private bool _swipeCandidate;

    // The press in progress landed on the open row (its cells or one of its items): a tap closes it.
    private bool _swipePressOnOpenRow;

    // The swipe item the press in progress holds (it runs when released on it), or -1.
    private int _swipePressItem = -1;

    // Tracking: the pointer x the offset is measured from, and the pointer's last x, time and speed (pixels per ms).
    private float _swipeGrabX;
    private float _swipeLastX;
    private long _swipeLastMs;
    private float _swipeVelocity;

    /// <summary>The swiped row, shared with the hit tester.</summary>
    internal RowSwipe Swipe => _hitTester.Swipe;

    /// <summary>
    /// The items a swipe toward the end edge reveals at the start edge of a row (at the left in a left-to-right
    /// layout), outermost first: item 0 sits against the edge, and a full swipe runs it. Items are drawn buttons
    /// (<see cref="ActionButtonDefinition"/>, or the hosts' bindable definitions); a tap runs the item's
    /// <see cref="IActionButtonDefinition.Command"/> (with the row's item as the parameter unless
    /// <see cref="IActionButtonDefinition.CommandParameter"/> is set) and then its <see cref="IActionButtonDefinition.Action"/>.
    /// An item whose command cannot execute is drawn disabled and does nothing. No items on a side: the row does not
    /// swipe that way.
    /// </summary>
    /// <remarks>
    /// Swipes are a touch shortcut: a keyboard, a mouse and a screen reader cannot reach them, so offer every swipe
    /// action another way too (an action-button column, a context or row menu, a toolbar acting on the selection).
    /// </remarks>
    public IList<IActionButtonDefinition> LeadingSwipeItems => Swipe.Leading;

    /// <summary>
    /// The items a swipe toward the start edge reveals at the end edge of a row (at the right in a left-to-right
    /// layout), outermost first: item 0 sits against the edge, and a full swipe runs it. See
    /// <see cref="LeadingSwipeItems"/>.
    /// </summary>
    public IList<IActionButtonDefinition> TrailingSwipeItems => Swipe.Trailing;

    /// <summary>
    /// Whether a finger can swipe a data row sideways to reveal <see cref="LeadingSwipeItems"/> or
    /// <see cref="TrailingSwipeItems"/>. A horizontal-dominant touch drag on a data cell then swipes instead of panning,
    /// so a grid wider than its viewport scrolls sideways from the header row, by a vertical-first or diagonal pan, by
    /// the wheel or trackpad and by the scroll bars. A mouse or pen never swipes. Turning it off closes an open row.
    /// Default: false.
    /// </summary>
    public bool AllowSwiping
    {
        get => _allowSwiping;
        set
        {
            if (_allowSwiping == value) return;
            _allowSwiping = value;
            if (!value) CloseSwipe();
        }
    }

    /// <summary>
    /// The width of each swipe item, in pixels: an open row moves aside by the number of its side's items times this
    /// (at most the row's width). A value that is not positive resets it to 72. Default: 72.
    /// </summary>
    public float SwipeItemWidth
    {
        get => _swipeItemWidth;
        set => _swipeItemWidth = float.IsNaN(value) || value <= 0 ? DefaultSwipeItemWidth : value;
    }

    /// <summary>
    /// Which sides run their outermost item (item 0) on a full swipe: dragging the row past 60% of its width (a haptic,
    /// <see cref="KumikoHapticKind.SwipeThreshold"/>, marks the point) and lifting the finger. Default:
    /// <see cref="KumikoFullSwipeMode.None"/>.
    /// </summary>
    public KumikoFullSwipeMode FullSwipeMode { get; set; }

    /// <summary>
    /// Raised when a finger starts to swipe a row, before the row moves: set
    /// <see cref="KumikoSwipeStartingEventArgs.Cancel"/> to keep that row from swiping (the finger pans instead). An open
    /// edit is committed first; a refused commit keeps the editor, and the finger pans.
    /// </summary>
    public event EventHandler<KumikoSwipeStartingEventArgs>? SwipeStarting;

    /// <summary>
    /// Raised once when a swiped row closes again: after one of its items ran
    /// (<see cref="KumikoSwipeEndedEventArgs.InvokedItem"/>, raised after the item's command), or without running one
    /// (released short of opening, a tap elsewhere, a scroll, a change of the rows, <see cref="CloseSwipe"/>). Not raised
    /// when the row opens.
    /// </summary>
    public event EventHandler<KumikoSwipeEndedEventArgs>? SwipeEnded;

    /// <summary>
    /// Closes the swiped row without running anything (it slides shut when animations are on) and raises
    /// <see cref="SwipeEnded"/>. Does nothing when no row is swiped.
    /// </summary>
    public void CloseSwipe()
    {
        if (!Swipe.IsActive) return;
        _swipeCandidate = false;
        _swipePressOnOpenRow = false;
        _swipePressItem = -1;
        EndSwipe(null);
    }

    /// <summary>
    /// The swiped row to draw this frame and its offset (a snap in progress included), or false when no row is swiped
    /// or the view changed since it was (the controller closes it at its next event).
    /// </summary>
    internal bool TryGetSwipeFrame(KumikoSource dataSource, out int row, out float offset, out RowSwipe swipe)
    {
        swipe = Swipe;
        row = -1;
        offset = 0;
        if (!swipe.IsShown(_animations) || !swipe.IsCurrent(dataSource)) return false;
        offset = swipe.CurrentOffset(_animations);
        if (MathF.Abs(offset) < 0.5f) return false;
        row = swipe.Row;
        return true;
    }

    /// <summary>
    /// The swipe's part of a pointer event, before the rest of the pipeline (<see cref="HandlePointer"/>): returns true
    /// when the swipe consumed the event. A press is never consumed (it is recorded, then pressed as usual).
    /// </summary>
    private bool HandleSwipePointer(KumikoPointerEventArgs e, InputAction action, ScrollState scroll,
        KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        var swipe = Swipe;
        if (!_allowSwiping && swipe.Phase == SwipePhase.None) return false;

        // A change of the rows (a sort, a filter, rows added or removed) closes the row, without a transition
        if (swipe.IsActive && !swipe.IsCurrent(dataSource)) AbortSwipe();

        switch (action)
        {
            case InputAction.Pressed:
                OnSwipePress(e, scroll, style, dataSource);
                return false;
            case InputAction.Moved:
                return OnSwipeMove(e, scroll, selection, style, dataSource);
            case InputAction.Released:
                return OnSwipeRelease(e, scroll, style, dataSource, cancelled: false);
            case InputAction.Cancelled:
                return OnSwipeRelease(e, scroll, style, dataSource, cancelled: true);
            case InputAction.LongPress:
                return OnSwipeLongPress(e);
            case InputAction.Scroll:
                CloseSwipe(); // the wheel or a trackpad scrolls: the open row closes
                return false;
            default:
                return false;
        }
    }

    private void OnSwipePress(KumikoPointerEventArgs e, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        _swipeCandidate = false;
        _swipePressOnOpenRow = false;
        _swipePressItem = -1;
        var swipe = Swipe;
        if (swipe.Phase == SwipePhase.Tracking) SettleSwipe(cancelled: true); // a press whose release was lost

        var hit = _hitTester.HitTestCore(e.X, e.Y, scroll, style, dataSource.Columns, dataSource.RowCount, dataSource,
            e.DeviceType);
        if (swipe.Phase == SwipePhase.Open)
        {
            bool onOpenRow = hit.Region == HitRegion.SwipeItem ||
                             (hit.RowIndex == swipe.Row && hit.Region is HitRegion.Cell or HitRegion.RowDragHandle);
            if (!onOpenRow)
            {
                CloseSwipe(); // a press elsewhere closes the open row, and acts as usual
            }
            else
            {
                _swipePressOnOpenRow = true;
                _swipePressItem = hit.Region == HitRegion.SwipeItem ? hit.SwipeItemIndex : -1;
                swipe.PressedItem = _swipePressItem;
                _swipeCandidate = e.DeviceType == KumikoPointerDeviceType.Touch; // a finger can drag it on
                if (_swipePressItem >= 0) Redraw();
                return;
            }
        }

        // A finger on a data cell may swipe its row, once it moves
        _swipeCandidate = _allowSwiping && e.DeviceType == KumikoPointerDeviceType.Touch && swipe.HasItems &&
                          hit.Region == HitRegion.Cell && IsDataRow(dataSource, hit.RowIndex) &&
                          !(_editSession is { IsEditing: true, ActiveEditor: { } editor } && editor.HitTest(e.X, e.Y));
    }

    private bool OnSwipeMove(KumikoPointerEventArgs e, ScrollState scroll, KumikoSelectionModel selection,
        KumikoStyle style, KumikoSource dataSource)
    {
        var swipe = Swipe;
        if (swipe.Phase == SwipePhase.Tracking)
        {
            TrackSwipe(e.X, e.TimestampMs);
            e.Handled = true;
            Redraw();
            return true;
        }

        if (!_swipeCandidate && !_swipePressOnOpenRow) return false;
        float dx = e.X - _pressOriginX, dy = e.Y - _pressOriginY;
        if (dx * dx + dy * dy <= PanThreshold * PanThreshold)
        {
            if (_swipePressItem < 0) return false;
            e.Handled = true; // a press on an item holds it within the slop
            return true;
        }

        // Past the slop the press decides: a horizontal-dominant move swipes, anything else pans (and closes an open row)
        bool horizontal = MathF.Abs(dx) > RowSwipe.DirectionRatio * MathF.Abs(dy);
        bool candidate = _swipeCandidate;
        _swipeCandidate = false;
        bool pressOnOpenRow = _swipePressOnOpenRow;
        _swipePressOnOpenRow = false;
        _swipePressItem = -1;
        swipe.PressedItem = -1;

        if (horizontal && candidate && pressOnOpenRow && swipe.Phase == SwipePhase.Open)
        {
            ResumeSwipe(e, style);
            return true;
        }

        if (horizontal && candidate && !pressOnOpenRow &&
            TryBeginSwipe(dx > 0 ? KumikoSwipeDirection.Leading : KumikoSwipeDirection.Trailing, e, scroll, selection,
                style, dataSource))
        {
            return true;
        }

        if (pressOnOpenRow) CloseSwipe();
        return false;
    }

    private bool OnSwipeRelease(KumikoPointerEventArgs e, ScrollState scroll, KumikoStyle style, KumikoSource dataSource,
        bool cancelled)
    {
        var swipe = Swipe;
        _swipeCandidate = false;
        if (swipe.Phase == SwipePhase.Tracking)
        {
            if (!cancelled) TrackSwipe(e.X, e.TimestampMs);
            if (e.TimestampMs - _swipeLastMs > SwipeFlingStaleMs) _swipeVelocity = 0; // the finger rested first
            SettleSwipe(cancelled);
            e.Handled = true;
            Redraw();
            return true;
        }

        int pressedItem = _swipePressItem;
        bool pressOnOpenRow = _swipePressOnOpenRow;
        _swipePressItem = -1;
        _swipePressOnOpenRow = false;
        swipe.PressedItem = -1;
        if (!pressOnOpenRow) return false;

        if (_feedback.EndPress()) Redraw();
        if (cancelled)
        {
            e.Handled = true;
            Redraw();
            return true;
        }

        // A tap on an item runs it (an item that cannot run does nothing); a tap on the row's cells closes it
        var hit = _hitTester.HitTestCore(e.X, e.Y, scroll, style, dataSource.Columns, dataSource.RowCount, dataSource,
            e.DeviceType);
        if (pressedItem >= 0)
        {
            if (hit.Region == HitRegion.SwipeItem && hit.SwipeItemIndex == pressedItem) RunSwipeItem(pressedItem);
        }
        else
        {
            CloseSwipe();
        }
        e.Handled = true;
        Redraw();
        return true;
    }

    private bool OnSwipeLongPress(KumikoPointerEventArgs e)
    {
        _swipeCandidate = false; // a long press before moving: the row drag or the context request, as usual
        if (Swipe.Phase == SwipePhase.Tracking || _swipePressItem >= 0)
        {
            e.Handled = true; // the press is the swipe's (or an item's)
            return true;
        }

        if (_swipePressOnOpenRow)
        {
            _swipePressOnOpenRow = false;
            CloseSwipe(); // held on the open row's cells: it closes, and the long press acts as usual
        }
        return false;
    }

    /// <summary>
    /// Starts swiping the pressed row toward <paramref name="direction"/>: commits an open edit, asks
    /// <see cref="SwipeStarting"/>, then the row follows the finger. False (the press pans) when that side has no items,
    /// the commit is refused or a handler cancels.
    /// </summary>
    private bool TryBeginSwipe(KumikoSwipeDirection direction, KumikoPointerEventArgs e, ScrollState scroll,
        KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        var swipe = Swipe;
        var items = direction == KumikoSwipeDirection.Leading ? swipe.Leading : swipe.Trailing;
        if (items.Count == 0 || !IsDataRow(dataSource, _pressRowIndex)) return false;

        // The row is the press's item: committing an edit may sort it elsewhere
        object item = dataSource.GetItem(_pressRowIndex);
        if (!CommitEditBeforeViewChange(dataSource, selection)) return false;
        int row = dataSource.IndexOfRowKey(item);
        if (row < 0) return false;

        if (SwipeStarting is { } handler)
        {
            var args = new KumikoSwipeStartingEventArgs(item, row, direction);
            handler(this, args);
            if (args.Cancel) return false;
        }

        if (swipe.Phase == SwipePhase.Open) AbortSwipe(); // a row opened meanwhile (from a handler) gives way
        float left = RowSwipe.BandLeft(style), right = RowSwipe.BandRight(scroll, style);
        _animations.StopSwipeSnap(); // a row still sliding shut stops
        swipe.Clear();
        swipe.Phase = SwipePhase.Tracking;
        swipe.Item = item;
        swipe.Row = row;
        swipe.ViewVersion = dataSource.ViewVersion;
        swipe.Direction = direction;
        swipe.BandWidth = Math.Max(0, right - left);
        swipe.RevealWidth = Math.Min(items.Count * _swipeItemWidth, swipe.BandWidth);
        swipe.FullSwipeEnabled = FullSwipeMode == KumikoFullSwipeMode.Both ||
                                 (direction == KumikoSwipeDirection.Leading
                                     ? FullSwipeMode == KumikoFullSwipeMode.Leading
                                     : FullSwipeMode == KumikoFullSwipeMode.Trailing);
        swipe.Animate = CanAnimate(style);
        _swipeGrabX = _pressOriginX; // the row stays under the finger from where it pressed
        BeginSwipeTracking(e);
        return true;
    }

    /// <summary>A finger drags the open row on from where it is (toward closing, or further open).</summary>
    private void ResumeSwipe(KumikoPointerEventArgs e, KumikoStyle style)
    {
        var swipe = Swipe;
        float offset = swipe.CurrentOffset(_animations);
        _animations.StopSwipeSnap();
        swipe.Phase = SwipePhase.Tracking;
        swipe.Offset = offset;
        swipe.Animate = CanAnimate(style);
        _swipeGrabX = _pressOriginX - offset;
        BeginSwipeTracking(e);
    }

    private void BeginSwipeTracking(KumikoPointerEventArgs e)
    {
        _swipeLastX = e.X;
        _swipeLastMs = e.TimestampMs;
        _swipeVelocity = 0;
        _feedback.EndPress(); // the press is a swipe, not a press on what is under it
        TrackSwipe(e.X, e.TimestampMs);
        e.Handled = true;
        Redraw();
    }

    /// <summary>
    /// The row follows the pointer at <paramref name="x"/>: never past closed (a swipe keeps its side), up to its items'
    /// width, then with a rubber band, or (with a full swipe) on to the row's width. Crossing the full-swipe point
    /// either way asks for <see cref="KumikoHapticKind.SwipeThreshold"/>.
    /// </summary>
    private void TrackSwipe(float x, long timestampMs)
    {
        var swipe = Swipe;
        float sign = swipe.Direction == KumikoSwipeDirection.Leading ? 1f : -1f;
        float along = Math.Max(0, (x - _swipeGrabX) * sign);
        bool fullSwipe = swipe.FullSwipeEnabled && OutermostSwipeItemEnabled();
        float limit = swipe.RevealWidth;
        if (along > limit)
            along = fullSwipe ? Math.Min(along, swipe.BandWidth) : limit + (along - limit) * RowSwipe.RubberBandFactor;
        swipe.Offset = along * sign;

        bool armed = fullSwipe && along > 0 &&
                     along >= Math.Min(swipe.BandWidth, Math.Max(swipe.BandWidth * RowSwipe.FullSwipeFraction, limit + 1));
        if (armed != swipe.FullSwipeArmed)
        {
            swipe.FullSwipeArmed = armed;
            RequestHaptic(KumikoHapticKind.SwipeThreshold, _pressDeviceType);
        }

        // The speed of the last segment that moved (a release where the last move was keeps the speed it had)
        long elapsed = timestampMs - _swipeLastMs;
        if (elapsed > 0 && InertialScroller.Differs(x, _swipeLastX))
        {
            _swipeVelocity = (x - _swipeLastX) / elapsed;
            _swipeLastX = x;
            _swipeLastMs = timestampMs;
        }
    }

    /// <summary>
    /// The finger lifted (or the gesture was canceled): past the full-swipe point the outermost item runs; otherwise a
    /// fling opens or shuts the row, and with no fling it opens when it is past half an item's width. A canceled gesture
    /// never runs an item or flings.
    /// </summary>
    private void SettleSwipe(bool cancelled)
    {
        var swipe = Swipe;
        float sign = swipe.Direction == KumikoSwipeDirection.Leading ? 1f : -1f;
        if (!cancelled && swipe.FullSwipeArmed && OutermostSwipeItemEnabled())
        {
            RunSwipeItem(0);
            return;
        }

        swipe.FullSwipeArmed = false;
        float opening = cancelled ? 0f : _swipeVelocity * sign; // positive: toward open
        bool open = opening > RowSwipe.FlingVelocity
                    || (opening >= -RowSwipe.FlingVelocity && MathF.Abs(swipe.Offset) >= Math.Min(_swipeItemWidth, swipe.RevealWidth) / 2f);
        if (open && swipe.RevealWidth > 0)
            SnapSwipe(SwipePhase.Open, sign * swipe.RevealWidth);
        else
            EndSwipe(null);
    }

    /// <summary>Runs item <paramref name="index"/> of the swiped row when it can run: the row closes first, then the
    /// item runs and <see cref="SwipeEnded"/> is raised. Returns whether it ran.</summary>
    private bool RunSwipeItem(int index)
    {
        var swipe = Swipe;
        var items = swipe.Items;
        if (index < 0 || index >= items.Count || swipe.Item is not { } dataItem) return false;
        var definition = items[index];
        if (!ActionButtonPainter.IsEnabled(definition, dataItem)) return false;

        // The row closes before the item runs: the item may change or remove the rows
        var direction = swipe.Direction;
        SnapSwipe(SwipePhase.Closing, 0);
        bool ran = ActionButtonPainter.TryInvoke(definition, dataItem);
        SwipeEnded?.Invoke(this, new KumikoSwipeEndedEventArgs(dataItem, direction, ran ? definition : null));
        return ran;
    }

    /// <summary>Whether the outermost item of the swipe's side can run (a full swipe runs only an enabled item).</summary>
    private bool OutermostSwipeItemEnabled()
    {
        var swipe = Swipe;
        var items = swipe.Items;
        return items.Count > 0 && ActionButtonPainter.IsEnabled(items[0], swipe.Item);
    }

    /// <summary>The row closes without running anything (sliding shut) and <see cref="SwipeEnded"/> is raised.</summary>
    private void EndSwipe(IActionButtonDefinition? invoked)
    {
        var swipe = Swipe;
        var item = swipe.Item;
        var direction = swipe.Direction;
        SnapSwipe(SwipePhase.Closing, 0);
        if (item != null) SwipeEnded?.Invoke(this, new KumikoSwipeEndedEventArgs(item, direction, invoked));
    }

    /// <summary>The row closes at once, with no transition (the view changed, or the grid was disabled).</summary>
    private void AbortSwipe()
    {
        var swipe = Swipe;
        bool wasActive = swipe.IsActive;
        var item = swipe.Item;
        var direction = swipe.Direction;
        _animations.StopSwipeSnap();
        swipe.Clear();
        _swipeCandidate = false;
        _swipePressOnOpenRow = false;
        _swipePressItem = -1;
        Redraw();
        if (wasActive && item != null) SwipeEnded?.Invoke(this, new KumikoSwipeEndedEventArgs(item, direction, null));
    }

    /// <summary>
    /// The row settles <paramref name="phase"/> (<see cref="SwipePhase.Open"/> at <paramref name="target"/>, or
    /// <see cref="SwipePhase.Closing"/> at 0): it slides there when the swipe animates, else it is there at once.
    /// </summary>
    private void SnapSwipe(SwipePhase phase, float target)
    {
        var swipe = Swipe;
        float from = swipe.CurrentOffset(_animations);
        swipe.Phase = phase;
        swipe.Offset = target;
        swipe.FullSwipeArmed = false;
        swipe.PressedItem = -1;
        if (swipe.Animate && _animationsEnabled && MathF.Abs(from - target) >= 0.5f)
        {
            swipe.SnapFrom = from;
            _animations.StartSwipeSnap();
        }
        else
        {
            _animations.StopSwipeSnap();
            if (phase == SwipePhase.Closing) swipe.Clear();
        }
        Redraw();
    }
}
