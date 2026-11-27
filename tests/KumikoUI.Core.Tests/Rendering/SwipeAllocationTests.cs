using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// A swiped row allocates nothing per frame (#329): a row that follows the finger, one settled open with its items, a
/// full swipe and a snap stepped by the frame loop draw from reused paints, like the rest of the render loop.
/// </summary>
[Collection(nameof(RenderAllocationCollection))]
public class SwipeAllocationTests
{
    private const int WarmUpFrames = 20;
    private const int MeasuredFrames = 100;

    private sealed class Row
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public string City { get; set; } = "";
    }

    private sealed class Grid
    {
        public KumikoSource Source { get; } = new();
        public KumikoStyle Style { get; } = new();
        public ScrollState Scroll { get; } = new() { ViewportWidth = 330, ViewportHeight = 600 };
        public KumikoSelectionModel Selection { get; }
        public KumikoRenderer Renderer { get; } = new();
        public KumikoInputController Input { get; } = new() { AllowSwiping = true };

        public Grid()
        {
            Source.SetColumns(new[]
            {
                new KumikoColumn { Header = "Name", PropertyName = nameof(Row.Name), Width = 110, IsFrozen = true },
                new KumikoColumn { Header = "Quantity", PropertyName = nameof(Row.Quantity), Width = 110, ColumnType = KumikoColumnType.Numeric },
                new KumikoColumn { Header = "City", PropertyName = nameof(Row.City), Width = 110, IsFrozenRight = true },
            });
            Source.SetItems(Enumerable.Range(0, 500).Select(i => new Row { Name = $"Row {i}", Quantity = i, City = "Oslo" }).ToList());
            Selection = new KumikoSelectionModel(Source);
            Selection.SetCurrentCell(3, 1);
            Input.LeadingSwipeItems.Add(new ActionButtonDefinition { Label = "Flag" });
            Input.TrailingSwipeItems.Add(new ActionButtonDefinition { Label = "Archive" });
            Input.TrailingSwipeItems.Add(new ActionButtonDefinition { Label = "Delete", BackgroundColor = new KumikoColor(220, 53, 69) });
            Renderer.InputController = Input;
            Render(new NoOpDrawingContext());
        }

        public void Render(IDrawingContext ctx) => Renderer.Render(ctx, Source, Scroll, Selection, Style);

        public KumikoPointerEventArgs Event(InputAction action, float x, long timestampMs) => new()
        {
            Action = action, X = x, Y = Style.HeaderHeight + 3 * Style.RowHeight + 10,
            DeviceType = KumikoPointerDeviceType.Touch, TimestampMs = timestampMs,
        };

        public void Touch(KumikoPointerEventArgs e) => Input.HandlePointer(e, Scroll, Selection, Style, Source);

        public void Touch(InputAction action, float x, long timestampMs) => Touch(Event(action, x, timestampMs));

        /// <summary>A finger presses row 3 at x 200 and drags it to <paramref name="toX"/>.</summary>
        public void Drag(float toX)
        {
            Touch(InputAction.Pressed, 200, 1000);
            Touch(InputAction.Moved, 200 + (toX - 200) / 2, 1016);
            Touch(InputAction.Moved, toX, 1032);
        }
    }

    private static long Measure(Action<IDrawingContext> render)
    {
        var ctx = new NoOpDrawingContext();
        for (int i = 0; i < WarmUpFrames; i++)
            render(ctx);

        long least = long.MaxValue;
        for (int attempt = 0; attempt < 3 && least > 0; attempt++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < MeasuredFrames; i++)
                render(ctx);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        return least;
    }

    [Fact]
    public void ARowFollowingTheFinger_AllocatesNothingPerFrame()
    {
        var grid = new Grid();
        grid.Drag(120);
        Assert.Equal(SwipePhase.Tracking, grid.Input.Swipe.Phase);
        int frame = 0;
        var left = grid.Event(InputAction.Moved, 110, 2000);
        var right = grid.Event(InputAction.Moved, 130, 2016);

        // The finger keeps moving between frames, as it does on a device (the events are the host's to allocate)
        Assert.Equal(0, Measure(ctx =>
        {
            grid.Touch(frame++ % 2 == 0 ? left : right);
            grid.Render(ctx);
        }));
    }

    [Fact]
    public void AnOpenRowWithItsItems_AllocatesNothingPerFrame()
    {
        var grid = new Grid();
        grid.Drag(120);
        grid.Touch(InputAction.Released, 120, 5000);
        Assert.Equal(SwipePhase.Open, grid.Input.Swipe.Phase);

        Assert.Equal(0, Measure(grid.Render));
    }

    [Fact]
    public void AFullSwipe_AllocatesNothingPerFrame()
    {
        var grid = new Grid();
        grid.Input.FullSwipeMode = KumikoFullSwipeMode.Both;
        grid.Drag(-40);
        Assert.True(grid.Input.Swipe.FullSwipeArmed);

        Assert.Equal(0, Measure(grid.Render));
    }

    [Fact]
    public void ASnap_SteppedByTheFrameLoop_AllocatesNothingPerFrame()
    {
        var grid = new Grid();
        grid.Input.AnimationsEnabled = true;
        grid.Drag(160);
        grid.Touch(InputAction.Released, 160, 5000);
        Assert.True(grid.Input.Animations.IsSwipeSnapping);

        // The clock is held (0 ms steps), so every measured frame draws the snap in progress
        Assert.Equal(0, Measure(ctx =>
        {
            grid.Input.UpdateAnimations(0);
            grid.Render(ctx);
        }));
        Assert.True(grid.Input.Animations.IsSwipeSnapping);
    }
}
