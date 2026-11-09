using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The drawn check box: toggling by pointer and keyboard, the two-state and tri-state cycles, the
/// <see cref="CheckStateChangedEventArgs"/> and value-changed events, and what each
/// <see cref="DrawnCheckState"/> draws.
/// </summary>
public class DrawnCheckBoxTests
{
    private static DrawnCheckBox Box(bool threeState = false) =>
        new() { Bounds = new KumikoRect(0, 0, 40, 30), IsThreeState = threeState };

    [Fact]
    public void Tap_TogglesBetweenCheckedAndUnchecked_AndReportsEachChange()
    {
        var box = Box();
        var changes = new List<CheckStateChangedEventArgs>();
        var values = new List<ComponentValueChangedEventArgs>();
        box.CheckStateChanged += (_, e) => changes.Add(e);
        box.ValueChanged += (_, e) => values.Add(e);

        box.Tap(20, 15);
        box.Tap(20, 15);

        Assert.Equal(DrawnCheckState.Unchecked, box.State);
        Assert.Equal(2, changes.Count);
        Assert.Equal((DrawnCheckState.Unchecked, DrawnCheckState.Checked), (changes[0].OldState, changes[0].NewState));
        Assert.Equal((DrawnCheckState.Checked, DrawnCheckState.Unchecked), (changes[1].OldState, changes[1].NewState));
        Assert.Equal(DrawnCheckState.Unchecked, values[0].OldValue);
        Assert.Equal(DrawnCheckState.Checked, values[0].NewValue);
    }

    [Fact]
    public void PointerDown_DoesNotToggle_OnlyTheRelease_Does()
    {
        var box = Box();

        bool down = box.Down(20, 15);
        Assert.False(down);
        Assert.False(box.IsChecked);

        Assert.True(box.Up(20, 15));
        Assert.True(box.IsChecked);
    }

    [Fact]
    public void SpaceAndEnter_Toggle_OtherKeysAreIgnored()
    {
        var box = Box();

        Assert.True(box.Key(KumikoKey.Space));
        Assert.True(box.IsChecked);
        Assert.True(box.Key(KumikoKey.Enter));
        Assert.False(box.IsChecked);

        Assert.False(box.Key(KumikoKey.Tab));
        Assert.False(box.Key(KumikoKey.A, 'a'));
        Assert.False(box.IsChecked);
    }

    [Fact]
    public void ThreeState_CyclesThroughIndeterminate()
    {
        var box = Box(threeState: true);
        var seen = new List<DrawnCheckState>();
        box.CheckStateChanged += (_, e) => seen.Add(e.NewState);

        for (int i = 0; i < 4; i++) box.Key(KumikoKey.Space);

        Assert.Equal(new[]
        {
            DrawnCheckState.Checked, DrawnCheckState.Indeterminate, DrawnCheckState.Unchecked, DrawnCheckState.Checked,
        }, seen);
    }

    [Fact]
    public void TwoState_FromIndeterminate_GoesToChecked()
    {
        var box = Box();
        box.State = DrawnCheckState.Indeterminate;

        box.Toggle();

        Assert.Equal(DrawnCheckState.Checked, box.State);
    }

    [Fact]
    public void SettingTheState_RaisesValueChangedButNotCheckStateChanged()
    {
        var box = Box();
        int stateEvents = 0, valueEvents = 0;
        box.CheckStateChanged += (_, _) => stateEvents++;
        box.ValueChanged += (_, _) => valueEvents++;

        box.IsChecked = true;
        box.IsChecked = true;
        box.State = DrawnCheckState.Indeterminate;

        Assert.Equal(0, stateEvents);
        Assert.Equal(2, valueEvents);
        Assert.False(box.IsChecked);
    }

    [Fact]
    public void Release_OnADisabledCheckBox_IsIgnored()
    {
        var box = Box();
        box.IsEnabled = false;

        Assert.False(box.Up(20, 15));
        Assert.False(box.IsChecked);
    }

    [Fact]
    public void ClickThroughTheFocusManager_TogglesOnlyWhenReleasedInside()
    {
        var box = Box();
        var manager = new DrawnFocusManager();
        manager.Register(box);

        manager.DispatchPointer(Ptr(InputAction.Pressed, 20, 15));
        manager.DispatchPointer(Ptr(InputAction.Released, 200, 200));
        Assert.False(box.IsChecked);

        manager.DispatchPointer(Ptr(InputAction.Released, 20, 15));
        Assert.True(box.IsChecked);
    }

    [Fact]
    public void Keyboard_ThroughTheFocusManager_TogglesTheFocusedBox()
    {
        var box = Box();
        var manager = Focus(box);

        Assert.True(manager.DispatchKey(new KumikoKeyEventArgs { Key = KumikoKey.Space }));
        Assert.True(box.IsChecked);
    }

    // ── Drawing ───────────────────────────────────────────────────

    [Fact]
    public void Draw_Unchecked_ShowsAnEmptyCenteredBox()
    {
        var box = Box();
        var ctx = Draw(box);

        var fill = Assert.Single(ctx.Fills);
        Assert.Equal(DrawOpKind.FillRoundRect, fill.Kind);
        Assert.Equal(new KumikoRect(11, 6, 18, 18), fill.Rect);
        Assert.Equal(box.BoxFillColor, fill.Color);
        Assert.Empty(ctx.Lines);
        var border = ctx.Ops.Single(o => o.Kind == DrawOpKind.DrawRoundRect);
        Assert.Equal(box.BoxBorderColor, border.Color);
    }

    [Fact]
    public void Draw_Checked_FillsWithTheCheckedColorAndDrawsTheTick()
    {
        var box = Box();
        box.IsChecked = true;
        var ctx = Draw(box);

        Assert.Equal(box.CheckedBoxFillColor, Assert.Single(ctx.Fills).Color);
        var ticks = ctx.Lines.ToList();
        Assert.Equal(2, ticks.Count);
        Assert.All(ticks, t => Assert.Equal(box.CheckMarkColor, t.Color));
        // The tick's two strokes meet at its lowest point
        Assert.Equal(ticks[0].X2, ticks[1].X1);
        Assert.Equal(ticks[0].Y2, ticks[1].Y1);
        Assert.True(ticks[1].Y2 < ticks[1].Y1);
    }

    [Fact]
    public void Draw_Indeterminate_DrawsAHorizontalDashInsideTheBox()
    {
        var box = Box();
        box.State = DrawnCheckState.Indeterminate;
        var ctx = Draw(box);

        var fills = ctx.Fills.ToList();
        Assert.Equal(2, fills.Count);
        var dash = fills[1];
        Assert.Equal(DrawOpKind.FillRect, dash.Kind);
        Assert.Equal(box.IndeterminateColor, dash.Color);
        Assert.Equal(9, dash.Rect.Width);
        Assert.Equal(3, dash.Rect.Height);
        Assert.Equal(20, dash.Rect.X + dash.Rect.Width / 2, 3);
        Assert.Empty(ctx.Lines);
    }

    [Fact]
    public void Draw_FocusChangesTheBorderColor()
    {
        var box = Box();
        var plain = Draw(box).Ops.Single(o => o.Kind == DrawOpKind.DrawRoundRect).Color;

        Focus(box);
        var focused = Draw(box).Ops.Single(o => o.Kind == DrawOpKind.DrawRoundRect).Color;

        Assert.NotEqual(plain, focused);
    }
}
