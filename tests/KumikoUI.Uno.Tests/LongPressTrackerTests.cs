using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Uno.Input;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// The Uno host's long-press rule (UNO-13): 500 ms / 15 px, once per press, and the release that ends it is swallowed.
/// The tracker is driven by a manual timer; the host-level tests feed a real Core input controller with the same
/// pointer sequence the host synthesizes.
/// </summary>
public class LongPressTrackerTests
{
    private sealed class ManualTimer : ILongPressTimer
    {
        private Action? _tick;
        public TimeSpan? Interval { get; private set; }
        public bool IsRunning => _tick != null;
        public void Start(TimeSpan interval, Action tick) { Interval = interval; _tick = tick; }
        public void Stop() => _tick = null;
        public void Elapse() { var t = _tick; _tick = null; t?.Invoke(); }
    }

    private static (LongPressTracker Tracker, ManualTimer Timer, List<(uint Id, float X, float Y)> Fired) Create()
    {
        var timer = new ManualTimer();
        var fired = new List<(uint, float, float)>();
        return (new LongPressTracker(timer, (id, x, y) => fired.Add((id, x, y))), timer, fired);
    }

    [Fact]
    public void Press_ArmsA500MsTimer()
    {
        var (tracker, timer, _) = Create();

        tracker.Press(1, 10, 10);

        Assert.Equal(TimeSpan.FromMilliseconds(500), timer.Interval);
        Assert.True(timer.IsRunning);
    }

    [Fact]
    public void TimerElapsing_FiresOnceAtThePressPosition()
    {
        var (tracker, timer, fired) = Create();
        tracker.Press(7, 12, 34);

        timer.Elapse();
        timer.Elapse();

        Assert.Equal(new[] { (7u, 12f, 34f) }, fired);
        Assert.True(tracker.HasFired);
    }

    [Fact]
    public void Release_AfterALongPress_IsSwallowedOnce()
    {
        var (tracker, timer, _) = Create();
        tracker.Press(1, 10, 10);
        timer.Elapse();

        Assert.True(tracker.ConsumeRelease());
        Assert.False(tracker.ConsumeRelease());
    }

    [Fact]
    public void Release_BeforeTheTimer_IsNotSwallowed_AndTheTimerStops()
    {
        var (tracker, timer, fired) = Create();
        tracker.Press(1, 10, 10);

        Assert.False(tracker.ConsumeRelease());
        timer.Elapse();

        Assert.Empty(fired);
        Assert.False(timer.IsRunning);
    }

    [Fact]
    public void Movement_WithinTheSlop_KeepsTheLongPress()
    {
        var (tracker, timer, fired) = Create();
        tracker.Press(1, 100, 100);

        tracker.Move(1, 109, 112); // 15 px away

        Assert.True(timer.IsRunning);
        timer.Elapse();
        Assert.Single(fired);
    }

    [Fact]
    public void Movement_BeyondTheSlop_CancelsTheLongPress()
    {
        var (tracker, timer, fired) = Create();
        tracker.Press(1, 100, 100);

        tracker.Move(1, 100, 116);
        timer.Elapse();

        Assert.Empty(fired);
        Assert.False(tracker.ConsumeRelease());
    }

    [Fact]
    public void Movement_OfAnotherPointer_IsIgnored()
    {
        var (tracker, timer, fired) = Create();
        tracker.Press(1, 100, 100);

        tracker.Move(2, 500, 500);
        timer.Elapse();

        Assert.Single(fired);
    }

    [Fact]
    public void Reset_ForgetsAFiredLongPress()
    {
        var (tracker, timer, _) = Create();
        tracker.Press(1, 10, 10);
        timer.Elapse();

        tracker.Reset();

        Assert.False(tracker.ConsumeRelease());
    }

    [Fact]
    public void NewPress_ForgetsThePreviousLongPress()
    {
        var (tracker, timer, _) = Create();
        tracker.Press(1, 10, 10);
        timer.Elapse();

        tracker.Press(1, 10, 10);

        Assert.False(tracker.HasFired);
    }

    // ── Holding fallback ──

    [Fact]
    public void Holding_FiresWhenTheTimerHasNot()
    {
        var (tracker, timer, fired) = Create();
        tracker.Press(3, 10, 10);

        Assert.True(tracker.NotifyHolding(3, 11, 12));
        timer.Elapse();

        Assert.Equal(new[] { (3u, 11f, 12f) }, fired);
        Assert.True(tracker.ConsumeRelease());
    }

    [Fact]
    public void Holding_AfterTheTimerFired_DoesNotFireAgain()
    {
        var (tracker, timer, fired) = Create();
        tracker.Press(3, 10, 10);
        timer.Elapse();

        Assert.False(tracker.NotifyHolding(3, 10, 10));

        Assert.Single(fired);
    }

    [Fact]
    public void Holding_AfterMovingTooFar_DoesNotFire()
    {
        var (tracker, _, fired) = Create();
        tracker.Press(3, 10, 10);
        tracker.Move(3, 80, 80);

        Assert.False(tracker.NotifyHolding(3, 80, 80));

        Assert.Empty(fired);
    }

    // ── Host-level: the pointer sequence the host synthesizes, into a real Core controller ──

