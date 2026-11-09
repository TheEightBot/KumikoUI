using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The <see cref="DrawnComponent"/> base: bounds, focus, enabled and visible state with their redraw
/// requests, hit testing, default input handling, and the value-changed and edit-completed signals
/// (<see cref="ComponentValueChangedEventArgs"/>).
/// </summary>
public class DrawnComponentTests
{
    private sealed class Probe : DrawnComponent
    {
        public int BoundsChanges, GotFocus, LostFocus;
        public override void OnDraw(IDrawingContext ctx) { }
        protected override void OnBoundsChanged() => BoundsChanges++;
        protected override void OnGotFocus() => GotFocus++;
        protected override void OnLostFocus() => LostFocus++;
        public void ChangeValue(object? o, object? n) => RaiseValueChanged(o, n);
        public void Complete() => RaiseEditCompleted();
    }

    private static Probe Make() => new() { Bounds = new KumikoRect(10, 20, 100, 40) };

    [Fact]
    public void Bounds_NotifiesOnlyWhenTheRectangleChanges()
    {
        var c = Make();
        Assert.Equal(1, c.BoundsChanges);

        c.Bounds = new KumikoRect(10, 20, 100, 40);
        Assert.Equal(1, c.BoundsChanges);

        c.Bounds = new KumikoRect(10, 20, 120, 40);
        Assert.Equal(2, c.BoundsChanges);
        Assert.Equal(120, c.Bounds.Width);
    }

    [Fact]
    public void Focus_ThroughTheManager_RunsTheHooksAndRequestsARedraw()
    {
        var c = Make();
        int redraws = 0;
        c.RedrawRequested += () => redraws++;
        var manager = new DrawnFocusManager();
        manager.Register(c);

        manager.SetFocus(c);
        Assert.True(c.IsFocused);
        Assert.Equal(1, c.GotFocus);
        Assert.Equal(1, redraws);

        manager.ClearFocus();
        Assert.False(c.IsFocused);
        Assert.Equal(1, c.LostFocus);
        Assert.Equal(2, redraws);
    }

    [Fact]
    public void EnabledAndVisible_RequestARedrawOnlyOnChange()
    {
        var c = Make();
        int redraws = 0;
        c.RedrawRequested += () => redraws++;

        c.IsEnabled = true;
        c.IsVisible = true;
        Assert.Equal(0, redraws);

        c.IsEnabled = false;
        c.IsVisible = false;
        Assert.Equal(2, redraws);
    }

    [Fact]
    public void HitTest_RequiresVisibleEnabledAndInsideTheBounds()
    {
        var c = Make();
        Assert.True(c.HitTest(10, 20));
        Assert.True(c.HitTest(60, 40));
        Assert.False(c.HitTest(9, 40));
        Assert.False(c.HitTest(60, 70));

        c.IsEnabled = false;
        Assert.False(c.HitTest(60, 40));

        c.IsEnabled = true;
        c.IsVisible = false;
        Assert.False(c.HitTest(60, 40));
    }

    [Fact]
    public void DefaultInputHandlers_DeclineEveryEvent()
    {
        var c = Make();
        Assert.False(c.Down(50, 30));
        Assert.False(c.Move(50, 30));
        Assert.False(c.Up(50, 30));
        Assert.False(c.OnScroll(new KumikoPointerEventArgs { ScrollDeltaY = 1, Action = InputAction.Scroll }));
        Assert.False(c.Key(KumikoKey.Enter));
        Assert.False(c.OnKeyUp(new KumikoKeyEventArgs { Key = KumikoKey.Enter, IsKeyDown = false }));
        Assert.False(c.ActivatesImmediately);
    }

    [Fact]
    public void RaiseValueChanged_DeliversTheOldAndNewValuesWithTheSender()
    {
        var c = Make();
        object? sender = null;
        ComponentValueChangedEventArgs? args = null;
        c.ValueChanged += (s, e) => { sender = s; args = e; };

        c.ChangeValue(null, "next");

        Assert.Same(c, sender);
        Assert.Null(args!.OldValue);
        Assert.Equal("next", args.NewValue);

        c.ChangeValue("next", 42);
        Assert.Equal("next", args.OldValue);
        Assert.Equal(42, args.NewValue);
    }

    [Fact]
    public void RaiseEditCompleted_SignalsEverySubscriber()
    {
        var c = Make();
        int a = 0, b = 0;
        c.EditCompleted += () => a++;
        c.EditCompleted += () => b++;

        c.Complete();
        c.Complete();

        Assert.Equal(2, a);
        Assert.Equal(2, b);
    }

    [Fact]
    public void RaisingWithoutSubscribers_DoesNotThrow()
    {
        var c = Make();
        c.ChangeValue(1, 2);
        c.Complete();
        c.InvalidateVisual();
    }

    [Fact]
    public void ZOrderAndTag_RoundTrip()
    {
        var c = Make();
        var tag = new object();
        c.ZOrder = 7;
        c.Tag = tag;
        Assert.Equal(7, c.ZOrder);
        Assert.Same(tag, c.Tag);
    }
}
