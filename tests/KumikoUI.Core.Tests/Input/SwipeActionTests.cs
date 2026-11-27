using System.Collections.ObjectModel;
using System.Windows.Input;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Swipe row actions (#329): how a touch drag on a row becomes a swipe rather than a pan, a row drag or a long press;
/// the items on each side; canceling in SwipeStarting; opening, closing, flinging and the full swipe; taps on an item and
/// elsewhere, scrolls and view changes that close the row; and an open edit committed first.
/// </summary>
/// <remarks>
/// <see cref="InputGrid"/> geometry: header 40 px, rows 36 px, a 300 x 256 viewport over 500 px of columns. Trailing
/// items Archive and Delete (72 px each, so the row opens 144 px to the left), one leading item, Flag (72 px).
/// </remarks>
public class SwipeActionTests
{
    private const KumikoPointerDeviceType Touch = KumikoPointerDeviceType.Touch;

    private sealed class Recorder : ICommand
    {
        public List<object?> Runs { get; } = new();
        public bool Enabled { get; set; } = true;
        public Action<object?>? OnRun { get; set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => Enabled;
        public void Execute(object? parameter)
        {
            Runs.Add(parameter);
            OnRun?.Invoke(parameter);
        }
    }

    private sealed class SwipeGrid
    {
        public InputGrid Grid { get; }
        public Recorder Flag { get; } = new();
        public Recorder Archive { get; } = new();
        public Recorder Delete { get; } = new();
        public ActionButtonDefinition FlagItem { get; }
        public ActionButtonDefinition ArchiveItem { get; }
        public ActionButtonDefinition DeleteItem { get; }
        public List<KumikoSwipeStartingEventArgs> Started { get; } = new();
        public List<KumikoSwipeEndedEventArgs> Ended { get; } = new();
        public List<KumikoHapticKind> Haptics { get; } = new();

        public SwipeGrid(bool leading = true, bool trailing = true, Action<InputGrid>? configure = null, int rows = 30,
            IList<InputGrid.Item>? items = null)
        {
            FlagItem = new ActionButtonDefinition { Label = "Flag", Command = Flag };
            ArchiveItem = new ActionButtonDefinition { Label = "Archive", Command = Archive };
            DeleteItem = new ActionButtonDefinition { Label = "Delete", Command = Delete };
            Grid = new InputGrid(rows, configure: g =>
            {
                if (items != null) g.Source.SetItems(items);
                g.Input.AllowSwiping = true;
                if (leading) g.Input.LeadingSwipeItems.Add(FlagItem);
                if (trailing)
                {
                    g.Input.TrailingSwipeItems.Add(ArchiveItem);
                    g.Input.TrailingSwipeItems.Add(DeleteItem);
                }
                g.Input.SwipeStarting += (_, e) => Started.Add(e);
                g.Input.SwipeEnded += (_, e) => Ended.Add(e);
                g.Input.HapticFeedbackRequested += (_, e) => Haptics.Add(e.Kind);
                configure?.Invoke(g);
            });
        }

        public KumikoInputController Input => Grid.Input;
        public RowSwipe Swipe => Grid.Input.Swipe;
        public float RowY(int row) => Grid.RowY(row);

        public KumikoPointerEventArgs Touched(InputAction action, float x, float y, long timestampMs = 0) =>
            Grid.Pointer(action, x, y, timestampMs: timestampMs, deviceType: Touch);

        /// <summary>A finger drags row <paramref name="row"/> from x 150 by <paramref name="dx"/> in 10 px steps, 16 ms apart, and holds still.</summary>
        public void Drag(int row, float dx, float fromX = 150, long startMs = 1000, float dy = 0)
        {
            float y = RowY(row);
            Touched(InputAction.Pressed, fromX, y, startMs);
            int steps = Math.Max(1, (int)MathF.Ceiling(Math.Max(MathF.Abs(dx), MathF.Abs(dy)) / 10f));
            for (int i = 1; i <= steps; i++)
                Touched(InputAction.Moved, fromX + dx * i / steps, y + dy * i / steps, startMs + i * 16L);
        }

        /// <summary><see cref="Drag"/>, then the finger rests (so the release does not fling) and lifts.</summary>
        public void Lift(int row, float dx, float fromX = 150, float dy = 0)
        {
            Drag(row, dx, fromX, dy: dy);
            Touched(InputAction.Released, fromX + dx, RowY(row) + dy, 5000);
        }
    }

    private static float Offset(SwipeGrid s) => s.Swipe.CurrentOffset(s.Input.Animations);

    // ── Gesture: swipe vs pan vs row drag vs long press ──

    [Fact]
    public void HorizontalTouchDrag_OnARow_SwipesTheRow_InsteadOfPanning()
    {
        var s = new SwipeGrid();

        s.Drag(2, -60);

        Assert.Equal(SwipePhase.Tracking, s.Swipe.Phase);
        Assert.Same(s.Grid.Items[2], s.Swipe.Item);
        Assert.Equal(-60, Offset(s), 0.01f); // the row stays under the finger from where it pressed
        Assert.Equal(0, s.Grid.Scroll.OffsetX);
        Assert.Equal(0, s.Grid.Scroll.OffsetY);
        var started = Assert.Single(s.Started);
        Assert.Equal((2, KumikoSwipeDirection.Trailing), (started.RowIndex, started.Direction));
        Assert.Same(s.Grid.Items[2], started.DataItem);
    }

    [Fact]
    public void VerticalTouchDrag_PansAsBefore()
    {
        var s = new SwipeGrid();

        s.Lift(4, 0, dy: -60);

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.True(s.Grid.Scroll.OffsetY > 0);
        Assert.Empty(s.Started);
    }

    [Fact]
    public void DiagonalTouchDrag_PansBothWays_ForHorizontalScrolling()
    {
        var s = new SwipeGrid();

        s.Lift(4, -40, dy: -30); // not twice as wide as tall

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.True(s.Grid.Scroll.OffsetX > 0);
        Assert.True(s.Grid.Scroll.OffsetY > 0);
    }

    [Fact]
    public void AVerticalFirstPan_NeverTurnsIntoASwipe()
    {
        var s = new SwipeGrid();
        float y = s.RowY(4);

        s.Touched(InputAction.Pressed, 150, y);
        s.Touched(InputAction.Moved, 150, y - 20); // a pan
        s.Touched(InputAction.Moved, 80, y - 22); // then sideways
        s.Touched(InputAction.Released, 80, y - 22);

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.True(s.Grid.Scroll.OffsetX > 0);
    }

    [Fact]
    public void MouseDrag_NeverSwipes()
    {
        var s = new SwipeGrid();

        s.Grid.Press(150, s.RowY(2));
        s.Grid.Move(90, s.RowY(2));
        s.Grid.Release(90, s.RowY(2));

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Empty(s.Started);
    }

    [Fact]
    public void WithSwipingOff_AHorizontalTouchDragScrollsSideways()
    {
        var s = new SwipeGrid(configure: g => g.Input.AllowSwiping = false);

        s.Lift(2, -60);

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Equal(40, s.Grid.Scroll.OffsetX, 0.01f); // from the move that passed the 10 px threshold
    }

    [Fact]
    public void ASideWithoutItems_Pans()
    {
        var s = new SwipeGrid(leading: false);
        s.Grid.Scroll.ScrollTo(100, 0);

        s.Lift(2, 60); // toward the end edge: leading, which has no items

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Equal(60, s.Grid.Scroll.OffsetX, 0.01f);
    }

    [Fact]
    public void EachSide_RevealsItsOwnItems()
    {
        var s = new SwipeGrid();

        s.Lift(1, 60);
        Assert.Equal((SwipePhase.Open, KumikoSwipeDirection.Leading), (s.Swipe.Phase, s.Swipe.Direction));
        Assert.Equal(72, Offset(s), 0.01f); // one leading item
        s.Input.CloseSwipe();

        s.Lift(1, -60);
        Assert.Equal((SwipePhase.Open, KumikoSwipeDirection.Trailing), (s.Swipe.Phase, s.Swipe.Direction));
        Assert.Equal(-144, Offset(s), 0.01f); // two trailing items
    }

    [Fact]
    public void SwipeStarting_Canceled_LeavesTheRowAndPans()
    {
        var s = new SwipeGrid(configure: g => g.Input.SwipeStarting += (_, e) => e.Cancel = e.RowIndex == 3);

        s.Lift(3, -60);
        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.True(s.Grid.Scroll.OffsetX > 0);
        Assert.Empty(s.Ended); // nothing started, so nothing ends

        s.Lift(2, -60);
        Assert.Equal(SwipePhase.Open, s.Swipe.Phase); // another row still swipes
    }

    [Fact]
    public void LongPress_BeforeMoving_StartsARowDrag_NotASwipe()
    {
        var s = new SwipeGrid(configure: g => g.Style.AllowRowDragDrop = true);
        float y = s.RowY(1);

        s.Touched(InputAction.Pressed, 150, y);
        s.Touched(InputAction.LongPress, 150, y);
        s.Touched(InputAction.Moved, 90, y);

        Assert.Equal(1, s.Input.DragRowIndex);
        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
    }

    [Fact]
    public void LongPress_BeforeMoving_StillAsksForTheContextMenu()
    {
        var s = new SwipeGrid();
        int requests = 0;
        s.Input.ContextRequested += (_, _) => requests++;

        s.Touched(InputAction.Pressed, 150, s.RowY(1));
        s.Touched(InputAction.LongPress, 150, s.RowY(1));

        Assert.Equal(1, requests);
    }

    [Fact]
    public void LongPress_DuringASwipe_BelongsToTheSwipe()
    {
        var s = new SwipeGrid(configure: g => g.Style.AllowRowDragDrop = true);
        int requests = 0;
        s.Input.ContextRequested += (_, _) => requests++;

        s.Drag(1, -12); // within the host's long-press tolerance
        var longPress = s.Touched(InputAction.LongPress, 138, s.RowY(1));

        Assert.True(longPress.Handled);
        Assert.Equal(-1, s.Input.DragRowIndex);
        Assert.Equal(0, requests);
        Assert.Equal(SwipePhase.Tracking, s.Swipe.Phase);
    }

    [Fact]
    public void Swiping_DoesNotSelect()
    {
        var s = new SwipeGrid();

        s.Lift(2, -60);
        s.Lift(2, 0); // a tap on the open row closes it

        Assert.False(s.Grid.Selection.CurrentCell.IsValid);
        Assert.Empty(s.Grid.Selection.SelectedItems);
    }

    // ── Tracking, open, close, fling, full swipe ──

    [Fact]
    public void TheRow_StopsAtItsItems_WithARubberBandPastThem()
    {
        var s = new SwipeGrid();

        s.Drag(2, -144 - 100);

        Assert.Equal(-(144 + 100 * RowSwipe.RubberBandFactor), Offset(s), 0.01f);
    }

    [Fact]
    public void TheRow_NeverMovesPastClosed()
    {
        var s = new SwipeGrid();

        s.Drag(2, -40);
        s.Touched(InputAction.Moved, 200, s.RowY(2), 2000); // back past where it pressed

        Assert.Equal(0, Offset(s), 0.01f);
    }

    [Fact]
    public void Released_PastHalfAnItem_Opens_WithoutEndingTheSwipe()
    {
        var s = new SwipeGrid();

        s.Lift(2, -40); // 36 is half an item

        Assert.Equal(SwipePhase.Open, s.Swipe.Phase);
        Assert.Equal(-144, Offset(s), 0.01f);
        Assert.Empty(s.Ended);
    }

    [Fact]
    public void Released_ShortOfHalfAnItem_Closes_AndEndsTheSwipe()
    {
        var s = new SwipeGrid();

        s.Lift(2, -30);

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        var ended = Assert.Single(s.Ended);
        Assert.Same(s.Grid.Items[2], ended.DataItem);
        Assert.Equal(KumikoSwipeDirection.Trailing, ended.Direction);
        Assert.Null(ended.InvokedItem);
    }

    [Fact]
    public void AFling_OpensTheRow_ShortOfHalfAnItem()
    {
        var s = new SwipeGrid();
        float y = s.RowY(2);

        s.Touched(InputAction.Pressed, 150, y, 1000);
        s.Touched(InputAction.Moved, 138, y, 1010);
        s.Touched(InputAction.Moved, 126, y, 1020); // 1.2 px per ms
        s.Touched(InputAction.Released, 126, y, 1024); // lifted where it last moved

        Assert.Equal(SwipePhase.Open, s.Swipe.Phase);
    }

    [Fact]
    public void AFlingBack_ClosesAnOpenRow_PastHalfAnItem()
    {
        var s = new SwipeGrid();
        s.Lift(2, -60);
        float y = s.RowY(2);

        s.Touched(InputAction.Pressed, 100, y, 6000);
        s.Touched(InputAction.Moved, 112, y, 6010); // drags the open row on, toward closed
        s.Touched(InputAction.Moved, 124, y, 6020);
        Assert.Equal(SwipePhase.Tracking, s.Swipe.Phase);
        Assert.Equal(-120, Offset(s), 0.01f);
        s.Touched(InputAction.Released, 124, y, 6024);

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Null(Assert.Single(s.Ended).InvokedItem);
    }

    [Fact]
    public void AFullSwipe_RunsTheOutermostItem_WithTheRowsItem_AndCloses()
    {
        var s = new SwipeGrid(configure: g => g.Input.FullSwipeMode = KumikoFullSwipeMode.Trailing);

        s.Drag(2, -200); // past 60% of the 300 px row
        Assert.True(s.Swipe.FullSwipeArmed);
        Assert.Equal(-200, Offset(s), 0.01f); // with a full swipe the row follows the finger past its items
        Assert.Equal(new[] { KumikoHapticKind.SwipeThreshold }, s.Haptics);
        s.Touched(InputAction.Released, -50, s.RowY(2), 5000);

        Assert.Equal(new object?[] { s.Grid.Items[2] }, s.Archive.Runs);
        Assert.Empty(s.Delete.Runs);
        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Same(s.ArchiveItem, Assert.Single(s.Ended).InvokedItem);
    }

    [Fact]
    public void BackingOutOfAFullSwipe_DisarmsIt_WithAnotherHaptic()
    {
        var s = new SwipeGrid(configure: g => g.Input.FullSwipeMode = KumikoFullSwipeMode.Both);

        s.Drag(2, -200);
        s.Touched(InputAction.Moved, 50, s.RowY(2), 3000); // back to 100 px
        s.Touched(InputAction.Released, 50, s.RowY(2), 5000);

        Assert.Equal(new[] { KumikoHapticKind.SwipeThreshold, KumikoHapticKind.SwipeThreshold }, s.Haptics);
        Assert.Empty(s.Archive.Runs);
        Assert.Equal(SwipePhase.Open, s.Swipe.Phase);
    }

    [Fact]
    public void AFullSwipe_OnASideWithoutIt_StopsAtTheItems()
    {
        var s = new SwipeGrid(configure: g => g.Input.FullSwipeMode = KumikoFullSwipeMode.Leading);

        s.Lift(2, -250);

        Assert.Empty(s.Archive.Runs);
        Assert.Empty(s.Haptics);
        Assert.Equal(SwipePhase.Open, s.Swipe.Phase);
    }

    [Fact]
    public void AFullSwipe_DoesNotRunADisabledOutermostItem()
    {
        var s = new SwipeGrid(configure: g => g.Input.FullSwipeMode = KumikoFullSwipeMode.Trailing);
        s.Archive.Enabled = false;

        s.Lift(2, -250);

        Assert.Empty(s.Archive.Runs);
        Assert.Equal(SwipePhase.Open, s.Swipe.Phase);
    }

    [Fact]
    public void ACanceledGesture_SettlesWithoutRunningAnything()
    {
        var s = new SwipeGrid(configure: g => g.Input.FullSwipeMode = KumikoFullSwipeMode.Trailing);

        s.Drag(2, -200);
        var cancel = s.Touched(InputAction.Cancelled, -50, s.RowY(2), 5000);

        Assert.True(cancel.Handled);
        Assert.Empty(s.Archive.Runs);
        Assert.Equal(SwipePhase.Open, s.Swipe.Phase); // past half an item: it stays open
    }

    // ── The open row: taps, scrolls, view changes ──

    [Fact]
    public void ATapOnAnItem_RunsIt_WithTheRowsItem_AndCloses()
    {
        var s = new SwipeGrid();
        s.Lift(2, -60);

        // Trailing: Archive (item 0) at 228-300, Delete at 156-228
        Assert.Equal(HitRegion.SwipeItem, s.Input.HitTester.HitTest(170, s.RowY(2), s.Grid.Scroll, s.Grid.Style,
            s.Grid.Source.Columns, s.Grid.Source.RowCount, s.Grid.Source, Touch).Region);
        s.Touched(InputAction.Pressed, 170, s.RowY(2));
        Assert.Equal(1, s.Swipe.PressedItem);
        s.Touched(InputAction.Released, 170, s.RowY(2));

        Assert.Equal(new object?[] { s.Grid.Items[2] }, s.Delete.Runs);
        Assert.Empty(s.Archive.Runs);
        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Same(s.DeleteItem, Assert.Single(s.Ended).InvokedItem);
        Assert.False(s.Grid.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void ATapOnAnItem_UsesItsCommandParameter_AndThenItsAction()
    {
        int actions = 0;
        var s = new SwipeGrid(configure: g =>
        {
            g.Input.TrailingSwipeItems.Clear();
            g.Input.TrailingSwipeItems.Add(new ActionButtonDefinition { Label = "Go", CommandParameter = "p", Action = () => actions++ });
        });
        var recorder = new Recorder();
        ((ActionButtonDefinition)s.Input.TrailingSwipeItems[0]).Command = recorder;

        s.Lift(2, -60);
        s.Touched(InputAction.Pressed, 280, s.RowY(2));
        s.Touched(InputAction.Released, 280, s.RowY(2));

        Assert.Equal(new object?[] { "p" }, recorder.Runs);
        Assert.Equal(1, actions);
    }

    [Fact]
    public void ATapOnADisabledItem_DoesNothing_AndTheRowStaysOpen()
    {
        var s = new SwipeGrid();
        s.Delete.Enabled = false;
        s.Lift(2, -60);

        s.Touched(InputAction.Pressed, 170, s.RowY(2));
        s.Touched(InputAction.Released, 170, s.RowY(2));

        Assert.Empty(s.Delete.Runs);
        Assert.Equal(SwipePhase.Open, s.Swipe.Phase);
        Assert.Empty(s.Ended);
    }

    [Fact]
    public void APressOnAnItem_ReleasedElsewhere_RunsNothing()
    {
        var s = new SwipeGrid();
        s.Lift(2, -60);

        s.Touched(InputAction.Pressed, 170, s.RowY(2));
        s.Touched(InputAction.Moved, 172, s.RowY(2) + 4);
        s.Touched(InputAction.Released, 240, s.RowY(2) + 4); // over Archive now

        Assert.Empty(s.Delete.Runs);
        Assert.Empty(s.Archive.Runs);
    }

    [Fact]
    public void ATapOnTheOpenRowsCells_ClosesIt_WithoutSelecting()
    {
        var s = new SwipeGrid();
        int taps = 0;
        s.Input.RowTapped += (_, _) => taps++;
        s.Lift(2, -60);

        s.Touched(InputAction.Pressed, 60, s.RowY(2));
        s.Touched(InputAction.Released, 60, s.RowY(2));

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Null(Assert.Single(s.Ended).InvokedItem);
        Assert.Equal(0, taps);
        Assert.False(s.Grid.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void ATapElsewhere_ClosesTheOpenRow_AndActsAsUsual()
    {
        var s = new SwipeGrid();
        s.Lift(2, -60);

        s.Touched(InputAction.Pressed, 60, s.RowY(4));
        Assert.Equal(SwipePhase.None, s.Swipe.Phase); // at the press
        s.Touched(InputAction.Released, 60, s.RowY(4));

        Assert.Null(Assert.Single(s.Ended).InvokedItem);
        Assert.Equal(4, s.Grid.Selection.CurrentCell.Row);
        Assert.Empty(s.Archive.Runs);
        Assert.Empty(s.Delete.Runs);
    }

    [Fact]
    public void AVerticalPanOnTheOpenRow_ClosesIt_AndScrolls()
    {
        var s = new SwipeGrid();
        s.Lift(2, -60);

        s.Lift(2, 0, fromX: 60, dy: 40);

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Single(s.Ended);
    }

    [Fact]
    public void AScrollWheel_ClosesTheOpenRow()
    {
        var s = new SwipeGrid();
        s.Lift(2, -60);

        s.Grid.Pointer(InputAction.Scroll, 60, s.RowY(4), scrollDy: -1);

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Single(s.Ended);
    }

    [Fact]
    public void ASort_ClosesTheOpenRow_WithoutRunningAnything()
    {
        var s = new SwipeGrid();
        s.Lift(2, -60);
        Assert.True(s.Input.TryGetSwipeFrame(s.Grid.Source, out _, out _, out _));

        s.Grid.Source.ToggleSort(s.Grid.Score);
        s.Grid.Source.ToggleSort(s.Grid.Score); // descending: the rows move

        Assert.False(s.Input.TryGetSwipeFrame(s.Grid.Source, out _, out _, out _)); // no longer drawn
        s.Touched(InputAction.Pressed, 60, s.RowY(4)); // the next event closes it
        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Null(Assert.Single(s.Ended).InvokedItem);
        Assert.Empty(s.Archive.Runs);
        Assert.Empty(s.Delete.Runs);
    }

    [Fact]
    public void AnItemThatRemovesTheRow_ClosesIt_AndTheNextRowIsNotSwiped()
    {
        var items = new ObservableCollection<InputGrid.Item>(Enumerable.Range(0, 20)
            .Select(i => new InputGrid.Item { Name = $"N{i:D2}", Score = i }));
        var s = new SwipeGrid(items: items);
        s.Delete.OnRun = item => items.Remove((InputGrid.Item)item!);
        var removed = items[2];

        s.Lift(2, -60);
        s.Touched(InputAction.Pressed, 170, s.RowY(2));
        s.Touched(InputAction.Released, 170, s.RowY(2));

        Assert.DoesNotContain(removed, items);
        Assert.Same(s.DeleteItem, Assert.Single(s.Ended).InvokedItem);
        Assert.False(s.Input.TryGetSwipeFrame(s.Grid.Source, out _, out _, out _));
        s.Touched(InputAction.Pressed, 60, s.RowY(2));
        s.Touched(InputAction.Released, 60, s.RowY(2));
        Assert.Single(s.Ended); // nothing more to close
        Assert.Equal(2, s.Grid.Selection.CurrentCell.Row); // the tap selects the row that moved up
    }

    [Fact]
    public void CloseSwipe_ClosesTheOpenRow_AndEndsTheSwipeOnce()
    {
        var s = new SwipeGrid();
        s.Lift(2, -60);

        s.Input.CloseSwipe();
        s.Input.CloseSwipe();

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Null(Assert.Single(s.Ended).InvokedItem);
    }

    [Fact]
    public void TurningSwipingOff_ClosesTheOpenRow()
    {
        var s = new SwipeGrid();
        s.Lift(2, -60);

        s.Input.AllowSwiping = false;

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Single(s.Ended);
    }

    [Fact]
    public void DisablingTheGrid_EndsASwipeInProgress()
    {
        var s = new SwipeGrid();
        s.Drag(2, -60);

        s.Input.IsEnabled = false;

        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Single(s.Ended);
    }

    [Fact]
    public void AnotherRowsSwipe_ClosesTheOpenOne()
    {
        var s = new SwipeGrid();
        s.Lift(2, -60);

        s.Lift(4, -60);

        Assert.Same(s.Grid.Items[4], s.Swipe.Item);
        Assert.Equal(SwipePhase.Open, s.Swipe.Phase);
        Assert.Same(s.Grid.Items[2], Assert.Single(s.Ended).DataItem);
    }

    [Fact]
    public void ADragOnTheOpenRow_MovesItOnFromWhereItIs()
    {
        var s = new SwipeGrid();
        s.Lift(2, -60);

        s.Drag(2, -20, fromX: 60);

        Assert.Equal(SwipePhase.Tracking, s.Swipe.Phase);
        Assert.Equal(-(144 + 20 * RowSwipe.RubberBandFactor), Offset(s), 0.01f);
        Assert.Single(s.Started); // the same swipe
    }

    // ── Editing ──

    [Fact]
    public void AnOpenEdit_IsCommittedBeforeTheSwipe()
    {
        var s = new SwipeGrid();
        s.Grid.Select(5, 0);
        s.Grid.Type('Z');
        Assert.True(s.Grid.Edit.IsEditing);

        s.Drag(2, -60);

        Assert.False(s.Grid.Edit.IsEditing);
        Assert.Equal("Z", s.Grid.Items[5].Name);
        Assert.Equal(SwipePhase.Tracking, s.Swipe.Phase);
    }

    [Fact]
    public void ARefusedCommit_CancelsTheSwipe_AndKeepsTheEditor()
    {
        var s = new SwipeGrid(configure: g => g.Edit.CellValidator = (_, _, _) => CellValidationResult.Error("no"));
        s.Grid.Select(5, 0);
        s.Grid.Type('Z');

        s.Drag(2, -60);

        Assert.True(s.Grid.Edit.IsEditing);
        Assert.Equal(SwipePhase.None, s.Swipe.Phase);
        Assert.Empty(s.Started);
    }

    // ── Animation ──

    [Fact]
    public void WithAnimations_TheRowSnapsOpenAndShut_ThroughTheFrameLoop()
    {
        var s = new SwipeGrid(configure: g => g.Input.AnimationsEnabled = true);

        s.Lift(2, -60);
        Assert.True(s.Input.IsAnimating);
        Assert.Equal(-60, Offset(s), 0.01f); // it leaves from where the finger lifted
        s.Input.UpdateAnimations(InteractionAnimations.SwipeSnapMs / 2);
        Assert.InRange(Offset(s), -143, -61);
        s.Input.UpdateAnimations(InteractionAnimations.SwipeSnapMs);
        Assert.False(s.Input.IsAnimating);
        Assert.Equal(-144, Offset(s), 0.01f);

        s.Input.CloseSwipe();
        Assert.Single(s.Ended); // closed at once; it only slides
        Assert.True(s.Input.TryGetSwipeFrame(s.Grid.Source, out _, out _, out _));
        s.Input.UpdateAnimations(InteractionAnimations.SwipeSnapMs);
        Assert.False(s.Input.TryGetSwipeFrame(s.Grid.Source, out _, out _, out _));
    }

    [Fact]
    public void WithTheStylesAnimationsOff_TheRowSnapsAtOnce()
    {
        var s = new SwipeGrid(configure: g =>
        {
            g.Input.AnimationsEnabled = true;
            g.Style.EnableAnimations = false;
        });

        s.Lift(2, -60);

        Assert.False(s.Input.IsAnimating);
        Assert.Equal(-144, Offset(s), 0.01f);
    }

    [Fact]
    public void SwipeItemWidth_SetsHowFarTheRowOpens()
    {
        var s = new SwipeGrid(configure: g => g.Input.SwipeItemWidth = 90);

        s.Lift(2, -60);
        Assert.Equal(-180, Offset(s), 0.01f);

        s.Input.SwipeItemWidth = -1;
        Assert.Equal(72, s.Input.SwipeItemWidth);
    }
}
