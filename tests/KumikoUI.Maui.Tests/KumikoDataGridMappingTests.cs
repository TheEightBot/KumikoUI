using System.Collections.ObjectModel;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using Microsoft.Maui.Graphics;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// Tests that the bindable properties of the MAUI <see cref="KumikoDataGrid"/> reach the KumikoUI.Core objects
/// the grid owns (style, data source, selection model, edit session, input controller).
/// </summary>
public class KumikoDataGridMappingTests
{
    private sealed record Person(string Name, int Age);

    private static ObservableCollection<Person> People(int count) =>
        new(Enumerable.Range(0, count).Select(i => new Person($"P{i}", 20 + i)));

    // ── Defaults ─────────────────────────────────────────────────

    [Fact]
    public void NewGrid_BindableDefaults_MatchTheCoreStyleDefaults()
    {
        var grid = new KumikoDataGrid();

        Assert.Equal(grid.RowHeight, grid.GridStyle.RowHeight);
        Assert.Equal(grid.HeaderHeight, grid.GridStyle.HeaderHeight);
    }

    // ── Style sizing ─────────────────────────────────────────────

    [Fact]
    public void RowHeight_Set_ReachesTheStyle()
    {
        var grid = new KumikoDataGrid { RowHeight = 52 };

        Assert.Equal(52f, grid.GridStyle.RowHeight);
    }

    [Fact]
    public void HeaderHeight_Set_ReachesTheStyle()
    {
        var grid = new KumikoDataGrid { HeaderHeight = 64 };

        Assert.Equal(64f, grid.GridStyle.HeaderHeight);
    }

    // ── Style / theme ────────────────────────────────────────────

    [Fact]
    public void GridStyle_Set_ReplacesTheStyleAndFlowsToTheEditSession()
    {
        var grid = new KumikoDataGrid();
        var style = new KumikoStyle { RowHeight = 77 };

        grid.GridStyle = style;

        Assert.Same(style, grid.GridStyle);
        Assert.Same(style, grid.EditSession.Style);
    }

    [Fact]
    public void Theme_Set_AppliesThePresetToTheStyleAndEditSession()
    {
        var grid = new KumikoDataGrid();

        grid.Theme = KumikoThemeMode.Dark;

        Assert.Equal(KumikoTheme.Create(KumikoThemeMode.Dark).BackgroundColor, grid.GridStyle.BackgroundColor);
        Assert.Same(grid.GridStyle, grid.EditSession.Style);
    }

    [Fact]
    public void StyleOptions_SurviveAThemeChange()
    {
        var grid = new KumikoDataGrid { ShowHorizontalGridLines = false };

        grid.Theme = KumikoThemeMode.Dark;

        Assert.False(grid.GridStyle.ShowHorizontalGridLines);
    }

    [Fact]
    public void StyleOptions_AreReappliedToAnAssignedStyle()
    {
        var grid = new KumikoDataGrid { ShowVerticalGridLines = false };
        var style = new KumikoStyle { ShowVerticalGridLines = true };

        grid.GridStyle = style;

        Assert.False(style.ShowVerticalGridLines);
    }

    // ── Nullable style options ───────────────────────────────────

    [Fact]
    public void ShowHorizontalGridLines_Set_ReachesTheStyle()
    {
        var grid = new KumikoDataGrid { ShowHorizontalGridLines = false };

        Assert.False(grid.GridStyle.ShowHorizontalGridLines);
    }

    [Fact]
    public void ShowVerticalGridLines_Set_ReachesTheStyle()
    {
        var grid = new KumikoDataGrid { ShowVerticalGridLines = false };

        Assert.False(grid.GridStyle.ShowVerticalGridLines);
    }

    [Fact]
    public void AllowRowDragDrop_Set_ReachesTheStyle()
    {
        var grid = new KumikoDataGrid { AllowRowDragDrop = true };

        Assert.True(grid.GridStyle.AllowRowDragDrop);
    }

    [Fact]
    public void ShowRowDragHandle_Set_ReachesTheStyle()
    {
        var grid = new KumikoDataGrid { ShowRowDragHandle = true };

        Assert.True(grid.GridStyle.ShowRowDragHandle);
    }

