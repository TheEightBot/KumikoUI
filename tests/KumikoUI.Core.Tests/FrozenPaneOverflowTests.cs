using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Frozen panes that do not fit the viewport (#268): left- and right-frozen columns together wider than their share
/// (<see cref="KumikoStyle.MaxFrozenColumnsWidthRatio"/>) are unpinned, right-frozen first and then the rightmost
/// left-frozen ones, so the panes never overlap and every scrolling column stays reachable; they are pinned again
/// when the viewport widens.
/// </summary>
/// <remarks>
/// The grid of the issue: the row drag handle (32), Id (72) and Name (160) frozen left, City (130) frozen right and
/// five scrolling columns (Department 130, Salary 110, Hire Date 120, Active 90, Level 90): 934 px of content.
/// On screen (<see cref="KumikoSource.DisplayColumns"/>): Id, Name, Department … Level, City.
/// </remarks>
public class FrozenPaneOverflowTests
{
    private sealed class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
        public string Department { get; set; } = "";
        public decimal Salary { get; set; }
        public DateTime HireDate { get; set; }
        public bool Active { get; set; }
        public int Level { get; set; }
    }

    private sealed class Grid
    {
        public KumikoSource Source { get; } = new();
        public KumikoStyle Style { get; } = new() { ShowRowDragHandle = true };
        public ScrollState Scroll { get; }
        public KumikoSelectionModel Selection { get; }
        public KumikoRenderer Renderer { get; } = new();
        public KumikoHitTester Tester { get; } = new();
        public KumikoInputController Input { get; } = new();
        public EditSession Edit { get; } = new();

        public Grid(float width, Action<Grid>? configure = null)
        {
            Scroll = new ScrollState { ViewportWidth = width, ViewportHeight = 400 };
            Selection = new KumikoSelectionModel(Source);
            Source.SetColumns(new[]
            {
                new KumikoColumn { Header = "Id", PropertyName = "Id", Width = 72, IsFrozen = true },
                new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 160, IsFrozen = true },
                new KumikoColumn { Header = "City", PropertyName = "City", Width = 130, IsFrozenRight = true },
                new KumikoColumn { Header = "Department", PropertyName = "Department", Width = 130 },
                new KumikoColumn { Header = "Salary", PropertyName = "Salary", Width = 110 },
                new KumikoColumn { Header = "Hire Date", PropertyName = "HireDate", Width = 120 },
                new KumikoColumn { Header = "Active", PropertyName = "Active", Width = 90 },
                new KumikoColumn { Header = "Level", PropertyName = "Level", Width = 90 },
            });
            Source.SetItems(Enumerable.Range(1, 20).Select(i => new Employee
            {
                Id = i, Name = $"E{i:D2}", City = "Oslo", Department = "Sales", Salary = i, Level = i,
            }).ToList());
            Edit.Style = Style;
            Input.EditSession = Edit;
            configure?.Invoke(this);
            Render();
        }

        public RecordingDrawingContext Render()
        {
            var ctx = new RecordingDrawingContext();
            Renderer.Render(ctx, Source, Scroll, Selection, Style, editSession: Edit);
            return ctx;
        }

        public int Index(string header) => Source.Columns.ToList().FindIndex(c => c.Header == header);

        public KumikoColumn Column(string header) => Source.Columns[Index(header)];

        public float X(string header) => KumikoScroller.GetColumnX(Source.Columns, Style, Scroll, Index(header));

        public KumikoHitTestResult Hit(float x, float y) =>
            Tester.HitTest(x, y, Scroll, Style, Source.Columns, Source.RowCount, Source);

        /// <summary>Viewport x ranges of the pinned left pane and the pinned right pane.</summary>
        public (float LeftEnd, float RightStart) Panes()
        {
            var (left, right) = FrozenPanes.GetWidths(Source.Columns);
            return (Style.RowDragHandleWidth + left, Scroll.ViewportWidth - right);
        }
    }

    private const float HeaderY = 20f;
    private const float RowY = 40f + 18f;

    private static string[] Pinned(Grid g, ColumnFreezeMode pane) =>
        g.Source.Columns.Where(c => c.IsVisible && c.Pane == pane).Select(c => c.Header).ToArray();

    // ── The issue's layout ──

    [Fact]
    public void Phone375_UnpinsCityThenName_AndThePanesDoNotOverlap()
    {
        // Column area 343, two thirds 228.7: City (362 -> 232) and then Name (232 -> 72) are unpinned.
        var g = new Grid(375);

        Assert.Equal(new[] { "Id" }, Pinned(g, ColumnFreezeMode.Left));
        Assert.Empty(Pinned(g, ColumnFreezeMode.Right));
        Assert.Equal(32, g.X("Id"));
        Assert.Equal(104, g.X("Name")); // first scrolling column, right after Id
        Assert.Equal(104 + 160, g.X("Department"));
        Assert.Equal(104 + 160 + 540, g.X("City")); // last scrolling column, after Level

        // Declared freeze modes are kept.
        Assert.True(g.Column("Name").IsFrozen);
        Assert.True(g.Column("City").IsFrozenRight);
    }

    [Fact]
    public void Phone390_UnpinsOnlyCity()
    {
        // Column area 358, two thirds 238.7: Id + Name (232) fit once City is unpinned.
        var g = new Grid(390);

        Assert.Equal(new[] { "Id", "Name" }, Pinned(g, ColumnFreezeMode.Left));
        Assert.Empty(Pinned(g, ColumnFreezeMode.Right));
        Assert.Equal(264, g.X("Department"));
        Assert.Equal(264 + 540, g.X("City"));
        var (leftEnd, rightStart) = g.Panes();
        Assert.True(leftEnd <= rightStart);
    }

    [Theory]
    [InlineData(375f)]
    [InlineData(390f)]
    [InlineData(1000f)]
    public void HeaderDrag_ScreenLeft_MatchesWhereTheColumnIsDrawn(float width)
    {
        var g = new Grid(width);

        for (int i = 0; i < g.Source.Columns.Count; i++)
        {
            float drawn = KumikoScroller.GetColumnX(g.Source.Columns, g.Style, g.Scroll, i);
            Assert.Equal(drawn, ColumnDragGeometry.ScreenLeft(g.Source.Columns, i, g.Scroll, g.Style), 3);
        }
    }

    [Fact]
    public void HeaderDrag_DropSlot_UsesTheDrawnPositionsOfUnpinnedColumns()
    {
        // At 390 px City is unpinned and drawn last in the scrolling pane, after Level, so it must not shift the
        // drop slots of the columns drawn before it.
        var g = new Grid(390, x => x.Scroll.OffsetX = 200);
        int department = g.Index("Department");

        // Dropped over the right half of Salary: Department goes after Salary.
        int index = ColumnDragGeometry.DropIndex(
            g.Source.Columns, department, g.X("Salary") + 100, g.Scroll, g.Style);

        Assert.Equal(g.Index("Salary"), index);
    }

    [Fact]
    public void WideViewport_PinsEverythingAsDeclared()
    {
        var g = new Grid(1000);

        Assert.Equal(new[] { "Id", "Name" }, Pinned(g, ColumnFreezeMode.Left));
        Assert.Equal(new[] { "City" }, Pinned(g, ColumnFreezeMode.Right));
        Assert.Equal(1000 - 130, g.X("City"));
    }

    [Fact]
    public void WideningTheViewport_PinsTheColumnsAgain_AndNarrowingUnpinsThem()
    {
        var g = new Grid(375);
        Assert.Empty(Pinned(g, ColumnFreezeMode.Right));

        g.Scroll.ViewportWidth = 1000;
        g.Render();
        Assert.Equal(new[] { "Id", "Name" }, Pinned(g, ColumnFreezeMode.Left));
        Assert.Equal(new[] { "City" }, Pinned(g, ColumnFreezeMode.Right));
        Assert.Equal(870, g.X("City"));
        Assert.Same(g.Column("City"), g.Hit(900, RowY).Column);

        g.Scroll.ViewportWidth = 375;
        g.Render();
        Assert.Equal(new[] { "Id" }, Pinned(g, ColumnFreezeMode.Left));
    }

    [Fact]
    public void ChangingFreezeMode_PinsTheColumnUntilTheNextLayout()
    {
        var g = new Grid(390);
        var city = g.Column("City");
        Assert.Equal(ColumnFreezeMode.None, city.Pane);

        city.FreezeMode = ColumnFreezeMode.Left; // a different freeze mode is not the one that was suspended
        Assert.Equal(ColumnFreezeMode.Left, city.Pane);

        g.Render(); // Id + Name + City = 362 > 238.7: City, the rightmost left-frozen column, is unpinned
        Assert.Equal(ColumnFreezeMode.None, city.Pane);
        Assert.Equal(new[] { "Id", "Name" }, Pinned(g, ColumnFreezeMode.Left));
    }

    // ── Policy ──

    [Fact]
    public void RatioOne_UnpinsOnlyWhatWouldOverlap()
    {
        var g = new Grid(400, x => x.Style.MaxFrozenColumnsWidthRatio = 1f);
        // Column area 368 holds the 362 px of frozen columns.
        Assert.Equal(new[] { "City" }, Pinned(g, ColumnFreezeMode.Right));
        Assert.Equal(400 - 130, g.X("City"));

        g.Scroll.ViewportWidth = 390; // column area 358 < 362: City would overlap Name
        g.Render();
        Assert.Empty(Pinned(g, ColumnFreezeMode.Right));
    }

    [Fact]
    public void RatioZero_UnpinsEveryFrozenColumnWhileTheGridScrolls()
    {
        var g = new Grid(600, x => x.Style.MaxFrozenColumnsWidthRatio = 0f);

        Assert.Empty(Pinned(g, ColumnFreezeMode.Left));
        Assert.Empty(Pinned(g, ColumnFreezeMode.Right));
        Assert.Equal(32, g.X("Id"));
    }

    [Fact]
    public void ColumnsThatFitTheViewport_StayPinned_WhateverTheShare()
    {
        // Only the frozen columns: 362 px in a 400 px grid, nothing scrolls, so nothing is unpinned.
        var g = new Grid(400);
        foreach (var c in g.Source.Columns.Where(c => !c.IsFrozen && !c.IsFrozenRight)) c.IsVisible = false;
        g.Render();

        Assert.Equal(new[] { "Id", "Name" }, Pinned(g, ColumnFreezeMode.Left));
        Assert.Equal(new[] { "City" }, Pinned(g, ColumnFreezeMode.Right));
    }

    [Fact]
    public void RightFrozenColumns_AreUnpinnedFromTheInnerEdge_AndLandAtTheEndOfTheScrollingPane()
    {
        var g = new Grid(600, x =>
        {
            x.Column("Level").IsFrozenRight = true; // right pane: City, Level (220); left pane 232; 452 > 378.7
        });

        Assert.Equal(new[] { "Level" }, Pinned(g, ColumnFreezeMode.Right)); // City, the inner one, goes first
        Assert.Equal(600 - 90, g.X("Level"));
        // City scrolls after the scrolling columns, so the on-screen order is unchanged.
        Assert.Equal(264 + 130 + 110 + 120 + 90, g.X("City"));
        var scrolling = g.Source.DisplayColumns.Where(c => c.Pane == ColumnFreezeMode.None).ToList();
        Assert.Equal(scrolling.Select(c => c.Header), scrolling.OrderBy(c => g.X(c.Header)).Select(c => c.Header));
    }

    // ── Hit-testing, scrolling, keyboard and rendering ──

    [Theory]
    [InlineData(240)]
    [InlineData(320)]
    [InlineData(375)]
    [InlineData(390)]
    [InlineData(430)]
    [InlineData(600)]
    [InlineData(768)]
    [InlineData(1000)]
    public void EveryColumn_IsReachable_AndThePanesNeverOverlap(float width)
    {
        var g = new Grid(width);
        var (leftEnd, rightStart) = g.Panes();
        Assert.True(leftEnd <= rightStart, $"left pane ends at {leftEnd}, right pane starts at {rightStart}");

        foreach (var column in g.Source.Columns)
        {
            int index = g.Index(column.Header);
            Assert.True(KumikoScroller.ScrollToColumn(g.Scroll, g.Source, g.Style, index, KumikoScrollPosition.Start));
            g.Render();

            float x = g.X(column.Header);
            if (column.Pane == ColumnFreezeMode.None)
                Assert.InRange(x, leftEnd - 0.01f, rightStart - 1);

            // A point inside the column, clear of the resize grips at its edges, hits it in the header and in a row.
            Assert.Same(column, g.Hit(x + 10, HeaderY).Column);
            var cell = g.Hit(x + 10, RowY);
            Assert.Equal(HitRegion.Cell, cell.Region);
            Assert.Same(column, cell.Column);
        }
    }

    [Fact]
    public void Phone390_ScrolledToTheEnd_ShowsCityAgainstTheRightEdge()
    {
        var g = new Grid(390);
        g.Scroll.ScrollTo(10_000, 0);
        g.Render();

        Assert.Equal(934 - 390, g.Scroll.OffsetX);
        Assert.Equal(260, g.X("City"));
        Assert.Same(g.Column("City"), g.Hit(380, RowY).Column);
        Assert.Same(g.Column("Name"), g.Hit(250, RowY).Column); // Name is pinned over the scrolled columns
    }

    [Fact]
    public void Keyboard_EndMovesToTheUnpinnedRightFrozenColumn_AndScrollsItIntoView()
    {
        var g = new Grid(390);
        g.Selection.CurrentCell = new CellPosition(0, g.Index("Id"));

        g.Input.HandleKey(new KumikoKeyEventArgs { Key = KumikoKey.End }, g.Scroll, g.Selection, g.Style, g.Source);
        g.Render();

        Assert.Equal(g.Index("City"), g.Selection.CurrentCell.Column);
        float x = g.X("City");
        // Scrolled to the end: City ends at the right edge; the 126 px scrolling pane shows all but its first 4 px.
        Assert.Equal(390 - 130, x);
        Assert.Same(g.Column("City"), g.Hit(270, RowY).Column);
        Assert.Same(g.Column("Name"), g.Hit(262, RowY).Column);

        var bounds = g.Input.GetCellScreenBounds(0, g.Index("City"), g.Scroll, g.Style, g.Source.Columns, dataSource: g.Source);
        Assert.Equal(x, bounds.X);
    }

    [Theory]
    [InlineData(375)]
    [InlineData(390)]
    public void Render_HeaderLabelsOfDifferentPanes_DoNotOverlap(float width)
    {
        var g = new Grid(width);
        var ctx = g.Render();

        // Each header label is clipped to its pane; the pane clips of the labels must not overlap.
        var headers = g.Source.Columns.Select(c => c.Header).ToHashSet();
        var clips = ctx.Ops
            .Where(o => o.IsText && o.Text != null && headers.Contains(o.Text) && o.Rect.Top < 40 && o.Visible.HasValue)
            .Select(o => o.Clip!.Value)
            .Distinct()
            .ToList();
        Assert.True(clips.Count >= 2);
        for (int i = 0; i < clips.Count; i++)
            for (int j = i + 1; j < clips.Count; j++)
                Assert.Null(RectMath.Intersect(clips[i], clips[j]));
    }

    [Theory]
    [InlineData(390, false)]
    [InlineData(1000, true)]
    public void Render_DrawsTheRightFrozenPane_OnlyWhileItsColumnsArePinned(float width, bool drawn)
    {
        var paneColor = new KumikoColor(1, 2, 3);
        var g = new Grid(width, x => x.Style.RightFrozenColumnBackgroundColor = paneColor);
        var ctx = g.Render();

        Assert.Equal(drawn, ctx.Ops.Any(o => o.Kind == DrawOpKind.FillRect && o.Color == paneColor));
    }
}
