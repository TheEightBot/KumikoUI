using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The drawn scroll-wheel picker: drag to scroll, coast and snap to the nearest item once released,
/// confirm on an explicit gesture or when the wheel settles (never on a mere tap), keyboard and mouse
/// wheel stepping, and what the wheel draws (<see cref="PickerSelectionChangedEventArgs"/>).
/// </summary>
public class DrawnScrollPickerTests
{
    // Bounds (0,0)-(100,200): the selection band is centered at y = 100 and items are 40 px apart.
    private static DrawnScrollPicker Picker(int count = 10, int selected = 0)
    {
        var p = new DrawnScrollPicker { Bounds = new KumikoRect(0, 0, 100, 200) };
        p.SetItems(Enumerable.Range(0, count).Select(i => $"Item {i}"));
        p.SelectedIndex = selected;
        return p;
    }

    /// <summary>Drags up by <paramref name="distance"/> px (the wheel scrolls to later items) without releasing.</summary>
    private static void DragUp(DrawnScrollPicker p, float distance, float from = 150)
    {
        p.Down(50, from);
        p.Move(50, from - distance);
    }

    /// <summary>Runs the animation frame by frame, as a host's frame timer does, until the wheel rests.</summary>
    private static int Settle(DrawnScrollPicker p)
    {
        int frames = 0;
        while (p.UpdateAnimation(16) && frames < 1000) frames++;
        Assert.False(p.IsAnimating);
        return frames;
    }

    private static KumikoPointerEventArgs Timed(InputAction action, float y, long ms) =>
        new() { X = 50, Y = y, Action = action, TimestampMs = ms };

    // ── Drag and snap ─────────────────────────────────────────────

    [Fact]
    public void DragAndRelease_SnapsToTheNearestItem_AndConfirmsItOnceSettled()
    {
        var p = Picker();
        var picks = new List<PickerSelectionChangedEventArgs>();
        var values = new List<ComponentValueChangedEventArgs>();
        int completed = 0;
        p.SelectionChanged += (_, e) => picks.Add(e);
        p.ValueChanged += (_, e) => values.Add(e);
        p.EditCompleted += () => completed++;

        DragUp(p, 85); // 2.125 items: nearest is 2
        Assert.Equal(0, p.SelectedIndex);
        Assert.True(p.Up(50, 65));

        // Released: the wheel is still settling, nothing is chosen yet
        Assert.True(p.IsAnimating);
        Assert.Equal(0, p.SelectedIndex);
        Assert.Equal(0, completed);

        Assert.True(Settle(p) > 0);

        Assert.Equal(2, p.SelectedIndex);
        Assert.Equal("Item 2", p.SelectedValue);
        Assert.Equal(2, Assert.Single(picks).SelectedIndex);
        Assert.Equal(("Item 0", "Item 2"), ((string)values[0].OldValue!, (string)values[0].NewValue!));
        Assert.Equal(1, completed);
        Assert.Equal(86, Draw(p).SingleText("Item 2").Rect.Y, 2);
    }

    [Fact]
    public void ReleaseOnTheSameItem_ChangesNothing_ButConfirmsItOnceSettled()
    {
        var p = Picker(selected: 3);
        int picks = 0, completed = 0;
        p.SelectionChanged += (_, _) => picks++;
        p.EditCompleted += () => completed++;

        DragUp(p, 10);
        p.Up(50, 140);
        Settle(p);

        Assert.Equal(3, p.SelectedIndex);
        Assert.Equal(0, picks);
        Assert.Equal(1, completed);
    }

    [Fact]
    public void Drag_ClampsAtBothEnds()
    {
        var p = Picker(count: 5);

        DragUp(p, 5000);
        p.Up(50, 0);
        Settle(p);
        Assert.Equal(4, p.SelectedIndex);

        p.Down(50, 0);
        p.Move(50, 5000);
        p.Up(50, 5000);
        Settle(p);
        Assert.Equal(0, p.SelectedIndex);
    }