    [Fact]
    public void UnsetStyleOptions_LeaveTheStyleDefaultsAlone()
    {
        var baseline = new KumikoStyle();
        var grid = new KumikoDataGrid();

        Assert.Equal(baseline.ShowHorizontalGridLines, grid.GridStyle.ShowHorizontalGridLines);
        Assert.Equal(baseline.ShowVerticalGridLines, grid.GridStyle.ShowVerticalGridLines);
        Assert.Equal(baseline.AllowRowDragDrop, grid.GridStyle.AllowRowDragDrop);
        Assert.Equal(baseline.ShowRowDragHandle, grid.GridStyle.ShowRowDragHandle);
        Assert.Equal(baseline.AlternateRowBackground, grid.GridStyle.AlternateRowBackground);
    }

    [Fact]
    public void AlternateRowBackground_Set_ConvertsTheColorAndTurnsShadingOn()
    {
        var grid = new KumikoDataGrid { AlternateRowBackground = Color.FromRgba(255, 0, 128, 255) };

        Assert.True(grid.GridStyle.AlternateRowBackground);
        Assert.Equal(new KumikoColor(255, 0, 128, 255), grid.GridStyle.AlternateRowColor);
    }

    // ── Selection ────────────────────────────────────────────────

    [Theory]
    [InlineData(KumikoSelectionMode.None)]
    [InlineData(KumikoSelectionMode.Single)]
    [InlineData(KumikoSelectionMode.Multiple)]
    [InlineData(KumikoSelectionMode.Extended)]
    public void GridSelectionMode_Set_ReachesTheSelectionModel(KumikoSelectionMode mode)
    {
        var grid = new KumikoDataGrid { GridSelectionMode = KumikoSelectionMode.Single };

        grid.GridSelectionMode = mode;

        Assert.Equal(mode, grid.Selection.Mode);
    }

    [Fact]
    public void ClearSelectionOnEmptyAreaTap_Set_ReachesTheSelectionModel()
    {
        var grid = new KumikoDataGrid { ClearSelectionOnEmptyAreaTap = false };

        Assert.False(grid.Selection.ClearSelectionOnEmptyAreaTap);
    }

    // ── Data source ──────────────────────────────────────────────

    [Fact]
    public void FrozenRowCount_Set_ReachesTheDataSource()
    {
        var grid = new KumikoDataGrid { FrozenRowCount = 3 };

        Assert.Equal(3, grid.DataSource.FrozenRowCount);
    }

    [Fact]
    public void ItemsSource_Set_LoadsTheRowsIntoTheDataSource()
    {
        var grid = new KumikoDataGrid { ItemsSource = People(4) };

        Assert.Equal(4, grid.DataSource.RowCount);
    }

    [Fact]
    public void ItemsSource_Null_ClearsTheRows()
    {
        var grid = new KumikoDataGrid { ItemsSource = People(4) };

        grid.ItemsSource = null;

        Assert.Equal(0, grid.DataSource.RowCount);
    }

    [Fact]
    public void ItemsSource_ObservableCollection_FollowsAdditions()
    {
        var items = People(2);
        var grid = new KumikoDataGrid { ItemsSource = items };

        items.Add(new Person("New", 40));

        Assert.Equal(3, grid.DataSource.RowCount);
    }

    [Fact]
    public void ItemsSource_Replaced_StopsFollowingTheOldCollection()
    {
        var old = People(2);
        var grid = new KumikoDataGrid { ItemsSource = old };
        grid.ItemsSource = People(5);

        old.Add(new Person("Late", 99));

        Assert.Equal(5, grid.DataSource.RowCount);
    }

    // ── Columns and summary rows ─────────────────────────────────

    [Fact]
    public void Columns_DefaultsToAnEmptyCollection()
    {
        var grid = new KumikoDataGrid();

        Assert.NotNull(grid.Columns);
        Assert.Empty(grid.Columns);
        Assert.Empty(grid.DataSource.Columns);
    }

