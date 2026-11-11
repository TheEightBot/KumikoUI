using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Row drag and drop (#99): how each device starts a drag (touch long press, mouse and pen drag past the threshold,
/// the drag handle as soon as it moves), edge autoscroll, the drop math under bands and grouping, and the events a
/// drop raises.
/// </summary>
/// <remarks>
/// <see cref="InputGrid"/> geometry: header 40 px, rows 36 px, 300 x 256 viewport (6 rows on screen), 30 rows, so
/// the content scrolls 1120 - 256 = 864 px. Edge autoscroll bands are 40 px: 40-80 and 216-256.
/// </remarks>
public class RowDragTests
{
    private const KumikoPointerDeviceType Touch = KumikoPointerDeviceType.Touch;
    private const KumikoPointerDeviceType Pen = KumikoPointerDeviceType.Pen;

    private static InputGrid RowDragGrid(int rows = 30, float viewportHeight = InputGrid.DefaultViewportHeight,
        Action<InputGrid>? more = null) =>
        new(rows, viewportHeight, configure: x =>
        {
            x.Style.AllowRowDragDrop = true;
            more?.Invoke(x);
        });

    private static KumikoPointerEventArgs Touched(InputGrid g, InputAction action, float x, float y, long timestampMs = 0) =>
        g.Pointer(action, x, y, timestampMs: timestampMs, deviceType: Touch);

    /// <summary>Starts a mouse drag of the row under (150, <paramref name="y"/>): press, then a 16 px move down.</summary>
    private static void StartMouseDrag(InputGrid g, float y)
    {
        g.Press(150, y);
        g.Move(150, y + 16);
    }

    private static (List<RowReorderedEventArgs> Reordered, List<RowReorderedEventArgs> Rejected) Track(InputGrid g)
    {
        var reordered = new List<RowReorderedEventArgs>();
        var rejected = new List<RowReorderedEventArgs>();
        g.Input.RowReordered += (_, e) => reordered.Add(e);
        g.Input.RowReorderRejected += (_, e) => rejected.Add(e);
        return (reordered, rejected);
    }

    private static int RunAutoScroll(InputGrid g, int maxFrames = 1000)
    {
        int frames = 0;
        while (frames < maxFrames && g.Input.UpdateAutoScroll(g.Scroll, 16f)) frames++;
        return frames;
    }

    // ── Touch: a drag pans, a long press drags ──

    [Fact]
    public void TouchDrag_OnACell_WithRowDragEnabled_ScrollsInsteadOfDragging()
    {
        var g = RowDragGrid();
        var (reordered, rejected) = Track(g);

        Touched(g, InputAction.Pressed, 150, 200, 100);
        Touched(g, InputAction.Moved, 150, 180, 116);
        Assert.Equal(-1, g.Input.DragRowIndex);
        Touched(g, InputAction.Moved, 150, 150, 132);
        Touched(g, InputAction.Released, 150, 150, 148);

        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.True(g.Scroll.OffsetY > 0);
        Assert.Empty(reordered);
        Assert.Empty(rejected);
    }

    [Fact]
    public void TouchLongPress_OnARow_StartsTheDrag_AndTheReleaseDropsIt()
    {
        var g = RowDragGrid();
        var (reordered, _) = Track(g);
        var dragged = g.Items[1];

        Touched(g, InputAction.Pressed, 150, g.RowY(1));
        var longPress = Touched(g, InputAction.LongPress, 150, g.RowY(1));

        Assert.True(longPress.Handled);
        Assert.Equal(1, g.Input.DragRowIndex);
        Assert.Equal(76, g.Input.DragRowScreenY); // the ghost covers the row: grabbed 18 px below its top
        Assert.False(g.Selection.CurrentCell.IsValid); // a drag does not select

        Touched(g, InputAction.Moved, 150, g.RowY(4));
        Touched(g, InputAction.Released, 150, g.RowY(4));

        var e = Assert.Single(reordered);
        Assert.Equal((1, 4), (e.OldIndex, e.NewIndex));
        Assert.Same(dragged, g.Items[4]);
        Assert.Equal(-1, g.Input.DragRowIndex);
    }

    [Fact]
    public void TouchLongPress_OnACellThatEditsOnLongPress_EditsInsteadOfDragging()
    {
        var g = RowDragGrid(more: x => x.Edit.EditTriggers = EditTrigger.LongPress);

        Touched(g, InputAction.Pressed, 150, g.RowY(2));
        Touched(g, InputAction.LongPress, 150, g.RowY(2));

        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.True(g.Edit.IsEditing);
    }

    [Fact]
    public void LongPress_AfterThePressBecameAPan_IsIgnored()
    {
        var g = RowDragGrid(more: x => x.Edit.EditTriggers = EditTrigger.LongPress);

        Touched(g, InputAction.Pressed, 150, 200, 100);
        Touched(g, InputAction.Moved, 150, 188, 116); // 12 px: a pan (the host's long-press tolerance is wider)
        var longPress = Touched(g, InputAction.LongPress, 150, 200, 600);

        Assert.True(longPress.Handled);
        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.False(g.Edit.IsEditing);
        Assert.False(g.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void MouseLongPress_DoesNotStartARowDrag()
    {
        var g = RowDragGrid();

        g.Press(150, g.RowY(2));
        g.Pointer(InputAction.LongPress, 150, g.RowY(2));

        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.Equal(new CellPosition(2, 1), g.Selection.CurrentCell); // the long press still selects
    }

    [Fact]
    public void TouchLongPress_WithoutRowDragOrAHandle_DoesNotDrag()
    {
        var g = new InputGrid();

        Touched(g, InputAction.Pressed, 150, g.RowY(2));
        Touched(g, InputAction.LongPress, 150, g.RowY(2));

        Assert.Equal(-1, g.Input.DragRowIndex);
    }

    // ── Mouse and pen: a drag past the threshold ──

    [Fact]
    public void PenDrag_OnACell_StartsTheRowDrag()
    {
        var g = RowDragGrid();

        g.Pointer(InputAction.Pressed, 150, g.RowY(1), deviceType: Pen);
        g.Pointer(InputAction.Moved, 150, g.RowY(1) + 12, deviceType: Pen);

        Assert.Equal(1, g.Input.DragRowIndex);
        Assert.Equal(0, g.Scroll.OffsetY);
    }

    [Fact]
    public void MouseDrag_BelowTheThreshold_DoesNotStartTheDrag()
    {
        var g = RowDragGrid();

        g.Press(150, g.RowY(1));
        g.Move(150, g.RowY(1) + 6);

        Assert.Equal(-1, g.Input.DragRowIndex);
    }

    // ── The drag handle: as soon as it moves, for every device ──

    [Fact]
    public void TouchOnTheDragHandle_DragsAsSoonAsItMoves_AndDoesNotScroll()
    {
        var g = new InputGrid(configure: x => x.Style.ShowRowDragHandle = true);

        Touched(g, InputAction.Pressed, 10, g.RowY(2), 100);
        Touched(g, InputAction.Moved, 10, g.RowY(2) + 3, 116);

        Assert.Equal(2, g.Input.DragRowIndex);
        Touched(g, InputAction.Moved, 10, g.RowY(2) + 40, 132);
        Assert.Equal(0, g.Scroll.OffsetY);
        Assert.Equal(76 + 36 + 40, g.Input.DragRowScreenY);
    }

    [Fact]
    public void TouchLongPress_OnTheDragHandle_StartsTheDrag()
    {
        var g = new InputGrid(configure: x => x.Style.ShowRowDragHandle = true);

        Touched(g, InputAction.Pressed, 10, g.RowY(3));
        Touched(g, InputAction.LongPress, 10, g.RowY(3));

        Assert.Equal(3, g.Input.DragRowIndex);
    }

    [Fact]
    public void TapOnTheDragHandle_DoesNotDrag()
    {
        var g = new InputGrid(configure: x => x.Style.ShowRowDragHandle = true);
        var (reordered, rejected) = Track(g);

        g.Press(10, g.RowY(3));
        g.Move(11, g.RowY(3) + 2); // a tap's jitter
        Assert.Equal(-1, g.Input.DragRowIndex);
        g.Release(11, g.RowY(3) + 2);

        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.Empty(reordered);
        Assert.Empty(rejected);
    }

    [Fact]
    public void TouchLongPress_InAnEdgeBand_AutoScrollsOnlyOnceTheFingerMoves()
    {
        var g = RowDragGrid();

        Touched(g, InputAction.Pressed, 150, 240);
        Touched(g, InputAction.LongPress, 150, 240);
        Assert.Equal(5, g.Input.DragRowIndex);
        Assert.False(g.Input.IsAutoScrolling);

        Touched(g, InputAction.Moved, 150, 242);
        Assert.True(g.Input.IsAutoScrolling);
    }

    // ── Ghost and drop math with bands ──

    [Fact]
    public void Ghost_StaysWithinTheRowsArea()
    {
        var g = RowDragGrid();
        StartMouseDrag(g, g.RowY(2));

        g.Move(150, -100);
        Assert.Equal(40 - 18, g.Input.DragRowScreenY); // its middle at the top of the rows
        g.Move(150, 900);
        Assert.Equal(256 - 18 - 1, g.Input.DragRowScreenY); // its middle just above the viewport bottom
    }

    [Fact]
    public void Drop_WithATopSummary_FrozenRowsAndScroll_LandsUnderTheGhost()
    {
        // Data area from 40 (header) + 36 (top summary) = 76; frozen rows 0-1 at 76-148; scrolled rows below them.
        var g = RowDragGrid(viewportHeight: 400, more: x =>
        {
            x.Source.AddTableSummaryRow(new TableSummaryRow("T", SummaryPosition.Top)
            {
                Columns = { new SummaryColumnDescription { PropertyName = "Score", SummaryType = SummaryType.Sum } },
            });
            x.Source.FrozenRowCount = 2;
        });
        g.Render();
        g.Scroll.OffsetY = 72;
        var (reordered, _) = Track(g);

        // Row 5: top at 76 + 5 x 36 - 72 = 184.
        StartMouseDrag(g, 184 + 18);
        Assert.Equal(5, g.Input.DragRowIndex);
        Assert.Equal(184 + 16, g.Input.DragRowScreenY);

        g.Release(150, 76 + 36 + 10); // ghost middle in frozen row 1

        var e = Assert.Single(reordered);
        Assert.Equal((5, 1), (e.OldIndex, e.NewIndex));
    }

    // ── Edge autoscroll ──

    [Fact]
    public void DragIntoTheBottomBand_AutoScrolls_FasterTheDeeperItGoes()
    {
        var g = RowDragGrid();
        StartMouseDrag(g, g.RowY(1));
        Assert.False(g.Input.IsAutoScrolling);

        g.Move(150, 226); // 10 px into the band
        Assert.True(g.Input.IsAutoScrolling);
        Assert.True(g.Input.UpdateAutoScroll(g.Scroll, 16f));
        float shallow = g.Scroll.OffsetY;

        g.Move(150, 246); // 30 px into the band
        Assert.True(g.Input.UpdateAutoScroll(g.Scroll, 16f));
        float deep = g.Scroll.OffsetY - shallow;

        Assert.Equal(1200f * 10 / 40 * 0.016f, shallow, 3);
        Assert.Equal(3 * shallow, deep, 3);
    }

    [Fact]
    public void AutoScroll_IsCappedOutsideTheGrid()
    {
        var g = RowDragGrid();
        StartMouseDrag(g, g.RowY(1));

        g.Move(150, 2000);
        g.Input.UpdateAutoScroll(g.Scroll, 16f);

        Assert.Equal(1200f * 0.016f, g.Scroll.OffsetY, 3);
    }

    [Fact]
    public void AutoScroll_StopsAtTheEnd_AndTheDropReachesTheLastRow()
    {
        var g = RowDragGrid();
        var (reordered, _) = Track(g);
        StartMouseDrag(g, g.RowY(1));
        g.Move(150, 250);

        int frames = RunAutoScroll(g);

        Assert.InRange(frames, 1, 999);
        Assert.Equal(g.Scroll.MaxOffsetY, g.Scroll.OffsetY);
        Assert.False(g.Input.IsAutoScrolling);
        Assert.False(g.Input.UpdateAutoScroll(g.Scroll, 16f));

        g.Release(150, 250);
        Assert.Equal(29, Assert.Single(reordered).NewIndex);
    }

    [Fact]
    public void DragIntoTheTopBand_AutoScrollsUp_UntilTheTop()
    {
        var g = RowDragGrid();
        g.Scroll.OffsetY = 360;
        StartMouseDrag(g, 40 + 72 + 18); // row 12

        g.Move(150, 50);
        Assert.True(g.Input.IsAutoScrolling);
        RunAutoScroll(g);

        Assert.Equal(0, g.Scroll.OffsetY);
        Assert.False(g.Input.IsAutoScrolling);
    }

    [Fact]
    public void AutoScroll_OnlyInTheBands_AndNeverAfterTheDrop()
    {
        var g = RowDragGrid();
        StartMouseDrag(g, g.RowY(1));

        g.Move(150, 150);
        Assert.False(g.Input.IsAutoScrolling);
        Assert.False(g.Input.UpdateAutoScroll(g.Scroll, 16f));

        g.Move(150, 250);
        Assert.True(g.Input.IsAutoScrolling);
        g.Release(150, 250);

        Assert.False(g.Input.IsAutoScrolling);
        Assert.False(g.Input.UpdateAutoScroll(g.Scroll, 16f));
    }

    [Fact]
    public void AutoScroll_AtTheTopEdgeOfAnUnscrolledGrid_DoesNothing()
    {
        var g = RowDragGrid();
        StartMouseDrag(g, g.RowY(3));

        g.Move(150, 45);

        Assert.False(g.Input.IsAutoScrolling);
    }

    [Fact]
    public void AutoScroll_UnderFrozenRows_StartsBelowThem()
    {
        // Frozen rows 0-1 cover 40-112: the top band is 112-152, and a pointer over the frozen rows scrolls at full
        // speed until the top, then drops between them.
        var g = RowDragGrid(more: x => x.Source.FrozenRowCount = 2);
        g.Scroll.OffsetY = 100;
        StartMouseDrag(g, 200);

        g.Move(150, 160);
        Assert.False(g.Input.IsAutoScrolling);
        g.Move(150, 90);
        Assert.True(g.Input.IsAutoScrolling);
        g.Input.UpdateAutoScroll(g.Scroll, 16f);
        Assert.Equal(100 - 1200f * 0.016f, g.Scroll.OffsetY, 3);
    }

    [Fact]
    public void Disabling_StopsTheDragAndTheAutoScroll()
    {
        var g = RowDragGrid();
        StartMouseDrag(g, g.RowY(1));
        g.Move(150, 250);

        g.Input.IsEnabled = false;

        Assert.Equal(-1, g.Input.DragRowIndex);
        Assert.False(g.Input.IsAutoScrolling);
    }

    // ── Drop results and events ──

    [Fact]
    public void Drop_RaisesRowReorderedOnce_AfterOneRebuild()
    {
        var g = RowDragGrid();
        var (reordered, rejected) = Track(g);
        var kinds = new List<KumikoViewChangeKind>();
        g.Source.ViewChanged += (_, e) => kinds.Add(e.Kind);
        int rebuildsSeenByHandler = -1;
        g.Input.RowReordered += (_, _) => rebuildsSeenByHandler = kinds.Count;

        StartMouseDrag(g, g.RowY(0));
        g.Release(150, g.RowY(2));

        Assert.Single(reordered);
        Assert.Empty(rejected);
        Assert.Equal(new[] { KumikoViewChangeKind.Rebuild }, kinds);
        Assert.Equal(1, rebuildsSeenByHandler);
        Assert.Equal(new[] { "N01", "N02", "N00" }, g.Items.Take(3).Select(i => i.Name));
    }

    [Fact]
    public void Drop_WhileSorted_IsRejected_AndNothingMoves()
    {
        var g = RowDragGrid();
        g.Source.SetSort(g.Score, KumikoSortDirection.Descending);
        var before = g.Items.ToList();
        var (reordered, rejected) = Track(g);

        StartMouseDrag(g, g.RowY(0));
        g.Release(150, g.RowY(3));

        Assert.Empty(reordered);
        var e = Assert.Single(rejected);
        Assert.Equal(KumikoReorderResult.NotSupportedSorted, e.Result);
        Assert.Equal((0, 3), (e.OldIndex, e.NewIndex));
        Assert.Equal(before, g.Items);
    }

    [Fact]
    public void Drop_OnItself_OrCanceled_RaisesNothing()
    {
        var g = RowDragGrid();
        var (reordered, rejected) = Track(g);

        StartMouseDrag(g, g.RowY(2));
        g.Release(150, g.RowY(2) + 10);
        StartMouseDrag(g, g.RowY(2));
        g.Move(150, g.RowY(4));
        g.Pointer(InputAction.Cancelled, 150, g.RowY(4));

        Assert.Empty(reordered);
        Assert.Empty(rejected);
        Assert.Equal(-1, g.Input.DragRowIndex);
    }

    // ── Drop math under grouping: within the dragged row's group only ──

    /// <summary>
    /// 6 rows grouped by City: 0 [Oslo], 1 N00, 2 N02, 3 N04, 4 [Rome], 5 N01, 6 N03, 7 N05. Rows start at 80
    /// (grouping panel + header), so row r spans 80 + 36 r to 116 + 36 r.
    /// </summary>
    private static InputGrid GroupedGrid() => RowDragGrid(rows: 6, viewportHeight: 500,
        more: x => x.Source.AddGroupDescription(new GroupDescription("City")));

    private static float GroupedRowY(int row) => 80 + 36 * row + 18;

    private static string[] Visible(InputGrid g) =>
        Enumerable.Range(0, g.Source.RowCount)
            .Select(r => g.Source.IsGroupHeaderRow(r) ? "[" + g.Source.GetGroupHeaderInfo(r)!.DisplayText + "]" : g.NameAt(r))
            .ToArray();

    [Fact]
    public void GroupedDrop_WithinTheGroup_MovesTheRow()
    {
        var g = GroupedGrid();
        var (reordered, _) = Track(g);

        StartMouseDrag(g, GroupedRowY(1));
        Assert.Equal(1, g.Input.DragRowIndex);
        Assert.Equal(116 + 16, g.Input.DragRowScreenY); // the ghost follows the panel offset
        g.Release(150, GroupedRowY(3));

        var e = Assert.Single(reordered);
        Assert.Equal((1, 3), (e.OldIndex, e.NewIndex));
        Assert.Equal(new[] { "[Oslo]", "N02", "N04", "N00", "[Rome]", "N01", "N03", "N05" }, Visible(g));
    }

    [Fact]
    public void GroupedDrop_UpWithinTheGroup_MovesTheRow()
    {
        var g = GroupedGrid();

        StartMouseDrag(g, GroupedRowY(7));
        g.Release(150, GroupedRowY(5));

        Assert.Equal(new[] { "[Oslo]", "N00", "N02", "N04", "[Rome]", "N05", "N01", "N03" }, Visible(g));
    }

    [Theory]
    [InlineData(6, KumikoReorderResult.NotSupportedAcrossGroups)] // a Rome row
    [InlineData(4, KumikoReorderResult.OutOfRange)]               // the Rome header
    public void GroupedDrop_OutsideTheGroup_IsRejected(int target, KumikoReorderResult expected)
    {
        var g = GroupedGrid();
        var before = g.Items.ToList();
        var (reordered, rejected) = Track(g);

        StartMouseDrag(g, GroupedRowY(2));
        g.Release(150, GroupedRowY(target));

        Assert.Empty(reordered);
        var e = Assert.Single(rejected);
        Assert.Equal(expected, e.Result);
        Assert.Equal((2, target), (e.OldIndex, e.NewIndex));
        Assert.Equal(before, g.Items);
    }

    [Fact]
    public void GroupedDrag_OfAGroupHeader_NeverStarts()
    {
        var g = GroupedGrid();

        Touched(g, InputAction.LongPress, 150, GroupedRowY(4));

        Assert.Equal(-1, g.Input.DragRowIndex);
    }
}
