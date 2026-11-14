using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// The press-release state machine fires each action once, on the release, and only on the target that was pressed
/// (#101, INPUT-15): header sorts, filter icons, group chips and group headers; and a click on a resize grip is not a
/// resize.
/// </summary>
public class PressReleaseTargetTests
{
    private const float PanelY = 20f;
    private const float GroupRowY = 40 + 40 + 18; // grouping panel + header, middle of the first row

    private static InputGrid Grouped() =>
        new(rows: 6, viewportHeight: 400, configure: x =>
        {
            x.Renderer.InputController = x.Input;
            x.Source.AddGroupDescription(new GroupDescription("City"));
        });

    private static List<bool> TrackGroupChanges(InputGrid g)
    {
        var changes = new List<bool>();
        g.Source.GroupExpandedChanged += (_, e) => changes.Add(e.IsExpanded);
        return changes;
    }

    // ── Group headers ──

    [Fact]
    public void TapOnAGroupHeader_TogglesItOnce()
    {
        var g = Grouped();
        var changes = TrackGroupChanges(g);

        g.Tap(150, GroupRowY);

        Assert.Equal(new[] { false }, changes);
    }

    [Fact]
    public void PressOnAGroupHeader_DoesNothingUntilTheRelease()
    {
        var g = Grouped();
        var changes = TrackGroupChanges(g);

        g.Press(150, GroupRowY);

        Assert.Empty(changes);
    }

    [Fact]
    public void ATouchPanThatStartsOnAGroupHeader_DoesNotToggleIt()
    {
        var g = Grouped();
        var changes = TrackGroupChanges(g);

        g.Pointer(InputAction.Pressed, 150, GroupRowY, timestampMs: 1000, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.Moved, 150, GroupRowY + 60, timestampMs: 1016, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.Released, 150, GroupRowY + 60, timestampMs: 1032, deviceType: KumikoPointerDeviceType.Touch);

        Assert.Empty(changes);
    }

    [Fact]
    public void ATouchLongPressOnAGroupHeader_OnlyAsksForAContextMenu()
    {
        var g = Grouped();
        var changes = TrackGroupChanges(g);
        int requests = 0;
        g.Input.ContextRequested += (_, _) => requests++;

        g.Pointer(InputAction.Pressed, 150, GroupRowY, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.LongPress, 150, GroupRowY, deviceType: KumikoPointerDeviceType.Touch);
        g.Pointer(InputAction.Released, 150, GroupRowY, deviceType: KumikoPointerDeviceType.Touch);

        Assert.Equal(1, requests);
        Assert.Empty(changes);
    }

    [Fact]
    public void ChevronPress_AndRowRelease_OfTheSameHeader_Toggle()
    {
        var g = Grouped();
        var changes = TrackGroupChanges(g);

        g.Press(15, GroupRowY);
        g.Release(20 + 8, GroupRowY); // past the chevron, on the same row

        Assert.Equal(new[] { false }, changes);
    }

    [Fact]
    public void PressOnAGroupHeader_ReleasedOnARow_NeitherTogglesNorSelects()
    {
        var g = Grouped();
        var changes = TrackGroupChanges(g);
        int taps = 0;
        g.Input.RowTapped += (_, _) => taps++;

        g.Press(150, 80 + 33); // the bottom of the group header (row 0)
        g.Release(150, 80 + 41); // within the drag threshold, on the first data row

        Assert.False(g.Source.IsGroupHeaderRow(1));
        Assert.Empty(changes);
        Assert.Equal(0, taps);
        Assert.False(g.Selection.CurrentCell.IsValid);
    }

    // ── Group chips ──

    [Fact]
    public void ChipRemove_ReleasedOffTheButton_KeepsTheGrouping()
    {
        var g = Grouped();
        var chip = g.Input.HitTester.RenderedRegions!.Chips[0].Bounds;

        g.Press(chip.Right - 6, PanelY);
        g.Release(chip.X + 8, PanelY); // on the chip, off its remove button

        Assert.Single(g.Source.GroupDescriptions);
        Assert.Equal(KumikoSortDirection.Ascending, g.Source.GroupDescriptions[0].GroupSortDirection);
    }

    [Fact]
    public void ChipTap_ReversesTheGroupSortOnce()
    {
        var g = Grouped();
        var chip = g.Input.HitTester.RenderedRegions!.Chips[0].Bounds;

        g.Tap(chip.X + 8, PanelY);

        Assert.Equal(KumikoSortDirection.Descending, g.Source.GroupDescriptions[0].GroupSortDirection);
    }

    // ── Header sort and filter ──

    [Fact]
    public void PressOnACell_ReleasedOnTheHeader_DoesNotSort()
    {
        var g = new InputGrid();

        g.Press(60, 44); // row 0, 4 px below the header
        g.Release(60, 36); // 8 px up, on the header

        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
    }

    [Fact]
    public void PressOnOneHeader_ReleasedOnTheNext_DoesNotSort()
    {
        var g = new InputGrid();

        g.Press(110, InputGrid.HeaderY); // Name (0..120), left of its grip
        g.Release(117 + 8, InputGrid.HeaderY); // Age, past the grip, within the drag threshold

        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
        Assert.Equal(KumikoSortDirection.None, g.Age.SortDirection);
    }

    [Fact]
    public void HeaderTap_SortsOnce_AndRaisesHeaderTappedOnce()
    {
        var g = new InputGrid();
        int taps = 0;
        g.Input.HeaderTapped += (_, _) => taps++;

        g.Tap(60, InputGrid.HeaderY);

        Assert.Equal(KumikoSortDirection.Ascending, g.Name.SortDirection);
        Assert.Equal(1, taps);
    }

    [Fact]
    public void FilterIcon_PressedOnTheHeaderText_DoesNotOpenTheFilter()
    {
        var g = new InputGrid();

        g.Press(150, InputGrid.HeaderY); // Age's header text
        g.Release(187, InputGrid.HeaderY); // its filter icon

        Assert.False(g.Input.PopupManager.HasActivePopups);
    }

    // ── Resize grip ──

    [Fact]
    public void ClickOnAResizeGrip_WithoutDragging_RaisesNoColumnResized()
    {
        var g = new InputGrid();
        int resized = 0;
        g.Input.ColumnResized += (_, _) => resized++;

        g.Tap(200, InputGrid.HeaderY);

        Assert.Equal(0, resized);
    }

    [Fact]
    public void DoubleClickOnAResizeGrip_AutoFitsOnce_AndRaisesNoColumnResized()
    {
        var g = new InputGrid();
        int resized = 0, autoFits = 0;
        g.Input.ColumnResized += (_, _) => resized++;
        g.Input.AutoFitColumnRequested += (_, _) => autoFits++;

        g.Tap(200, InputGrid.HeaderY);
        g.Tap(200, InputGrid.HeaderY, clicks: 2);

        Assert.Equal(1, autoFits);
        Assert.Equal(0, resized);
    }

    [Fact]
    public void DragOnAResizeGrip_RaisesColumnResizedOnce()
    {
        var g = new InputGrid();
        int resized = 0;
        g.Input.ColumnResized += (_, _) => resized++;

        g.Press(200, InputGrid.HeaderY);
        g.Move(230, InputGrid.HeaderY);
        g.Move(240, InputGrid.HeaderY);
        g.Release(240, InputGrid.HeaderY);

        Assert.Equal(1, resized);
        Assert.Equal(120, g.Age.Width);
    }
}
