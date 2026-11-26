using KumikoUI.Core.Data;
using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.IncrementalLoading;

/// <summary>
/// The load-more row (#318, spec 104 §9): its place in the content, what it draws in each state, what a hit on it
/// returns, and how a tap or a key activates it.
/// </summary>
public class LoadMoreRowTests
{
    private const float BandTop = 40 + 3 * 36; // after the 3 rows of the grids below

    /// <summary>A grid of 3 rows whose loader is in <paramref name="state"/> (Manual mode shows the "Load more" row).</summary>
    private static LoadMoreGrid GridShowing(Showing state, Action<LoadMoreGrid>? configure = null)
    {
        var grid = new LoadMoreGrid(rows: 3, configure: g =>
        {
            g.Loader.Mode = KumikoLoadMoreMode.Manual;
            configure?.Invoke(g);
        });
        switch (state)
        {
            case Showing.Loading:
                _ = grid.Loader.LoadMoreAsync();
                break;
            case Showing.Failed:
                _ = grid.Loader.LoadMoreAsync();
                grid.Fake.Fail(new InvalidOperationException());
                break;
            case Showing.None:
                grid.Fake.HasMoreItems = false;
                break;
        }
        grid.Frame();
        Assert.Equal(state.ToString(), grid.Loader.RowKind.ToString());
        return grid;
    }

    /// <summary>What the row shows (the internal <see cref="LoadMoreRowKind"/>, for test parameters).</summary>
    public enum Showing { None, LoadMore, Loading, Failed }

    // ── Geometry ──

    [Theory]
    [InlineData(Showing.None, 0f)]
    [InlineData(Showing.LoadMore, 36f)]
    [InlineData(Showing.Loading, 36f)]
    [InlineData(Showing.Failed, 36f)]
    public void ContentHeight_IncludesTheRow_WhileItShows(Showing state, float band)
    {
        var grid = GridShowing(state);

        grid.Render();

        Assert.Equal(40 + 3 * 36 + band, grid.Scroll.ContentHeight);
    }

    [Theory]
    [InlineData(Showing.None, true)]
    [InlineData(Showing.Loading, true)]
    [InlineData(Showing.Failed, false)]
    [InlineData(Showing.LoadMore, false)]
    public void TheRenderersContentHeight_MatchesTheScrollers(Showing state, bool pinned)
    {
        var grid = GridShowing(state, g =>
        {
            g.Style.PinBottomSummaryRows = pinned;
            g.Source.AddTableSummaryRow(new TableSummaryRow
            {
                Name = "Totals",
                Position = SummaryPosition.Bottom,
                Columns = { new SummaryColumnDescription { PropertyName = "Value", SummaryType = SummaryType.Sum } },
            });
            g.Source.AddTableSummaryRow(new TableSummaryRow
            {
                Name = "Top",
                Position = SummaryPosition.Top,
                Columns = { new SummaryColumnDescription { PropertyName = "Value", SummaryType = SummaryType.Count } },
            });
            g.Source.AddGroupDescription(new GroupDescription("Name"));
        });

        grid.Render();
        float rendered = grid.Scroll.ContentHeight;
        var scroller = new ScrollState { ViewportWidth = LoadMoreGrid.Width, ViewportHeight = LoadMoreGrid.Height };
        KumikoScroller.RefreshContentExtents(scroller, grid.Source, grid.Style);

        Assert.Equal(rendered, scroller.ContentHeight);
    }

    [Fact]
    public void LoadMoreRowHeight_SetsTheBandsHeight()
    {
        var grid = GridShowing(Showing.LoadMore, g => g.Style.LoadMoreRowHeight = 50);

        var ctx = grid.Render();

        Assert.Equal(40 + 3 * 36 + 50, grid.Scroll.ContentHeight);
        Assert.Equal(50, ctx.SingleText("Load more").Rect.Height);
    }

    [Fact]
    public void WhenTheRowHides_AGridScrolledToTheBottomSettles()
    {
        var grid = new LoadMoreGrid(rows: 40);
        grid.ScrollToEnd();
        grid.Frame(); // starts loading
        grid.Render(); // the band adds a row's height
        grid.Scroll.OffsetY = grid.Scroll.MaxOffsetY;
        float withBand = grid.Scroll.OffsetY;

        grid.Fake.Complete(0, hasMore: false);
        grid.Frame();
        grid.Render();

        Assert.Equal(withBand - 36, grid.Scroll.OffsetY);
    }

    // ── Drawing ──

