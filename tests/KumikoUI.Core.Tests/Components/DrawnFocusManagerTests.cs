using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// <see cref="DrawnFocusManager"/>: registration, single-focus hand-over with
/// <see cref="KumikoFocusChangedEventArgs"/>, tab order, key dispatch to the focused component and
/// pointer dispatch in z-order.
/// </summary>
public class DrawnFocusManagerTests
{
    private sealed class Probe : DrawnComponent
    {
        public bool HandlesPress { get; set; } = true;
        public bool HandlesRelease { get; init; } = true;
        public List<string> Received { get; } = new();
        public override void OnDraw(IDrawingContext ctx) { }
        public override bool OnPointerDown(KumikoPointerEventArgs e) { Received.Add("down"); return HandlesPress; }
        public override bool OnPointerMove(KumikoPointerEventArgs e) { Received.Add("move"); return true; }
        public override bool OnPointerUp(KumikoPointerEventArgs e) { Received.Add("up"); return HandlesRelease; }
        public override bool OnKeyDown(KumikoKeyEventArgs e) { Received.Add("keydown:" + e.Key); return true; }
        public override bool OnKeyUp(KumikoKeyEventArgs e) { Received.Add("keyup:" + e.Key); return true; }
    }

    private static Probe At(float x, float y, float w = 50, float h = 30, int z = 0) =>
        new() { Bounds = new KumikoRect(x, y, w, h), ZOrder = z };

    private static (DrawnFocusManager Manager, Probe[] Probes) Row(int n)
    {
        var manager = new DrawnFocusManager();
        var probes = Enumerable.Range(0, n).Select(i => At(i * 60f, 0)).ToArray();
        foreach (var p in probes) manager.Register(p);
        return (manager, probes);
    }

    // ── Focus ─────────────────────────────────────────────────────

    [Fact]
    public void SetFocus_MovesFocusAndReportsTheHandOver()
    {
        var (m, p) = Row(2);
        var events = new List<KumikoFocusChangedEventArgs>();
        m.FocusChanged += (_, e) => events.Add(e);

        m.SetFocus(p[0]);
        m.SetFocus(p[1]);

        Assert.Same(p[1], m.FocusedComponent);
        Assert.False(p[0].IsFocused);
        Assert.True(p[1].IsFocused);
        Assert.Equal(2, events.Count);
        Assert.Null(events[0].OldFocus);
        Assert.Same(p[0], events[0].NewFocus);
        Assert.Same(p[0], events[1].OldFocus);
        Assert.Same(p[1], events[1].NewFocus);
    }