    [Fact]
    public void Columns_AddAndRemove_AreMirroredInTheDataSource()
    {
        var grid = new KumikoDataGrid();
        var name = new KumikoColumn { Header = "Name", PropertyName = "Name" };

        grid.Columns.Add(name);
        grid.Columns.Add(new KumikoColumn { Header = "Age", PropertyName = "Age" });
        Assert.Equal(2, grid.DataSource.Columns.Count);

        grid.Columns.Remove(name);
        Assert.Single(grid.DataSource.Columns);
        Assert.Equal("Age", grid.DataSource.Columns[0].Header);
    }

    [Fact]
    public void Columns_ReplacedCollection_IsMirroredAndTheOldOneIsDetached()
    {
        var grid = new KumikoDataGrid();
        var old = grid.Columns;
        var replacement = new ObservableCollection<KumikoColumn> { new() { Header = "A", PropertyName = "Name" } };

        grid.Columns = replacement;
        old.Add(new KumikoColumn { Header = "Stale", PropertyName = "Age" });

        Assert.Single(grid.DataSource.Columns);
        Assert.Equal("A", grid.DataSource.Columns[0].Header);
    }

    [Fact]
    public void Columns_Null_IsCoercedToAnEmptyCollection()
    {
        var grid = new KumikoDataGrid();
        grid.Columns.Add(new KumikoColumn { Header = "A", PropertyName = "Name" });

        grid.Columns = null!;

        Assert.NotNull(grid.Columns);
        Assert.Empty(grid.DataSource.Columns);
    }

    // ── Column order and saved layouts ───────────────────────────

    private static KumikoDataGrid GridWithColumns(out KumikoColumn name, out KumikoColumn age)
    {
        // No announcements: the screen reader is not implemented on the plain net10.0 test host.
        var grid = new KumikoDataGrid { ItemsSource = People(5), AnnounceChanges = false };
        name = new KumikoColumn { Header = "Name", PropertyName = "Name" };
        age = new KumikoColumn { Header = "Age", PropertyName = "Age" };
        grid.Columns.Add(name);
        grid.Columns.Add(age);
        return grid;
    }

    [Fact]
    public void ColumnMovedInTheDataSource_IsMovedInColumns_AndSurvivesALaterColumnChange()
    {
        var grid = GridWithColumns(out var name, out var age);

        grid.DataSource.ReorderColumn(0, 1); // what a header drag does
        Assert.Equal(new[] { age, name }, grid.Columns);

        grid.DataSource.SetDisplayIndex(age, 1);
        Assert.Equal(new[] { name, age }, grid.Columns);

        grid.DataSource.ReorderColumn(0, 1);
        var extra = new KumikoColumn { Header = "Extra", PropertyName = "Name", Key = "extra" };
        grid.Columns.Add(extra); // copies Columns into the source: the moved order must not be undone
        Assert.Equal(new[] { age, name, extra }, grid.DataSource.Columns);
    }

    [Fact]
    public void HeaderDrag_MovesTheColumn_ThenRaisesColumnReordered()
    {
        var grid = GridWithColumns(out var name, out var age); // 120 px each, in a 400 px viewport
        var scroll = new ScrollState { ViewportWidth = 400, ViewportHeight = 300 };
        var seen = new List<(object? Sender, int NewIndex, KumikoColumn[] Columns)>();
        grid.ColumnReordered += (sender, e) => seen.Add((sender, e.NewIndex, grid.Columns.ToArray()));

        foreach (var (action, x) in new[] { (InputAction.Pressed, 60f), (InputAction.Moved, 80f), (InputAction.Released, 200f) })
        {
            grid.InputController.HandlePointer(
                new KumikoPointerEventArgs { Action = action, X = x, Y = 20 },
                scroll, grid.Selection, grid.GridStyle, grid.DataSource);
        }

        var e = Assert.Single(seen);
        Assert.Same(grid, e.Sender);
        Assert.Equal(1, e.NewIndex);
        Assert.Equal(new[] { age, name }, e.Columns); // already moved when the event is raised
        Assert.Equal(new[] { age, name }, grid.DataSource.Columns);
    }