    /// <summary>The routing of OnPointerPressed/Moved/Released/OnLongPressFired, with the Core controller behind it.</summary>
    private sealed class HostHarness
    {
        public readonly ManualTimer Timer = new();
        public readonly LongPressTracker Tracker;
        public readonly KumikoColumn Column = new()
        {
            Header = "On", PropertyName = "On", Width = 100, ColumnType = KumikoColumnType.Boolean,
        };
        public readonly List<int> RowsTapped = new();
        public int Changes;
        private readonly Flag[] _items;
        private readonly KumikoSource _source = new();
        private readonly KumikoSelectionModel _selection;
        private readonly ScrollState _scroll = new() { ViewportWidth = 300, ViewportHeight = 200 };
        private readonly KumikoStyle _style = new();
        private readonly KumikoInputController _input;

        public sealed class Flag { public bool On { get; set; } }

        public HostHarness(EditTrigger triggers)
        {
            _items = Enumerable.Range(0, 3).Select(_ => new Flag()).ToArray();
            _source.SetColumns(new[] { Column });
            _source.SetItems(_items);
            _selection = new KumikoSelectionModel(_source);
            var edit = new EditSession { Style = _style, EditTriggers = triggers };
            edit.CellValueChanged += (_, _) => Changes++;
            _input = new KumikoInputController { EditSession = edit };
            _input.RowTapped += (_, e) => RowsTapped.Add(e.RowIndex);
            Tracker = new LongPressTracker(Timer, (id, x, y) => Send(InputAction.LongPress, x, y, id));
        }

        public bool IsOn(int row) => _items[row].On;
        public float RowY(int row) => _style.HeaderHeight + row * _style.RowHeight + _style.RowHeight / 2;
        public const float CheckBoxX = 50;

        public void Press(float x, float y)
        {
            Send(InputAction.Pressed, x, y, 1);
            Tracker.Press(1, x, y);
        }

        public void Move(float x, float y)
        {
            Tracker.Move(1, x, y);
            Send(InputAction.Moved, x, y, 1);
        }

        public void Release(float x, float y)
        {
            bool swallow = Tracker.ConsumeRelease();
            Send(swallow ? InputAction.Cancelled : InputAction.Released, x, y, 1);
        }

        private void Send(InputAction action, float x, float y, uint id) =>
            _input.HandlePointer(new KumikoPointerEventArgs
            {
                X = x, Y = y, Action = action, PointerId = id, DeviceType = KumikoPointerDeviceType.Touch,
            }, _scroll, _selection, _style, _source);
    }

    [Fact]
    public void BooleanLongPress_WithTheLongPressTrigger_TogglesOnce_AndDoesNotRaiseRowTapped()
    {
        var h = new HostHarness(EditTrigger.LongPress);
        float y = h.RowY(1);

        h.Press(HostHarness.CheckBoxX, y);
        h.Timer.Elapse();
        h.Release(HostHarness.CheckBoxX, y);

        Assert.True(h.IsOn(1));
        Assert.Equal(1, h.Changes);
        Assert.Empty(h.RowsTapped);
    }

    [Fact]
    public void BooleanLongPress_ViaTheHoldingFallbackAndTheTimer_StillTogglesOnce()
    {
        var h = new HostHarness(EditTrigger.LongPress);
        float y = h.RowY(0);

        h.Press(HostHarness.CheckBoxX, y);
        Assert.True(h.Tracker.NotifyHolding(1, HostHarness.CheckBoxX, y));
        h.Timer.Elapse();
        h.Release(HostHarness.CheckBoxX, y);

        Assert.True(h.IsOn(0));
        Assert.Equal(1, h.Changes);
    }

    [Fact]
    public void ShortTap_StillTogglesAndRaisesRowTapped()
    {
        var h = new HostHarness(EditTrigger.LongPress);
        float y = h.RowY(2);

        h.Press(HostHarness.CheckBoxX, y);
        h.Release(HostHarness.CheckBoxX, y);

        Assert.True(h.IsOn(2));
        Assert.Equal(new[] { 2 }, h.RowsTapped);
    }

    [Fact]
    public void PressMovedBeyondTheSlop_DoesNotLongPress()
    {
        var h = new HostHarness(EditTrigger.LongPress);
        float y = h.RowY(1);

        h.Press(HostHarness.CheckBoxX, y);
        h.Move(HostHarness.CheckBoxX, y + 30);
        h.Timer.Elapse();
        h.Release(HostHarness.CheckBoxX, y + 30);

        Assert.False(h.IsOn(1));
        Assert.Equal(0, h.Changes);
    }

    [Fact]
    public void NextPress_AfterALongPress_TapsNormally()
    {
        var h = new HostHarness(EditTrigger.LongPress);
        float y = h.RowY(1);
        h.Press(HostHarness.CheckBoxX, y);
        h.Timer.Elapse();
        h.Release(HostHarness.CheckBoxX, y);

        h.Press(HostHarness.CheckBoxX, y);
        h.Release(HostHarness.CheckBoxX, y);

        Assert.False(h.IsOn(1)); // toggled on by the long press, off by the tap
        Assert.Equal(2, h.Changes);
        Assert.Equal(new[] { 1 }, h.RowsTapped);
    }
}