    [Fact]
    public void ManualIdle_DrawsLoadMore_AfterTheLastRow_InTheAccentColor()
    {
        var grid = GridShowing(Showing.LoadMore);

        var text = grid.Render().SingleText("Load more");

        Assert.Equal(BandTop, text.Rect.Y);
        Assert.Equal(36, text.Rect.Height);
        Assert.Equal(KumikoTextAlignment.Center, text.HAlign);
        Assert.Equal(grid.Style.AccentColor, text.Color);
    }

    [Fact]
    public void Loading_DrawsThreeDotsAndTheLoadingText()
    {
        var grid = GridShowing(Showing.Loading);

        var ctx = grid.Render();

        var text = ctx.SingleText("Loading more...");
        Assert.Equal(BandTop, text.Rect.Y);
        var dots = ctx.Ops.Where(o => o.Kind == DrawOpKind.FillRoundRect && o.Rect.Y > BandTop && o.Rect.Y < BandTop + 36).ToList();
        Assert.Equal(3, dots.Count);
        Assert.All(dots, d => Assert.True(d.Rect.Right <= text.Rect.X));
        Assert.True(grid.Renderer.NeedsLoadMoreAnimationFrame);
    }

    [Fact]
    public void Loading_TheOpaqueDotFollowsTheClock()
    {
        var grid = GridShowing(Showing.Loading);

        int Opaque()
        {
            var dots = grid.Render().Ops.Where(o => o.Kind == DrawOpKind.FillRoundRect && o.Rect.Y > BandTop && o.Rect.Y < BandTop + 36)
                .OrderBy(o => o.Rect.X).ToList();
            return dots.FindIndex(d => d.Color.A == 255);
        }

        grid.ClockMs = 0;
        Assert.Equal(0, Opaque());
        grid.ClockMs = KumikoRenderer.LoadMoreIndicatorStepMs;
        Assert.Equal(1, Opaque());
        grid.ClockMs = 2 * KumikoRenderer.LoadMoreIndicatorStepMs + 10;
        Assert.Equal(2, Opaque());
    }

    [Fact]
    public void Loading_WithReducedMotion_IsStatic()
    {
        var grid = GridShowing(Showing.Loading, g => g.Input.AnimationsEnabled = false);

        var dots = grid.Render().Ops.Where(o => o.Kind == DrawOpKind.FillRoundRect && o.Rect.Y > BandTop && o.Rect.Y < BandTop + 36).ToList();

        Assert.Equal(3, dots.Count);
        Assert.Single(dots.Select(d => d.Color).Distinct());
        Assert.False(grid.Renderer.NeedsLoadMoreAnimationFrame);
    }

    [Fact]
    public void NeedsLoadMoreAnimationFrame_IsFalse_WhenTheLoadingRowIsOffScreen()
    {
        var grid = new LoadMoreGrid(rows: 100);
        _ = grid.Loader.LoadMoreAsync();

        grid.Render();

        Assert.False(grid.Renderer.NeedsLoadMoreAnimationFrame);
    }

    [Fact]
    public void Failed_DrawsTheErrorAndARetryPill()
    {
        var grid = GridShowing(Showing.Failed);

        var ctx = grid.Render();

        var error = ctx.SingleText(KumikoStrings.Default.LoadMoreFailed);
        Assert.Equal(grid.Style.LoadMoreErrorTextColor, error.Color);
        var retry = ctx.SingleText("Retry");
        Assert.True(retry.Rect.X > error.Rect.X);
        Assert.Contains(ctx.Ops, o => o.Kind == DrawOpKind.FillRoundRect && o.Rect.Equals(retry.Rect) && o.Color == grid.Style.AccentColor);
    }

    [Fact]
    public void Failed_DrawsTheErrorTextGivenByAHandler()
    {
        var grid = new LoadMoreGrid(rows: 3);
        grid.Loader.LoadMoreCompleted += (_, e) => e.ErrorText = "No connection";
        grid.Frame();
        grid.Fake.Fail(new InvalidOperationException());

        Assert.Single(grid.Render().TextsEqual("No connection"));
    }

    [Fact]
    public void TheRowUsesTheLocalizedStrings()
    {
        var grid = GridShowing(Showing.LoadMore, g => g.Source.Strings = KumikoStrings.Default with { LoadMoreButton = "Mehr laden" });

        Assert.Single(grid.Render().TextsEqual("Mehr laden"));
    }

