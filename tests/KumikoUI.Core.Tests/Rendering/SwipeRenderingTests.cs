using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Input;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// Drawing a swiped row (#329): the row moves by its offset as one piece across the left-frozen, scrolling and
/// right-frozen panes, the items it uncovers are drawn in the space it left (shared equally, the outermost against the
/// edge, all of it past the full-swipe point, disabled colors for an item that cannot run), and nothing is drawn for a
/// row off screen.
/// </summary>
/// <remarks>
/// <see cref="InputGrid"/> geometry with Name frozen left (0-120) and Score frozen right (200-300): the scrolling pane
/// (120-200) shows Age. Row 2 is drawn at y 112-148.
/// </remarks>
public class SwipeRenderingTests
{
    private const KumikoPointerDeviceType Touch = KumikoPointerDeviceType.Touch;
    private static readonly KumikoColor FlagColor = new(255, 149, 0);
    private static readonly KumikoColor ArchiveColor = new(0, 122, 255);
    private static readonly KumikoColor DeleteColor = new(255, 59, 48);

    private sealed class NeverCommand : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => false;
        public void Execute(object? parameter) { }
    }

    private static InputGrid Grid(Action<InputGrid>? more = null) => new(configure: g =>
    {
        g.Name.IsFrozen = true;
        g.Score.IsFrozenRight = true;
        g.Style.MaxFrozenColumnsWidthRatio = 0.9f; // both frozen panes stay pinned in the 300 px viewport
        g.Input.AllowSwiping = true;
        g.Input.LeadingSwipeItems.Add(new ActionButtonDefinition { Label = "Flag", BackgroundColor = FlagColor });
        g.Input.TrailingSwipeItems.Add(new ActionButtonDefinition { Label = "Archive", BackgroundColor = ArchiveColor });
        g.Input.TrailingSwipeItems.Add(new ActionButtonDefinition { Label = "Delete", BackgroundColor = DeleteColor });
        more?.Invoke(g);
    });

    private static void Drag(InputGrid g, int row, float dx, bool release = true)
    {
        float y = g.RowY(row);
        g.Pointer(InputAction.Pressed, 150, y, timestampMs: 1000, deviceType: Touch);
        int steps = (int)MathF.Ceiling(MathF.Abs(dx) / 10f);
        for (int i = 1; i <= steps; i++)
            g.Pointer(InputAction.Moved, 150 + dx * i / steps, y, timestampMs: 1000 + i * 16L, deviceType: Touch);
        if (release) g.Pointer(InputAction.Released, 150 + dx, y, timestampMs: 5000, deviceType: Touch);
    }

    private static bool Near(KumikoRect a, KumikoRect b) =>
        MathF.Abs(a.X - b.X) < 0.01f && MathF.Abs(a.Y - b.Y) < 0.01f &&
        MathF.Abs(a.Width - b.Width) < 0.01f && MathF.Abs(a.Height - b.Height) < 0.01f;

    private static void AssertRect(KumikoRect expected, KumikoRect? actual) =>
        Assert.True(actual is { } a && Near(expected, a), $"expected {expected.X},{expected.Y} {expected.Width}x{expected.Height}, got {actual}");

    /// <summary>The last op drawing <paramref name="text"/>: the swiped row draws over its first drawing.</summary>
    private static DrawOp LastText(RecordingDrawingContext ctx, string text) => ctx.TextsEqual(text).Last();

    [Fact]
    public void AnOpenLeadingSwipe_MovesTheRowInEveryPane_AndDrawsItsItem()
    {
        var g = Grid();
        var before = g.RenderRecorded();
        Drag(g, 2, 60);
        Assert.Equal(SwipePhase.Open, g.Input.Swipe.Phase);

        var ctx = g.RenderRecorded();

        foreach (var text in new[] { "N02", "22", "20" }) // Name (left-frozen), Age (scrolling), Score (right-frozen)
        {
            var moved = LastText(ctx, text);
            Assert.Equal(LastText(before, text).Rect.X + 72, moved.Rect.X, 0.01f);
        }

        // Each pane keeps its clip, moved with the row, inside the row's band
        var name = LastText(ctx, "N02");
        AssertRect(new KumikoRect(72, 112, 120, 36), name.Clip);
        AssertRect(new KumikoRect(272, 112, 28, 36), LastText(ctx, "20").Clip);

        // The item fills the space the row left
        AssertRect(new KumikoRect(0, 112, 72, 36), LastText(ctx, "Flag").Rect);
        Assert.Contains(ctx.Fills, f => f.Color == FlagColor && Near(f.Rect, new KumikoRect(0, 112, 72, 36)));

        // Other rows are drawn once, where they are
        Assert.Single(ctx.TextsEqual("N03"));
        AssertRect(LastText(before, "N03").Rect, LastText(ctx, "N03").Rect);
        Assert.Equal(0, ctx.SaveDepth);
    }

    [Fact]
    public void AnOpenTrailingSwipe_DrawsItsItems_OutermostAgainstTheEdge()
    {
        var g = Grid();
        Drag(g, 2, -60);

        var ctx = g.RenderRecorded();

        AssertRect(new KumikoRect(228, 112, 72, 36), LastText(ctx, "Archive").Rect);
        AssertRect(new KumikoRect(156, 112, 72, 36), LastText(ctx, "Delete").Rect);
        Assert.Contains(ctx.Fills, f => f.Color == ArchiveColor && Near(f.Rect, new KumikoRect(228, 112, 72, 36)));
        Assert.Contains(ctx.Fills, f => f.Color == DeleteColor && Near(f.Rect, new KumikoRect(156, 112, 72, 36)));
        Assert.Empty(ctx.TextsEqual("Flag"));
        // The row moved 144 to the left: the scrolling pane's Age is clipped at the item's edge
        var age = LastText(ctx, "22");
        Assert.True(age.Clip is { } clip && clip.Right <= 156 + 0.01f);
    }

    [Fact]
    public void WhileTracking_TheItemsShareTheUncoveredSpace()
    {
        var g = Grid();
        Drag(g, 2, -80, release: false);

        var ctx = g.RenderRecorded();

        AssertRect(new KumikoRect(260, 112, 40, 36), LastText(ctx, "Archive").Rect);
        AssertRect(new KumikoRect(220, 112, 40, 36), LastText(ctx, "Delete").Rect);
    }

    [Fact]
    public void PastTheFullSwipePoint_TheOutermostItemTakesAllTheSpace()
    {
        var g = Grid(x => x.Input.FullSwipeMode = KumikoFullSwipeMode.Trailing);
        Drag(g, 2, -200, release: false);

        var ctx = g.RenderRecorded();

        AssertRect(new KumikoRect(100, 112, 200, 36), LastText(ctx, "Archive").Rect);
        Assert.Empty(ctx.TextsEqual("Delete"));
    }

    [Fact]
    public void AnItemThatCannotRun_IsDrawnDisabled()
    {
        var g = Grid(x => ((ActionButtonDefinition)x.Input.TrailingSwipeItems[1]).Command = new NeverCommand());
        Drag(g, 2, -60);

        var ctx = g.RenderRecorded();

        var delete = LastText(ctx, "Delete");
        Assert.Equal(g.Style.ActionButtonDisabledTextColor, delete.Color);
        Assert.Contains(ctx.Fills, f => f.Color == g.Style.ActionButtonDisabledBackgroundColor &&
                                        Near(f.Rect, new KumikoRect(156, 112, 72, 36)));
    }

    [Fact]
    public void ARowScrolledOutOfView_DrawsNothing()
    {
        var g = Grid();
        Drag(g, 2, -60);

        g.Scroll.ScrollTo(0, 400);
        var ctx = g.RenderRecorded();

        Assert.Empty(ctx.TextsEqual("Archive"));
        Assert.Equal(0, ctx.SaveDepth);
    }

    [Fact]
    public void ASwipedFrozenRow_StaysBelowTheHeader_WhileTheRowsScroll()
    {
        var g = Grid(x => x.Source.FrozenRowCount = 1);
        Drag(g, 0, -60);
        g.Scroll.ScrollTo(0, 100);

        var ctx = g.RenderRecorded();

        AssertRect(new KumikoRect(228, 40, 72, 36), LastText(ctx, "Archive").Rect);
    }

    [Fact]
    public void ARowClosing_IsDrawnSlidingBack_UntilTheSnapEnds()
    {
        var g = Grid(x => x.Input.AnimationsEnabled = true);
        Drag(g, 2, -60);
        g.Input.UpdateAnimations(InteractionAnimations.SwipeSnapMs);
        g.Input.CloseSwipe();
        g.Input.UpdateAnimations(InteractionAnimations.SwipeSnapMs / 2);

        var sliding = g.RenderRecorded();
        var archive = LastText(sliding, "Archive");
        Assert.InRange(archive.Rect.Width, 1, 71);

        g.Input.UpdateAnimations(InteractionAnimations.SwipeSnapMs);
        Assert.Empty(g.RenderRecorded().TextsEqual("Archive"));
    }

    [Fact]
    public void TheItemsAreHitWhereTheyAreDrawn()
    {
        var g = Grid();
        Drag(g, 2, -60);
        var ctx = g.RenderRecorded();
        var delete = LastText(ctx, "Delete").Rect;

        var hit = g.Input.HitTester.HitTest(delete.X + 1, delete.Y + 1, g.Scroll, g.Style, g.Source.Columns,
            g.Source.RowCount, g.Source, Touch);
        Assert.Equal((HitRegion.SwipeItem, 2), (hit.Region, hit.RowIndex));
        var beside = g.Input.HitTester.HitTest(delete.X - 1, delete.Y + 1, g.Scroll, g.Style, g.Source.Columns,
            g.Source.RowCount, g.Source, Touch);
        Assert.Equal(HitRegion.Cell, beside.Region);
    }
}