    [Fact]
    public void ATapOnTheCenteredItem_ConfirmsItAtOnce()
    {
        var p = Picker(selected: 3);
        int completed = 0;
        p.EditCompleted += () => completed++;

        p.Tap(50, 104); // inside the selection band

        Assert.Equal(1, completed);
        Assert.Equal(3, p.SelectedIndex);
        Assert.False(p.IsAnimating);
    }

    [Fact]
    public void ATapOnTheCenteredItem_WithNothingSelected_SelectsAndConfirmsIt()
    {
        var p = Picker();
        p.ClearSelection();
        int completed = 0;
        p.EditCompleted += () => completed++;

        p.Tap(50, 100);

        Assert.Equal(0, p.SelectedIndex);
        Assert.Equal(1, completed);
    }

    [Fact]
    public void ATapOnAnotherItem_DoesNotConfirmAtOnce_ItScrollsThereAndConfirmsOnceSettled()
    {
        var p = Picker(selected: 3);
        int completed = 0;
        p.EditCompleted += () => completed++;

        p.Tap(50, 100 + 2 * 40); // two items below the center

        Assert.Equal(0, completed);
        Assert.Equal(3, p.SelectedIndex);
        Assert.True(p.IsAnimating);

        Settle(p);

        Assert.Equal(5, p.SelectedIndex);
        Assert.Equal(1, completed);
    }