    [Fact]
    public void TheRow_IsCenteredInTheViewport_WhenScrolledHorizontally()
    {
        var grid = GridShowing(Showing.LoadMore, g => g.Name.Width = 600);
        var before = grid.Render().SingleText("Load more").Rect;

        grid.Scroll.OffsetX = 200;
        var after = grid.Render().SingleText("Load more").Rect;

        Assert.Equal(before, after);
        Assert.Equal(grid.Style.CellPadding, after.X);
        Assert.Equal(LoadMoreGrid.Width - 2 * grid.Style.CellPadding, after.Width);
    }

    [Fact]
    public void UnpinnedBottomSummaries_AreDrawnBelowTheRow()
    {
        var grid = GridShowing(Showing.LoadMore, g =>
        {
            g.Style.PinBottomSummaryRows = false;
            g.Source.AddTableSummaryRow(new TableSummaryRow
            {
                Name = "Totals",
                Position = SummaryPosition.Bottom,
                Columns = { new SummaryColumnDescription { PropertyName = "Value", SummaryType = SummaryType.Sum } },
            });
        });

        var ctx = grid.Render();

        Assert.Contains(ctx.Fills, f => f.Rect.Y == BandTop + 36 && f.Color == grid.Style.SummaryRowBackgroundColor);
        Assert.Equal(HitRegion.TableSummaryRow, grid.Hit(50, BandTop + 40).Region);
    }

    [Fact]
    public void APinnedFooter_IsDrawnOverTheRow()
    {
        var grid = GridShowing(Showing.LoadMore, g =>
        {
            g.Source.AddTableSummaryRow(new TableSummaryRow
            {
                Name = "Totals",
                Position = SummaryPosition.Bottom,
                Columns = { new SummaryColumnDescription { PropertyName = "Value", SummaryType = SummaryType.Sum } },
            });
            g.Scroll.ViewportHeight = BandTop + 36 + 10; // the footer covers most of the band
        });

        var ctx = grid.Render();
        var text = ctx.SingleText("Load more");
        var footer = ctx.Fills.Last(f => f.Color == grid.Style.SummaryRowBackgroundColor);

        Assert.True(footer.Index > text.Index);
        Assert.Equal(HitRegion.TableSummaryRow, grid.Hit(50, grid.Scroll.ViewportHeight - 5).Region);
    }

    [Fact]
    public void Completed_DrawsNoRow()
    {
        var grid = GridShowing(Showing.None);

        var ctx = grid.Render();

        Assert.Empty(ctx.TextsEqual("Load more"));
        Assert.Empty(ctx.TextsEqual("Loading more..."));
    }

    // ── Hit testing ──

    [Fact]
    public void Hit_OnTheRow_IsLoadMoreRow_WithNoRowIndex()
    {
        var grid = GridShowing(Showing.LoadMore);

        var hit = grid.Hit(50, BandTop + 10);

        Assert.Equal(HitRegion.LoadMoreRow, hit.Region);
        Assert.Equal(-1, hit.RowIndex);
        Assert.Equal(HitRegion.Cell, grid.Hit(50, BandTop - 10).Region);
        Assert.Equal(HitRegion.EmptyArea, grid.Hit(50, BandTop + 40).Region);
    }

    [Fact]
    public void Hit_WhenTheRowIsHidden_IsTheEmptyArea()
    {
        var grid = GridShowing(Showing.None);

        Assert.Equal(HitRegion.EmptyArea, grid.Hit(50, BandTop + 10).Region);
    }

    [Fact]
    public void Hit_FollowsTheScrollOffset()
    {
        var grid = new LoadMoreGrid(rows: 40, configure: g => g.Loader.Mode = KumikoLoadMoreMode.Manual);
        grid.Frame();
        grid.ScrollToEnd();

        Assert.Equal(HitRegion.LoadMoreRow, grid.Hit(50, LoadMoreGrid.Height - 10).Region);
        Assert.Equal(HitRegion.Cell, grid.Hit(50, LoadMoreGrid.Height - 40).Region);
    }

    // ── Pointer ──

    [Fact]
    public void Tap_OnLoadMore_Loads_AndLeavesTheSelectionAlone()
    {
        var grid = GridShowing(Showing.LoadMore);
        grid.Selection.ClearSelectionOnEmptyAreaTap = true;
        grid.Selection.CurrentCell = new CellPosition(1, 0);
        grid.Selection.Select(grid.Items[1]);
        var current = grid.Selection.CurrentCell;

        grid.Tap(50, BandTop + 10);

        Assert.Equal(KumikoLoadMoreTrigger.Tap, Assert.Single(grid.Requests).Trigger);
        Assert.Equal(current, grid.Selection.CurrentCell);
        Assert.Single(grid.Selection.SelectedItems);
    }

