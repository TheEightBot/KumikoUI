using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Transitions after an interaction (#101): a group's rows slide open and shut, a dragged column header lifts and
/// settles into its slot, the current row's highlight fades in. They run only when the host drives them
/// (<see cref="KumikoInputController.AnimationsEnabled"/>) and the style allows them, advance by the frame time the host
/// passes, and end on their own.
/// </summary>
public class TransitionTests
{
    // Grouping panel 40 + header 40: rows start at 80, 36 px each. Six items grouped by City:
    // 0 "City: Oslo" header, 1-3 its items, 4 "City: Rome" header, 5-7 its items.
    private const float RowsTop = 80f;
    private const float OsloHeaderY = RowsTop + 18;

    private static InputGrid Grouped(bool animate = true) =>
        new(rows: 6, viewportHeight: 400, configure: g =>
        {
            g.Source.AddGroupDescription(new GroupDescription("City"));
            g.Input.AnimationsEnabled = animate;
        });

    private static float RomeHeaderY(InputGrid g) =>
        g.RenderRecorded().Texts.Single(o => o.Text == "City: Rome").Rect.Y;

    // ── Group expand and collapse ──

    [Fact]
    public void Collapsing_SlidesTheRowsAfterTheGroupUpFromWhereTheyWere()
    {
        var g = Grouped();

        g.Tap(300, OsloHeaderY);

        Assert.True(g.Input.IsAnimating);
        Assert.Equal(RowsTop + 4 * 36, RomeHeaderY(g), 2); // where it was: three rows lower than its new place
        g.Input.UpdateAnimations(100);
        Assert.Equal(RowsTop + 36 + 0.125f * 108, RomeHeaderY(g), 2); // eased out: seven eighths of the way
        Assert.False(g.Input.UpdateAnimations(100));
        Assert.Equal(RowsTop + 36, RomeHeaderY(g), 2);
        Assert.False(g.Input.IsAnimating);
    }

    [Fact]
    public void Expanding_SlidesTheGroupsRowsOutFromUnderItsHeader()
    {
        var g = Grouped(animate: false);
        g.Tap(300, OsloHeaderY); // collapsed, no transition
        g.Input.AnimationsEnabled = true;

        g.Tap(300, OsloHeaderY);

        Assert.Equal(RowsTop + 36, RomeHeaderY(g), 2);
        var frame = g.RenderRecorded();
        var firstItem = frame.Texts.Where(o => o.Text == "N00").ToList();
        Assert.All(firstItem, o => Assert.True(o.Visible == null || o.Visible.Value.Height < 0.5f || o.Visible.Value.Y >= RowsTop + 36 - 0.01f,
            $"a row sliding out is drawn only below its header: {o}"));
        g.Input.UpdateAnimations(250);
        Assert.Equal(RowsTop + 4 * 36, RomeHeaderY(g), 2);
    }

    [Fact]
    public void ASlidingGroup_KeepsSaveAndRestoreBalanced()
    {
        var g = Grouped();
        g.Tap(300, OsloHeaderY);

        var frame = g.RenderRecorded();

        Assert.Equal(0, frame.SaveDepth);
        Assert.Equal(0, frame.UnbalancedRestores);
    }

    [Fact]
    public void NoTransition_WhenTheHostDoesNotDriveThem()
    {
        var g = Grouped(animate: false);

        g.Tap(300, OsloHeaderY);

        Assert.False(g.Input.IsAnimating);
        Assert.Equal(RowsTop + 36, RomeHeaderY(g), 2);
    }

    [Fact]
    public void NoTransition_WhenTheStyleTurnsThemOff()
    {
        var g = Grouped();
        g.Style.EnableAnimations = false;

        g.Tap(300, OsloHeaderY);
        g.Tap(60, RowsTop + 36 + 18 + 36); // a data row: no highlight fade either

        Assert.False(g.Input.IsAnimating);
        Assert.Equal(RowsTop + 36, RomeHeaderY(g), 2);
    }

    [Fact]
    public void TurningAnimationsOff_EndsTheRunningTransition()
    {
        var g = Grouped();
        g.Tap(300, OsloHeaderY);
        int redraws = g.Redraws;

        g.Input.AnimationsEnabled = false; // the platform asked for reduced motion

        Assert.False(g.Input.IsAnimating);
        Assert.True(g.Redraws > redraws);
        Assert.Equal(RowsTop + 36, RomeHeaderY(g), 2);
    }

    [Fact]
    public void UpdateAnimations_RedrawsEachFrame_AndNotOnceDone()
    {
        var g = Grouped();
        g.Tap(300, OsloHeaderY);
        int redraws = g.Redraws;

        g.Input.UpdateAnimations(16);
        g.Input.UpdateAnimations(16);
        Assert.Equal(redraws + 2, g.Redraws);

        g.Input.UpdateAnimations(1000);
        int after = g.Redraws;
        Assert.False(g.Input.UpdateAnimations(16));
        Assert.Equal(after, g.Redraws);
    }

    // ── Selection ──

    [Fact]
    public void SelectingARow_FadesItsHighlightIn()
    {
        var g = new InputGrid(configure: x => x.Input.AnimationsEnabled = true);

        g.Tap(60, g.RowY(2));
        var start = g.RenderRecorded();

        Assert.True(g.Input.IsAnimating);
        // The row's own background laid over its highlight, opaque at first...
        float rowTop = g.Style.HeaderHeight + 2 * g.Style.RowHeight;
        Assert.Single(start.Fills, o => o.Rect.Y == rowTop && o.Color == g.Style.BackgroundColor);

        // ...and gone once the fade is over
        g.Input.UpdateAnimations(200);
        var end = g.RenderRecorded();
        Assert.DoesNotContain(end.Fills, o => o.Rect.Y == rowTop && o.Color.R == g.Style.BackgroundColor.R &&
            o.Color.G == g.Style.BackgroundColor.G && o.Color.B == g.Style.BackgroundColor.B);
        Assert.False(g.Input.IsAnimating);
    }

    [Fact]
    public void ARedrawWithTheSameCurrentRow_StartsNoFade()
    {
        var g = new InputGrid(configure: x => x.Input.AnimationsEnabled = true);
        g.Tap(60, g.RowY(2));
        g.Render();
        g.Input.UpdateAnimations(500);

        g.Render();

        Assert.False(g.Input.IsAnimating);
    }

    // ── Column drag ──

    [Fact]
    public void ADraggedHeader_Lifts_ThenSettlesIntoItsSlot()
    {
        var g = new InputGrid(configure: x => x.Input.AnimationsEnabled = true);
        var ghost = g.Style.HeaderBackgroundColor.WithAlpha(180);

        g.Press(60, InputGrid.HeaderY);
        g.Move(100, InputGrid.HeaderY);
        Assert.True(g.Input.IsAnimating); // lifting
        g.Input.UpdateAnimations(200);
        Assert.Equal(1f, g.Input.Animations.LiftProgress);

        g.Release(100, InputGrid.HeaderY); // no host applies the move: the header settles back at 0
        Assert.True(g.Input.Animations.IsDropping);
        Assert.Contains(g.RenderRecorded().Fills, o => o.Color == ghost && Math.Abs(o.Rect.X - 40) < 0.5f);

        g.Input.UpdateAnimations(90);
        var halfway = g.RenderRecorded().Fills.Single(o => o.Color == ghost);
        Assert.InRange(halfway.Rect.X, 0.5f, 39.5f);

        g.Input.UpdateAnimations(200);
        Assert.DoesNotContain(g.RenderRecorded().Fills, o => o.Color == ghost);
    }

    [Fact]
    public void ADroppedHeader_WithoutAnimations_DisappearsAtOnce()
    {
        var g = new InputGrid();
        var ghost = g.Style.HeaderBackgroundColor.WithAlpha(180);

        g.Press(60, InputGrid.HeaderY);
        g.Move(100, InputGrid.HeaderY);
        g.Release(100, InputGrid.HeaderY);

        Assert.DoesNotContain(g.RenderRecorded().Fills, o => o.Color == ghost);
    }
}