    [Fact]
    public void ATapOutsideTheItems_ConfirmsNothing()
    {
        var p = Picker(count: 2);
        int completed = 0;
        p.EditCompleted += () => completed++;

        p.Tap(50, 10); // above the first item: nothing there

        Assert.False(p.IsAnimating);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void APressWhileSettling_StopsTheWheel_AndDropsThePendingConfirmation()
    {
        var p = Picker();
        int completed = 0;
        p.EditCompleted += () => completed++;
        DragUp(p, 85);
        p.Up(50, 65);
        p.UpdateAnimation(16);

        p.Down(50, 100);

        Assert.False(p.IsAnimating);
        Assert.False(p.UpdateAnimation(16));
        Assert.Equal(0, completed);
    }

    [Fact]
    public void AFastFlick_CoastsPastWhereItWasReleased_ThenSnapsAndConfirms()
    {
        var p = Picker(count: 50);
        int completed = 0;
        p.EditCompleted += () => completed++;

        p.OnPointerDown(Timed(InputAction.Pressed, 190, 1000));
        p.OnPointerMove(Timed(InputAction.Moved, 170, 1016));
        p.OnPointerMove(Timed(InputAction.Moved, 140, 1032));
        p.OnPointerMove(Timed(InputAction.Moved, 110, 1048)); // 80 px up in 48 ms: about 2 items
        p.OnPointerUp(Timed(InputAction.Released, 110, 1050));

        Assert.True(p.IsAnimating);
        Assert.Equal(0, completed);

        Settle(p);

        Assert.True(p.SelectedIndex > 3, $"coasted only to {p.SelectedIndex}");
        Assert.Equal(1, completed);
        Assert.Equal(86, Draw(p).SingleText($"Item {p.SelectedIndex}").Rect.Y, 2); // rests on an item
    }

    [Fact]
    public void MoveWithoutAPress_IsNotHandled()
    {
        var p = Picker();

        Assert.False(p.Move(50, 50));
        Assert.Equal(0, p.SelectedIndex);
    }

    [Fact]
    public void WhileDragging_TheWheelScrollsContinuously_ButTheSelectionWaits()
    {
        var p = Picker();
        DragUp(p, 60);

        var ctx = Draw(p);

        // 1.5 items scrolled: "Item 1" sits 20 px above the center, "Item 2" 20 px below
        Assert.Equal(100 - 40 * 0.5f, ctx.SingleText("Item 1").Rect.Bottom, 2);
        Assert.Equal(0, p.SelectedIndex);
    }

    // ── Keyboard ──────────────────────────────────────────────────

    [Fact]
    public void UpAndDown_MoveTheSelectionOneItem_AndReportIt()
    {
        var p = Picker(selected: 4);
        var picks = new List<int>();
        var values = new List<ComponentValueChangedEventArgs>();
        p.SelectionChanged += (_, e) => picks.Add(e.SelectedIndex);
        p.ValueChanged += (_, e) => values.Add(e);

        Assert.True(p.Key(KumikoKey.Down));
        Assert.True(p.Key(KumikoKey.Up));
        Assert.True(p.Key(KumikoKey.Up));

        Assert.Equal(3, p.SelectedIndex);
        Assert.Equal(new[] { 5, 4, 3 }, picks);
        Assert.Equal(new object?[] { "Item 5", "Item 4", "Item 3" }, values.Select(v => v.NewValue));
    }

    [Fact]
    public void Keys_StopAtTheEnds()
    {
        var p = Picker(count: 3);

        p.Key(KumikoKey.Up);
        Assert.Equal(0, p.SelectedIndex);

        p.Key(KumikoKey.Down);
        p.Key(KumikoKey.Down);
        p.Key(KumikoKey.Down);
        Assert.Equal(2, p.SelectedIndex);
    }

    [Fact]
    public void Wheel_StepsTheSelectionOneItemPerNotch_WithoutConfirming()
    {
        var p = Picker(count: 5, selected: 1);
        int completed = 0;
        p.EditCompleted += () => completed++;

        Assert.True(p.OnScroll(new KumikoPointerEventArgs { X = 50, Y = 100, Action = InputAction.Scroll, ScrollDeltaY = -1 }));
        Assert.Equal(2, p.SelectedIndex);

        p.OnScroll(new KumikoPointerEventArgs { X = 50, Y = 100, Action = InputAction.Scroll, ScrollDeltaY = 0.5f });
        Assert.Equal(2, p.SelectedIndex); // half a notch: not yet
        p.OnScroll(new KumikoPointerEventArgs { X = 50, Y = 100, Action = InputAction.Scroll, ScrollDeltaY = 0.5f });
        Assert.Equal(1, p.SelectedIndex);

        p.OnScroll(new KumikoPointerEventArgs { X = 50, Y = 100, Action = InputAction.Scroll, ScrollDeltaY = 10 });
        Assert.Equal(0, p.SelectedIndex);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void Enter_WhileSettling_ConfirmsTheItemTheWheelIsSettlingOn()
    {
        var p = Picker();
        int completed = 0;
        p.EditCompleted += () => completed++;
        DragUp(p, 85);
        p.Up(50, 65);

        Assert.True(p.Key(KumikoKey.Enter));

        Assert.Equal(2, p.SelectedIndex);
        Assert.Equal(1, completed);
        Assert.False(p.IsAnimating);
    }

    [Fact]
    public void Enter_ConfirmsTheSelection_AndOtherKeysAreNotHandled()
    {
        var p = Picker();
        int completed = 0;
        p.EditCompleted += () => completed++;

        Assert.False(p.Key(KumikoKey.Left));
        Assert.True(p.Key(KumikoKey.Enter));
        Assert.Equal(1, completed);

        p.ClearSelection();
        Assert.False(p.Key(KumikoKey.Enter)); // nothing selected: nothing to confirm
        Assert.Equal(1, completed);
    }

    // ── Items and selection ───────────────────────────────────────

    [Fact]
    public void SelectedIndex_IsClamped_AndMovesTheWheel()
    {
        var p = Picker(count: 4);

        p.SelectedIndex = 99;
        Assert.Equal(3, p.SelectedIndex);
        Assert.Equal("Item 3", p.SelectedValue);

        p.SelectedIndex = -4; // -1 is "nothing selected"
        Assert.Equal(-1, p.SelectedIndex);
        Assert.Null(p.SelectedValue);
        Assert.Equal(86, Draw(p).SingleText("Item 0").Rect.Y, 2); // the wheel rests on the first item
    }

    [Fact]
    public void SetItems_KeepsTheSelectionInRange()
    {
        var p = Picker(count: 10, selected: 8);

        p.SetItems(new[] { "a", "b", "c" });

        Assert.Equal(2, p.SelectedIndex);
        Assert.Equal("c", p.SelectedValue);

        p.SetItems(Array.Empty<string>());
        Assert.Null(p.SelectedValue);
    }

    // ── Animation ─────────────────────────────────────────────────

    [Fact]
    public void UpdateAnimation_DoesNothingWhileTheFingerIsDown()
    {
        var p = Picker();
        DragUp(p, 30);

        Assert.False(p.UpdateAnimation(16));
    }

    [Fact]
    public void Release_WithoutVelocity_SnapsWithoutCoasting_AndTheConfirmedItemIsWhereItRests()
    {
        var p = Picker();
        int completed = 0;
        p.EditCompleted += () => completed++;
        DragUp(p, 30); // no timestamps: no velocity
        p.Up(50, 120);

        Settle(p);

        // The confirmed item is the one the wheel rests on: nothing moves afterward
        Assert.Equal(1, completed);
        Assert.Equal(1, p.SelectedIndex);
        Assert.False(p.UpdateAnimation(16));
        Assert.Equal(86, Draw(p).SingleText("Item 1").Rect.Y, 2);
    }

    [Fact]
    public void UpdateAnimation_WithoutVelocity_OnASnappedWheel_IsAlreadyAtRest()
    {
        var p = Picker(selected: 2);

        Assert.False(p.UpdateAnimation(16));
        Assert.Equal(2, p.SelectedIndex);
    }

    // ── Drawing ───────────────────────────────────────────────────

    [Fact]
    public void Draw_ShowsTheSelectionBandWithSeparators_ClippedToTheBounds()
    {
        var p = Picker();

        var ctx = Draw(p);

        var band = ctx.Fills.Single(o => o.Color.Equals(p.SelectionBandColor));
        Assert.Equal(new KumikoRect(0, 80, 100, 40), band.Rect);
        Assert.Equal((KumikoRect?)p.Bounds, band.Clip);
        var lines = ctx.Lines.ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(new[] { 80f, 120f }, lines.Select(l => l.Y1).ToArray());
        Assert.Equal(0, ctx.SaveDepth);
    }

    [Fact]
    public void Draw_SelectedItemIsBoldAndCentered_NeighborsFadeWithDistance()
    {
        var p = Picker(selected: 4);

        var ctx = Draw(p);

        var selected = ctx.SingleText("Item 4");
        Assert.True(selected.Font!.IsBold);
        Assert.Equal(p.SelectedTextColor.R, selected.Color.R);
        Assert.Equal(86, selected.Rect.Y, 2);
        var near = ctx.SingleText("Item 5");
        var far = ctx.SingleText("Item 6");
        Assert.False(near.Font!.IsBold);
        Assert.True(near.Color.A < 255);
        Assert.True(far.Color.A < near.Color.A);
        // Only the items near the center are drawn
        Assert.Empty(ctx.TextsEqual("Item 0"));
        Assert.Empty(ctx.TextsEqual("Item 8"));
        Assert.Equal(6, ctx.Texts.Count());
    }

    [Fact]
    public void Draw_CentersEachItemHorizontally()
    {
        var p = Picker();

        var text = Draw(p).SingleText("Item 0");

        Assert.Equal(50, text.Rect.X + text.Rect.Width / 2, 2);
    }

    [Fact]
    public void ApplyTheme_TakesTheColorsFromTheGridStyle()
    {
        var style = new KumikoStyle();
        var p = new DrawnScrollPicker();

        p.ApplyTheme(style);

        Assert.Equal(style.BackgroundColor, p.BackgroundColor);
        Assert.Equal(style.CellTextColor, p.SelectedTextColor);
        Assert.Equal(style.GridLineColor, p.SeparatorColor);
    }
}