    [Fact]
    public void Tap_OnTheFailedRow_Retries()
    {
        var grid = GridShowing(Showing.Failed);

        grid.Tap(20, BandTop + 10); // anywhere on the row, not only on the pill

        Assert.Equal(2, grid.Fake.Calls.Count);
        Assert.Equal(KumikoLoadMoreTrigger.Tap, grid.Requests[^1].Trigger);
    }

    [Fact]
    public void Tap_OnTheLoadingRow_DoesNothing()
    {
        var grid = GridShowing(Showing.Loading);

        grid.Tap(50, BandTop + 10);

        Assert.Single(grid.Fake.Calls);
    }

    [Fact]
    public void APressThatStartedElsewhere_DoesNotLoad()
    {
        var grid = GridShowing(Showing.LoadMore);

        grid.Pointer(InputAction.Pressed, 50, BandTop - 10);
        grid.Pointer(InputAction.Released, 50, BandTop + 10);

        Assert.Empty(grid.Fake.Calls);
    }

    [Theory]
    [InlineData(Showing.LoadMore, KumikoCursor.Hand)]
    [InlineData(Showing.Failed, KumikoCursor.Hand)]
    [InlineData(Showing.Loading, KumikoCursor.Default)]
    public void Hover_ShowsAHand_OverAnActionableRow(Showing state, KumikoCursor cursor)
    {
        var grid = GridShowing(state);

        grid.Pointer(InputAction.Hover, 50, BandTop + 10, PointerButton.None);

        Assert.Equal(cursor, grid.Input.Cursor);
    }

    [Fact]
    public void Hover_OverTheLoadMoreRow_DrawsTheHoverOverlay()
    {
        var grid = GridShowing(Showing.LoadMore);

        grid.Pointer(InputAction.Hover, 50, BandTop + 10, PointerButton.None);
        var ctx = grid.Render();

        Assert.Contains(ctx.Fills, f => f.Rect.Y == BandTop && f.Color == grid.Style.HoverOverlayColor);
    }

    // ── Keyboard ──

    [Theory]
    [InlineData(KumikoKey.Down, InputModifiers.None)]
    [InlineData(KumikoKey.PageDown, InputModifiers.None)]
    [InlineData(KumikoKey.End, InputModifiers.Control)]
    public void AKeyPastTheLastRow_LoadsInManualMode(KumikoKey key, InputModifiers modifiers)
    {
        var grid = GridShowing(Showing.LoadMore);
        grid.Selection.CurrentCell = new CellPosition(2, 0);

        grid.Key(key, modifiers);

        Assert.Equal(KumikoLoadMoreTrigger.Keyboard, Assert.Single(grid.Requests).Trigger);
        Assert.Equal(2, grid.Selection.CurrentCell.Row); // the current cell stays on the last loaded row
    }

    [Fact]
    public void Down_BeforeTheLastRow_DoesNotLoad()
    {
        var grid = GridShowing(Showing.LoadMore);
        grid.Selection.CurrentCell = new CellPosition(1, 0);

        grid.Key(KumikoKey.Down);

        Assert.Empty(grid.Requests);
        Assert.Equal(2, grid.Selection.CurrentCell.Row);
    }

    [Fact]
    public void Down_OnTheLastRow_RetriesAFailedLoad()
    {
        var grid = GridShowing(Showing.Failed);
        grid.Selection.CurrentCell = new CellPosition(2, 0);

        grid.Key(KumikoKey.Down);

        Assert.Equal(2, grid.Fake.Calls.Count);
        Assert.Equal(KumikoLoadMoreTrigger.Keyboard, grid.Requests[^1].Trigger);
    }

    [Fact]
    public void Down_OnTheLastRow_InAutomaticMode_AddsNothingToTheNearEndCheck()
    {
        var grid = new LoadMoreGrid(rows: 3, configure: g => g.Fake.HasMoreItems = false);
        grid.Frame();
        grid.Selection.CurrentCell = new CellPosition(2, 0);

        grid.Key(KumikoKey.Down);

        Assert.Empty(grid.Requests);
    }

    // ── Row drag ──

    [Fact]
    public void ARowDrag_NeverDropsOnTheRow()
    {
        var grid = GridShowing(Showing.LoadMore);

        int target = RowDragGeometry.DropTarget(BandTop + 10, grid.Scroll, grid.Style, grid.Source);

        Assert.Equal(2, target); // clamped to the last row
    }
}
