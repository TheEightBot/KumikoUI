using System.Diagnostics;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The filter popup (#49): every value is reachable by dragging, the wheel, the scrollbar and the keyboard;
/// a press toggles only when released without dragging; only visible rows are drawn; the search box
/// follows the theme; and a press outside the popup closes it and does nothing else.
/// </summary>
public class DrawnFilterPopupTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";
    }

    // Layout of a popup shown at (0, 0): the value list starts at y = 109, rows are 24 px, it is at most
    // 250 px tall, and the scrollbar is the rightmost 12 px of the 208 px wide list (x 202–214).
    private const float ListTop = 109;
    private const float RowHeight = 24;
    private const float ListHeight = 250;
    private const float ItemX = 60;
    private const float ScrollbarX = 208;

    private static (DrawnFilterPopup Popup, PopupManager Manager, KumikoSource Source, KumikoColumn Column) Open(int count)
    {
        var source = new KumikoSource { Culture = System.Globalization.CultureInfo.InvariantCulture };
        var column = new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 150 };
        source.SetColumns(new[] { column });
        source.SetItems(Enumerable.Range(0, count).Select(i => new Row { Name = $"V{i:D5}" }).ToList());
        var popup = new DrawnFilterPopup(column, source);
        var manager = new PopupManager();
        manager.Show(popup, new KumikoRect(0, 0, 150, 0));
        var focus = new DrawnFocusManager(); // the grid focuses the popup so its search box takes typing
        focus.Register(popup);
        focus.SetFocus(popup);
        return (popup, manager, source, column);
    }

    private static string Value(int i) => $"V{i:D5}";

    private static bool Pointer(PopupManager manager, InputAction action, float x, float y) =>
        manager.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = action });

    private static float RowY(int visibleRow) => ListTop + visibleRow * RowHeight + RowHeight / 2;

    private static List<string> DrawnValues(DrawnFilterPopup popup)
    {
        var ctx = new RecordingDrawingContext();
        popup.OnDraw(ctx);
        return ctx.Texts.Select(t => t.Text!).Where(t => t.StartsWith('V')).ToList();
    }

    private static void Key(DrawnFilterPopup popup, KumikoKey key, char? character = null) =>
        popup.OnKeyDown(new KumikoKeyEventArgs { Key = key, Character = character });

    private static float MaxOffset(int count) => count * RowHeight - ListHeight;

    // ── Large value lists ─────────────────────────────────────────

    [Fact]
    public void TenThousandValues_OpenQuickly_AndDrawOnlyTheVisibleRows()
    {
        var stopwatch = Stopwatch.StartNew();
        var (popup, _, _, _) = Open(10_000);
        var drawn = DrawnValues(popup);
        stopwatch.Stop();

        Assert.Equal(10_000, popup.ValueCount);
        Assert.InRange(drawn.Count, 10, 12); // 250 px of 24 px rows, not 10,000
        Assert.Equal(Value(0), drawn[0]);
        // The target is 100 ms; the bound is loose so a slow CI machine doesn't fail the build.
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"opening took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void TheWheel_ReachesTheLastValue()
    {
        var (popup, manager, _, _) = Open(10_000);

        manager.HandlePointer(new KumikoPointerEventArgs { X = ItemX, Y = RowY(2), Action = InputAction.Scroll, ScrollDeltaY = -1 });
        Assert.Equal(3 * RowHeight, popup.ListScrollOffset); // three rows per notch

        manager.HandlePointer(new KumikoPointerEventArgs { X = ItemX, Y = RowY(2), Action = InputAction.Scroll, ScrollDeltaY = -100_000 });
        Assert.Equal(MaxOffset(10_000), popup.ListScrollOffset);
        Assert.Equal(Value(9_999), DrawnValues(popup)[^1]);
    }

    [Fact]
    public void Dragging_ScrollsTheList_EvenOutsideThePopup_AndTogglesNothing()
    {
        var (popup, manager, _, _) = Open(1_000);

        Assert.True(Pointer(manager, InputAction.Pressed, ItemX, RowY(8)));
        Assert.True(Pointer(manager, InputAction.Moved, ItemX, RowY(8) - 100));
        Assert.True(Pointer(manager, InputAction.Moved, 600, RowY(8) - 400)); // outside: still captured
        Assert.True(Pointer(manager, InputAction.Released, 600, RowY(8) - 400));

        Assert.Equal(400, popup.ListScrollOffset);
        Assert.Equal(1_000, popup.CheckedValues.Count);
    }

    [Fact]
    public void TheScrollbar_DragsToTheEnd_AndAPressOnItsTrackJumps()
    {
        var (popup, manager, _, _) = Open(1_000);

        Pointer(manager, InputAction.Pressed, ScrollbarX, ListTop + 5); // on the thumb
        Pointer(manager, InputAction.Moved, ScrollbarX, ListTop + 1_000);
        Pointer(manager, InputAction.Released, ScrollbarX, ListTop + 1_000);
        Assert.Equal(MaxOffset(1_000), popup.ListScrollOffset);
        Assert.Equal(Value(999), DrawnValues(popup)[^1]);

        Pointer(manager, InputAction.Pressed, ScrollbarX, ListTop + 2); // the track above the thumb
        Pointer(manager, InputAction.Released, ScrollbarX, ListTop + 2);
        Assert.Equal(0, popup.ListScrollOffset);
        Assert.Equal(1_000, popup.CheckedValues.Count);
    }

    [Fact]
    public void TheKeyboard_ReachesEveryValue_AndSpaceToggles()
    {
        var (popup, _, _, _) = Open(1_000);

        Key(popup, KumikoKey.Down);
        for (int i = 0; i < 200; i++) Key(popup, KumikoKey.PageDown);
        Key(popup, KumikoKey.Space);

        Assert.Equal(MaxOffset(1_000), popup.ListScrollOffset);
        Assert.DoesNotContain(Value(999), popup.CheckedValues);
        Assert.Equal(999, popup.CheckedValues.Count);

        Key(popup, KumikoKey.Up);
        Key(popup, KumikoKey.Character, ' '); // a space character on the list toggles too
        Assert.DoesNotContain(Value(998), popup.CheckedValues);
        Assert.Equal("", popup.SearchText);
    }

    // ── Press, drag, release ──────────────────────────────────────

    [Fact]
    public void APressToggles_OnlyWhenReleasedWithoutDragging()
    {
        var (popup, manager, _, _) = Open(50);

        Pointer(manager, InputAction.Pressed, ItemX, RowY(1));
        Assert.Equal(50, popup.CheckedValues.Count); // nothing on press
        Pointer(manager, InputAction.Moved, ItemX, RowY(1) + 3); // within the threshold
        Pointer(manager, InputAction.Released, ItemX, RowY(1) + 3);
        Assert.DoesNotContain(Value(1), popup.CheckedValues);

        Pointer(manager, InputAction.Pressed, ItemX, RowY(4));
        Pointer(manager, InputAction.Moved, ItemX, RowY(4) - 30);
        Pointer(manager, InputAction.Released, ItemX, RowY(4) - 30);
        Assert.Equal(49, popup.CheckedValues.Count); // a drag scrolls instead
        Assert.Equal(30, popup.ListScrollOffset);
    }

    [Fact]
    public void Buttons_ActOnReleaseOverTheSameButton()
    {
        var (popup, manager, _, _) = Open(50);
        int canceled = 0;
        popup.FilterCancelled += () => canceled++;
        float buttonsY = ListTop + ListHeight + 9 + 3 + 10; // separator, then the OK/Cancel/Clear row
        float cancelX = 6 + (208 - 8) / 3f + 4 + 10;

        Pointer(manager, InputAction.Pressed, cancelX, buttonsY);
        Pointer(manager, InputAction.Released, ItemX, RowY(2)); // released elsewhere
        Assert.Equal(0, canceled);

        Pointer(manager, InputAction.Pressed, cancelX, buttonsY);
        Pointer(manager, InputAction.Released, cancelX, buttonsY);
        Assert.Equal(1, canceled);
    }

    // ── Search ────────────────────────────────────────────────────

    [Fact]
    public void Search_NarrowsTheList_AndResetsTheScroll()
    {
        var (popup, manager, _, _) = Open(1_000);
        manager.HandlePointer(new KumikoPointerEventArgs { X = ItemX, Y = RowY(2), Action = InputAction.Scroll, ScrollDeltaY = -5 });

        foreach (char c in "v0009")
            Key(popup, KumikoKey.Character, c); // applied once, when the list is next needed

        Assert.Equal(Enumerable.Range(90, 10).Select(Value), popup.VisibleValues);
        Assert.Equal(0, popup.ListScrollOffset);

        Key(popup, KumikoKey.Backspace);
        Assert.Equal(100, popup.VisibleValues.Count);
    }

    [Fact]
    public void SelectAll_AndDeselectAll_ApplyToTheSearchResults()
    {
        var (popup, manager, _, _) = Open(100);
        popup.SearchText = "V0001"; // V00010–V00019
        float rowY = 6 + 28 + 9 + 30 + 4 + 16;

        Pointer(manager, InputAction.Pressed, 150, rowY); // Deselect All
        Pointer(manager, InputAction.Released, 150, rowY);

        Assert.Equal(90, popup.CheckedValues.Count);
    }

    [Fact]
    public void TheSearchBox_FollowsTheTheme_AndThePopupHasOneShadow()
    {
        var (popup, manager, _, _) = Open(20);
        var dark = KumikoTheme.Create(KumikoThemeMode.Dark);
        popup.ApplyTheme(dark);

        var ctx = new RecordingDrawingContext();
        manager.DrawPopups(ctx);

        var searchFill = ctx.Fills.First(f => f.Rect.Y > 40 && f.Rect.Y < 50 && f.Rect.Height < 30);
        Assert.Equal(dark.BackgroundColor, searchFill.Color);
        Assert.DoesNotContain(ctx.Fills, f => f.Color == KumikoColor.White);
        Assert.Single(ctx.Fills, f => f.Color == new KumikoColor(0, 0, 0, 40));
    }

    [Fact]
    public void TheSearchBox_IsATextInputSession()
    {
        var (popup, _, _, _) = Open(100);

        popup.SearchTextInput.SetComposition("v0002", 5);
        popup.SearchTextInput.CommitComposition();

        Assert.Equal(10, popup.VisibleValues.Count);
    }

    // ── Popup manager ─────────────────────────────────────────────

    [Fact]
    public void APressOutside_ClosesThePopup_AndIsSwallowedThroughItsRelease()
    {
        var (popup, manager, _, _) = Open(20);
        var closed = new List<DrawnComponent>();
        manager.PopupClosed += closed.Add;

        Assert.True(Pointer(manager, InputAction.Pressed, 600, 600));
        Assert.False(manager.HasActivePopups);
        Assert.Equal(new DrawnComponent[] { popup }, closed);
        Assert.True(Pointer(manager, InputAction.Moved, 610, 600));
        Assert.True(Pointer(manager, InputAction.Released, 610, 600));

        Assert.False(Pointer(manager, InputAction.Pressed, 600, 600)); // the next press is the grid's
    }

    [Fact]
    public void ZOrder_KeepsIncreasing_AndAnchorsAreReadEachFrame()
    {
        var manager = new PopupManager();
        var first = new ProbeComponent { Bounds = new KumikoRect(0, 0, 50, 50) };
        var second = new ProbeComponent { Bounds = new KumikoRect(0, 0, 50, 50) };
        float anchorX = 10;
        manager.Show(first, new KumikoRect(0, 0, 10, 10));
        manager.Show(second, () => new KumikoRect(anchorX, 20, 10, 10));
        manager.Close(first);
        var third = new ProbeComponent { Bounds = new KumikoRect(0, 0, 50, 50) };
        manager.Show(third, new KumikoRect(0, 0, 10, 10));

        Assert.True(third.ZOrder > second.ZOrder);

        anchorX = 70; // the anchor scrolled
        manager.DrawPopups(new RecordingDrawingContext());
        Assert.Equal(new KumikoRect(70, 30, 50, 50), second.Bounds);
    }

    // ── Through the grid ──────────────────────────────────────────

    private sealed class Grid
    {
        public KumikoSource Source { get; } = new();
        public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name", Width = 150 };
        public KumikoColumn Other { get; } = new() { Header = "Other", PropertyName = "Name", Width = 150 };
        public KumikoSelectionModel Selection { get; }
        public KumikoInputController Input { get; } = new();
        public ScrollState Scroll { get; } = new() { ViewportWidth = 600, ViewportHeight = 400 };
        public KumikoStyle Style { get; } = new();
        public int Closed { get; private set; }

        public Grid()
        {
            Source.SetColumns(new[] { Name, Other });
            Source.SetItems(Enumerable.Range(0, 30).Select(i => new Row { Name = $"V{i:D5}" }).ToList());
            Selection = new KumikoSelectionModel(Source);
            Input.FilterPopupClosed += () => Closed++;
            new KumikoRenderer().Render(new RecordingDrawingContext(), Source, Scroll, Selection, Style);
        }

        public void Tap(float x, float y)
        {
            Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Pressed }, Scroll, Selection, Style, Source);
            Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Released }, Scroll, Selection, Style, Source);
        }

        public void OpenFilter(KumikoColumn column) =>
            Tap(column == Name ? 125 : 275, Style.HeaderHeight / 2);
    }

    [Fact]
    public void Grid_OutsideTap_ClosesTheFilterPopup_RaisesClosed_AndSelectsNothing()
    {
        var g = new Grid();
        g.OpenFilter(g.Name);
        Assert.True(g.Input.PopupManager.HasActivePopups);
        Assert.NotNull(g.Input.ActiveTextInput);

        g.Tap(450, 360); // a data cell, outside the popup

        Assert.False(g.Input.PopupManager.HasActivePopups);
        Assert.Equal(1, g.Closed);
        Assert.False(g.Selection.CurrentCell.IsValid);
        Assert.Empty(g.Selection.SelectedItems);
        Assert.Null(g.Input.ActiveTextInput);
    }

    [Fact]
    public void Grid_FilterPopup_FollowsItsColumnWhenTheGridScrolls()
    {
        var g = new Grid();
        g.Scroll.ContentWidth = 2_000;
        g.OpenFilter(g.Other);
        var popup = Assert.IsType<DrawnFilterPopup>(g.Input.PopupManager.HitTest(160, 100));
        float before = popup.Bounds.X;

        g.Scroll.OffsetX = 40;
        g.Input.PopupManager.DrawPopups(new RecordingDrawingContext());

        Assert.Equal(before - 40, popup.Bounds.X);
    }
}