    [Fact]
    public void SetFocus_OnTheFocusedComponent_RaisesNothing()
    {
        var (m, p) = Row(1);
        m.SetFocus(p[0]);
        int raised = 0;
        m.FocusChanged += (_, _) => raised++;

        m.SetFocus(p[0]);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void ClearFocus_RemovesFocusFromEveryComponent()
    {
        var (m, p) = Row(1);
        m.SetFocus(p[0]);
        KumikoFocusChangedEventArgs? args = null;
        m.FocusChanged += (_, e) => args = e;

        m.ClearFocus();

        Assert.Null(m.FocusedComponent);
        Assert.False(p[0].IsFocused);
        Assert.Same(p[0], args!.OldFocus);
        Assert.Null(args.NewFocus);
    }

    [Fact]
    public void Unregister_OfTheFocusedComponent_ForgetsIt()
    {
        var (m, p) = Row(2);
        m.SetFocus(p[0]);

        m.Unregister(p[0]);

        Assert.Null(m.FocusedComponent);
        m.FocusNext();
        Assert.Same(p[1], m.FocusedComponent);
    }

    [Fact]
    public void Unregister_OfTheFocusedComponent_RemovesItsFocusState()
    {
        var (m, p) = Row(1);
        m.SetFocus(p[0]);

        m.Unregister(p[0]);

        Assert.False(p[0].IsFocused);
    }

    [Fact]
    public void Register_TwiceKeepsASingleTabStop()
    {
        var m = new DrawnFocusManager();
        var a = At(0, 0);
        var b = At(60, 0);
        m.Register(a);
        m.Register(a);
        m.Register(b);

        m.FocusNext();
        m.FocusNext();
        m.FocusNext();

        Assert.Same(a, m.FocusedComponent);
    }

    // ── Tab order ─────────────────────────────────────────────────

    [Fact]
    public void FocusNext_WalksTheTabOrderAndWraps()
    {
        var (m, p) = Row(3);

        var seen = new List<DrawnComponent?>();
        for (int i = 0; i < 4; i++) { m.FocusNext(); seen.Add(m.FocusedComponent); }

        Assert.Equal(new[] { p[0], p[1], p[2], p[0] }, seen);
    }

    [Fact]
    public void FocusPrevious_WalksBackwardsAndWraps()
    {
        var (m, p) = Row(3);

        var seen = new List<DrawnComponent?>();
        for (int i = 0; i < 4; i++) { m.FocusPrevious(); seen.Add(m.FocusedComponent); }

        Assert.Equal(new[] { p[2], p[1], p[0], p[2] }, seen);
    }

    [Fact]
    public void Tabbing_SkipsInvisibleAndDisabledComponents()
    {
        var (m, p) = Row(4);
        p[1].IsVisible = false;
        p[2].IsEnabled = false;

        m.FocusNext();
        Assert.Same(p[0], m.FocusedComponent);
        m.FocusNext();
        Assert.Same(p[3], m.FocusedComponent);
        m.FocusPrevious();
        Assert.Same(p[0], m.FocusedComponent);
    }

    [Fact]
    public void Tabbing_WithNothingFocusable_DoesNothing()
    {
        var m = new DrawnFocusManager();
        m.FocusNext();
        m.FocusPrevious();
        Assert.Null(m.FocusedComponent);

        var (m2, p) = Row(1);
        p[0].IsEnabled = false;
        m2.FocusNext();
        Assert.Null(m2.FocusedComponent);
    }

    // ── Keys ──────────────────────────────────────────────────────

    [Fact]
    public void DispatchKey_RoutesKeyDownAndKeyUpToTheFocusedComponentOnly()
    {
        var (m, p) = Row(2);
        m.SetFocus(p[1]);

        Assert.True(m.DispatchKey(new KumikoKeyEventArgs { Key = KumikoKey.Enter }));
        Assert.True(m.DispatchKey(new KumikoKeyEventArgs { Key = KumikoKey.Enter, IsKeyDown = false }));

        Assert.Empty(p[0].Received);
        Assert.Equal(new[] { "keydown:Enter", "keyup:Enter" }, p[1].Received);
    }

    [Fact]
    public void DispatchKey_WithoutFocus_OrToADisabledComponent_IsNotHandled()
    {
        var (m, p) = Row(1);
        var key = new KumikoKeyEventArgs { Key = KumikoKey.Space };

        Assert.False(m.DispatchKey(key));

        m.SetFocus(p[0]);
        p[0].IsEnabled = false;
        Assert.False(m.DispatchKey(key));
        Assert.Empty(p[0].Received);
    }

    // ── Pointer ───────────────────────────────────────────────────

    [Fact]
    public void DispatchPointer_PressFocusesTheHitComponentAndRoutesTheSequence()
    {
        var (m, p) = Row(2);

        Assert.True(m.DispatchPointer(Ptr(InputAction.Pressed, 70, 10)));
        Assert.True(m.DispatchPointer(Ptr(InputAction.Moved, 72, 10)));
        Assert.True(m.DispatchPointer(Ptr(InputAction.Released, 72, 10)));

        Assert.Same(p[1], m.FocusedComponent);
        Assert.Empty(p[0].Received);
        Assert.Equal(new[] { "down", "move", "up" }, p[1].Received);
    }

    [Fact]
    public void DispatchPointer_PressOutsideEveryComponent_ClearsFocus()
    {
        var (m, p) = Row(1);
        m.SetFocus(p[0]);

        bool handled = m.DispatchPointer(Ptr(InputAction.Pressed, 500, 500));

        Assert.False(handled);
        Assert.Null(m.FocusedComponent);
        Assert.False(p[0].IsFocused);
    }

    [Fact]
    public void DispatchPointer_MoveOutside_KeepsFocus()
    {
        var (m, p) = Row(1);
        m.SetFocus(p[0]);

        Assert.False(m.DispatchPointer(Ptr(InputAction.Moved, 500, 500)));

        Assert.Same(p[0], m.FocusedComponent);
    }

    [Fact]
    public void DispatchPointer_OverlappingComponents_HighestZOrderGetsItFirst()
    {
        var m = new DrawnFocusManager();
        var low = At(0, 0, z: 1);
        var high = At(10, 0, z: 5);
        m.Register(low);
        m.Register(high);

        m.DispatchPointer(Ptr(InputAction.Pressed, 20, 10));

        Assert.Equal(new[] { "down" }, high.Received);
        Assert.Empty(low.Received);
        Assert.Same(high, m.FocusedComponent);
    }

    [Fact]
    public void DispatchPointer_WhenTheTopComponentDeclines_FallsThroughToTheOneBelow()
    {
        var m = new DrawnFocusManager();
        var low = At(0, 0, z: 1);
        var high = At(10, 0, z: 5);
        high.HandlesPress = false;
        m.Register(low);
        m.Register(high);

        bool handled = m.DispatchPointer(Ptr(InputAction.Pressed, 20, 10));

        Assert.True(handled);
        Assert.Equal(new[] { "down" }, high.Received);
        Assert.Equal(new[] { "down" }, low.Received);
        Assert.Same(low, m.FocusedComponent);
    }

    [Fact]
    public void DispatchPointer_IgnoresInvisibleAndDisabledComponents()
    {
        var m = new DrawnFocusManager();
        var hidden = At(0, 0, z: 9);
        var disabled = At(0, 0, z: 8);
        var live = At(0, 0, z: 1);
        hidden.IsVisible = false;
        disabled.IsEnabled = false;
        m.Register(hidden);
        m.Register(disabled);
        m.Register(live);

        m.DispatchPointer(Ptr(InputAction.Pressed, 10, 10));

        Assert.Empty(hidden.Received);
        Assert.Empty(disabled.Received);
        Assert.Equal(new[] { "down" }, live.Received);
    }

    [Fact(Skip = "Known defect: a press on a component that does not handle OnPointerDown falls through to 'clicked outside' and clears the focus it just set")]
    public void DispatchPointer_PressOnAComponentThatDoesNotHandleIt_KeepsItFocused()
    {
        var m = new DrawnFocusManager();
        var box = new DrawnCheckBox { Bounds = new KumikoRect(0, 0, 40, 30) };
        m.Register(box);

        m.DispatchPointer(Ptr(InputAction.Pressed, 20, 15));

        Assert.Same(box, m.FocusedComponent);
    }
}