    [Fact]
    public void ApplyLayoutState_RestoresTheLayout_AndMirrorsGroupDescriptions()
    {
        var grid = GridWithColumns(out var name, out var age);
        grid.DataSource.ReorderColumn(0, 1);
        age.Width = 140;
        grid.DataSource.SetSort(name, KumikoSortDirection.Descending);
        grid.GroupDescriptions.Add(new GroupDescription("Age"));
        string json = grid.GetLayoutState().ToJson();

        var restored = GridWithColumns(out var name2, out var age2);
        restored.ApplyLayoutState(KumikoLayoutState.FromJson(json));

        Assert.Equal(new[] { age2, name2 }, restored.Columns);
        Assert.Equal(new[] { age2, name2 }, restored.DataSource.Columns);
        Assert.Equal(140, age2.Width);
        Assert.Equal(KumikoSortDirection.Descending, name2.SortDirection);
        var group = Assert.Single(restored.GroupDescriptions);
        Assert.Equal("Age", group.PropertyName);
        Assert.Same(group, Assert.Single(restored.DataSource.GroupDescriptions));
        Assert.Equal(json, restored.GetLayoutState().ToJson());
    }

    [Fact]
    public void TableSummaryRows_AddAndRemove_AreMirroredInTheDataSource()
    {
        var grid = new KumikoDataGrid();
        var summary = new TableSummaryRow { Name = "Totals" };

        grid.TableSummaryRows.Add(summary);
        Assert.Single(grid.DataSource.TableSummaryRows);

        grid.TableSummaryRows.Remove(summary);
        Assert.Empty(grid.DataSource.TableSummaryRows);
    }

    [Fact]
    public void TableSummaryRows_Null_ClearsTheDataSourceRows()
    {
        var grid = new KumikoDataGrid();
        grid.TableSummaryRows.Add(new TableSummaryRow { Name = "Totals" });

        grid.TableSummaryRows = null!;

        Assert.NotNull(grid.TableSummaryRows);
        Assert.Empty(grid.DataSource.TableSummaryRows);
    }

    // ── Editing ──────────────────────────────────────────────────

    [Fact]
    public void EditTriggers_Set_ReachesTheEditSession()
    {
        var grid = new KumikoDataGrid { EditTriggers = EditTrigger.SingleTap | EditTrigger.F2Key };

        Assert.Equal(EditTrigger.SingleTap | EditTrigger.F2Key, grid.EditSession.EditTriggers);
    }

    [Fact]
    public void EditTextSelectionMode_Set_ReachesTheEditSession()
    {
        var grid = new KumikoDataGrid { EditTextSelectionMode = EditTextSelectionMode.CursorAtEnd };

        Assert.Equal(EditTextSelectionMode.CursorAtEnd, grid.EditSession.TextSelectionMode);
    }

    [Fact]
    public void DismissKeyboardOnEnter_Set_ReachesTheEditSession()
    {
        var grid = new KumikoDataGrid { DismissKeyboardOnEnter = false };

        Assert.False(grid.EditSession.DismissKeyboardOnEnter);
    }

    [Fact]
    public void CellValidator_SetAndCleared_ReachesTheEditSession()
    {
        var grid = new KumikoDataGrid();
        Func<int, int, object?, CellValidationResult> validator = (_, _, _) => CellValidationResult.Valid;

        grid.CellValidator = validator;
        Assert.Same(validator, grid.EditSession.CellValidator);

        grid.CellValidator = null;
        Assert.Null(grid.EditSession.CellValidator);
    }

    // ── Scrolling ────────────────────────────────────────────────

    [Fact]
    public void ScrollSettings_Set_ReachesTheInputController()
    {
        var grid = new KumikoDataGrid();
        var settings = new ScrollSettings();

        grid.ScrollSettings = settings;

        Assert.Same(settings, grid.InputController.ScrollSettings);
    }

    // ── Content ──────────────────────────────────────────────────

    [Fact]
    public void Content_Replaced_Throws()
    {
        var grid = new KumikoDataGrid();

        // The handler also tries to restore the grid's own content before throwing; the outer SetValue then
        // overwrites that restore, so the replaced content is deliberately not asserted here.
        Assert.Throws<InvalidOperationException>(() => grid.Content = new Microsoft.Maui.Controls.Label());
    }
}
