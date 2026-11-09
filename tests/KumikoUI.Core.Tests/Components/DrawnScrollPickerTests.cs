using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The drawn scroll-wheel picker: drag to scroll, snap to the nearest item on release, keyboard
/// stepping, coasting and snapping animation, and what the wheel draws
/// (<see cref="PickerSelectionChangedEventArgs"/>).
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

    // ── Drag and snap ─────────────────────────────────────────────

    [Fact]
    public void DragAndRelease_SnapsToTheNearestItem_AndReportsTheChange()
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

        Assert.Equal(2, p.SelectedIndex);
        Assert.Equal("Item 2", p.SelectedValue);
        Assert.Equal(2, Assert.Single(picks).SelectedIndex);
        Assert.Equal((0, 2), ((int)values[0].OldValue!, (int)values[0].NewValue!));
        Assert.Equal(1, completed);
    }

    [Fact]
    public void ReleaseOnTheSameItem_ChangesNothing_ButStillCompletesTheEdit()
    {
        var p = Picker(selected: 3);
        int picks = 0, completed = 0;
        p.SelectionChanged += (_, _) => picks++;
        p.EditCompleted += () => completed++;

        DragUp(p, 10);
        p.Up(50, 140);

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
        Assert.Equal(4, p.SelectedIndex);

        p.Down(50, 0);
        p.Move(50, 5000);
        p.Up(50, 5000);
        Assert.Equal(0, p.SelectedIndex);
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
    public void OtherKeys_AreNotHandled()
    {
        var p = Picker();

        Assert.False(p.Key(KumikoKey.Left));
        Assert.False(p.Key(KumikoKey.Enter));
    }

    // ── Items and selection ───────────────────────────────────────

    [Fact]
    public void SelectedIndex_IsClamped_AndMovesTheWheel()
    {
        var p = Picker(count: 4);

        p.SelectedIndex = 99;
        Assert.Equal(3, p.SelectedIndex);
        Assert.Equal("Item 3", p.SelectedValue);

        p.SelectedIndex = -4;
        Assert.Equal(0, p.SelectedIndex);
        Assert.Equal(86, Draw(p).SingleText("Item 0").Rect.Y, 2);
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
    public void UpdateAnimation_CoastsAfterAFlick_ThenComesToRestOnAnItem()
    {
        var p = Picker();
        var picks = new List<int>();
        p.SelectionChanged += (_, e) => picks.Add(e.SelectedIndex);
        DragUp(p, 20);
        p.Up(50, 130);

        int frames = 0;
        while (p.UpdateAnimation(16) && frames < 1000) frames++;

        Assert.InRange(frames, 1, 999);
        Assert.True(p.SelectedIndex > 0);
        Assert.Equal(p.SelectedIndex, picks[^1]);
        // At rest the selected item is exactly on the center line
        var selected = Draw(p).SingleText($"Item {p.SelectedIndex}");
        Assert.Equal(86, selected.Rect.Y, 2);
        Assert.False(p.UpdateAnimation(16));
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
