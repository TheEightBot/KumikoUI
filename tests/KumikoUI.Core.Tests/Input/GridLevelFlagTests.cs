using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// The grid-level IsReadOnly, AllowSorting and AllowFiltering switches, alone and combined with the
/// per-column flags (a grid-level false always wins).
/// </summary>
public class GridLevelFlagTests
{
    private const float AgeFilterIconX = 180;

    // ── IsReadOnly ──

    [Fact]
    public void ReadOnly_BeginEditIsRefused()
    {
        var g = new InputGrid();
        g.Edit.IsReadOnly = true;
        g.Select(1, 0);

        g.Key(KumikoKey.F2);
        Assert.False(g.Edit.IsEditing);

        Assert.False(g.Edit.BeginEdit(1, 0, g.Name, g.Source, new KumikoRect(0, 40, 120, 36)));
        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void ReadOnly_TypingAndDoubleTapDoNotOpenAnEditor()
    {
        var g = new InputGrid();
        g.Edit.IsReadOnly = true;
        g.Select(1, 0);

        g.Type('x');
        g.Tap(60, g.RowY(1), clicks: 2);

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("N01", g.NameAt(1));
    }

    [Fact]
    public void ReadOnly_Off_StillEdits()
    {
        var g = new InputGrid();
        g.Edit.IsReadOnly = true;
        g.Edit.IsReadOnly = false;
        g.Select(1, 0);

        g.Key(KumikoKey.F2);

        Assert.True(g.Edit.IsEditing);
    }

    [Fact]
    public void ReadOnly_SetWhileEditing_CancelsTheEdit()
    {
        var g = new InputGrid();
        g.Select(1, 0);
        g.Key(KumikoKey.F2);
        Assert.True(g.Edit.IsEditing);

        g.Edit.IsReadOnly = true;

        Assert.False(g.Edit.IsEditing);
    }

    [Fact]
    public void ReadOnly_BooleanCell_DoesNotToggleOnTapSpaceOrEnter()
    {
        var g = BooleanColumnGrid.Create();
        g.Edit.IsReadOnly = true;

        g.Tap(50, g.RowY(1));
        Assert.False(g.Items[1].On);

        g.Select(1, 0);
        g.Key(KumikoKey.Space);
        g.Key(KumikoKey.Enter);
        Assert.False(g.Items[1].On);
    }

    [Fact]
    public void ReadOnly_BooleanCell_TogglesWhenNotReadOnly()
    {
        var g = BooleanColumnGrid.Create();
        g.Select(1, 0);

        g.Key(KumikoKey.Space);

        Assert.True(g.Items[1].On);
    }

    [Fact]
    public void ReadOnly_TabSkipsEveryCell_AndOpensNoEditor()
    {
        var g = new InputGrid();
        g.Edit.IsReadOnly = true;
        g.Select(0, 0);

        Assert.False(g.Input.WouldHandleTab(g.Selection, g.Source, backwards: false));
        Assert.False(g.Key(KumikoKey.Tab).Handled);

        Assert.False(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(0, 0), g.Selection.CurrentCell);
    }

    [Fact]
    public void ReadOnly_SelectionAndSortingStillWork()
    {
        var g = new InputGrid();
        g.Edit.IsReadOnly = true;

        g.Tap(10, g.RowY(2));
        g.Tap(10, InputGrid.HeaderY);

        Assert.True(g.Selection.IsRowSelected(2));
        Assert.Equal(KumikoSortDirection.Ascending, g.Name.SortDirection);
    }

    [Fact]
    public void ReadOnly_CombinesWithPerColumnReadOnly()
    {
        var g = new InputGrid(configure: x => x.City.IsReadOnly = true);
        g.Select(1, 2);

        g.Key(KumikoKey.F2);
        Assert.False(g.Edit.IsEditing); // column read-only, grid not

        g.Select(1, 0);
        g.Key(KumikoKey.F2);
        Assert.True(g.Edit.IsEditing);  // neither
    }

    // ── AllowSorting ──

    [Fact]
    public void AllowSorting_False_HeaderTapDoesNotSort()
    {
        var g = new InputGrid();
        g.Input.AllowSorting = false;

        g.Tap(10, InputGrid.HeaderY);
        g.Tap(10, InputGrid.HeaderY, InputModifiers.Control);

        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
        Assert.False(g.Source.IsMultiSortActive);
    }

    [Fact]
    public void AllowSorting_True_HeaderTapSorts()
    {
        var g = new InputGrid();
        g.Input.AllowSorting = false;
        g.Input.AllowSorting = true;

        g.Tap(10, InputGrid.HeaderY);

        Assert.Equal(KumikoSortDirection.Ascending, g.Name.SortDirection);
    }

    [Fact]
    public void AllowSorting_GridTrueColumnFalse_ColumnWins()
    {
        var g = new InputGrid(configure: x => x.Name.AllowSorting = false);

        g.Tap(10, InputGrid.HeaderY);

        Assert.Equal(KumikoSortDirection.None, g.Name.SortDirection);
    }

    [Fact]
    public void AllowSorting_False_StillCommitsAnOpenEdit()
    {
        var g = new InputGrid();
        g.Input.AllowSorting = false;
        g.Select(1, 0);
        g.Key(KumikoKey.F2);

        g.Tap(10, InputGrid.HeaderY);

        Assert.False(g.Edit.IsEditing);
    }

    // ── AllowFiltering ──

    [Fact]
    public void AllowFiltering_False_FilterIconIsNotHitTestable()
    {
        var g = new InputGrid();
        Assert.Equal(HitRegion.HeaderFilterIcon, g.Hit(AgeFilterIconX, InputGrid.HeaderY).Region);

        g.Tester.AllowFiltering = false;

        Assert.Equal(HitRegion.HeaderCell, g.Hit(AgeFilterIconX, InputGrid.HeaderY).Region);
    }

    [Fact]
    public void AllowFiltering_False_TapOnTheIconOpensNoPopup()
    {
        var g = new InputGrid();
        g.Input.AllowFiltering = false;
        int opened = 0;
        g.Input.FilterPopupOpened += () => opened++;

        g.Tap(AgeFilterIconX, InputGrid.HeaderY);

        Assert.Equal(0, opened);
        Assert.False(g.Input.PopupManager.HasActivePopups);
    }

    [Fact]
    public void AllowFiltering_False_NoHoverHandOverTheIcon()
    {
        var g = new InputGrid();
        g.Input.AllowFiltering = false;

        g.Hover(AgeFilterIconX, InputGrid.HeaderY);

        Assert.DoesNotContain(KumikoCursor.Hand, g.Cursors);
    }

    [Fact]
    public void AllowFiltering_True_TapOnTheIconOpensThePopup()
    {
        var g = new InputGrid();
        g.Input.AllowFiltering = false;
        g.Input.AllowFiltering = true;
        int opened = 0;
        g.Input.FilterPopupOpened += () => opened++;

        g.Tap(AgeFilterIconX, InputGrid.HeaderY);

        Assert.Equal(1, opened);
    }

    [Fact]
    public void AllowFiltering_False_RendererDrawsNoFilterIcons()
    {
        var g = new InputGrid();
        int FilterIcons()
        {
            var ctx = new RecordingDrawingContext();
            g.Renderer.Render(ctx, g.Source, g.Scroll, g.Selection, g.Style, editSession: g.Edit);
            return ctx.Polylines.Count(l => l.Color == g.Style.FilterIconColor);
        }

        Assert.True(FilterIcons() > 0);

        g.Renderer.AllowFiltering = false;

        Assert.Equal(0, FilterIcons());
    }

    [Fact]
    public void AllowFiltering_GridTrueColumnFalse_ColumnWins()
    {
        var g = new InputGrid(configure: x => x.Age.AllowFiltering = false);

        Assert.Equal(HitRegion.HeaderCell, g.Hit(AgeFilterIconX, InputGrid.HeaderY).Region);
    }
}
