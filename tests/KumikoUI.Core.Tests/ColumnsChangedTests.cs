using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Stage 1 of the column chooser (#335, spec #106): <see cref="KumikoSource.ColumnsChanged"/> for visibility, freeze and
/// order changes, batched by <see cref="KumikoSource.BeginUpdate"/>; <see cref="KumikoSource.SetColumnVisible"/> and its
/// guards; the default layout and <see cref="KumikoSource.ResetColumnLayout"/>; the current cell leaving a hidden column.
/// </summary>
public class ColumnsChangedTests
{
    private sealed class Row
    {
        public string A { get; set; } = "";
        public string B { get; set; } = "";
        public string C { get; set; } = "";
        public string D { get; set; } = "";
    }

    private static KumikoSource NewSource(out KumikoColumn[] c, List<KumikoColumnsChangedEventArgs>? events = null)
    {
        c = new[]
        {
            new KumikoColumn { Header = "A", PropertyName = "A" },
            new KumikoColumn { Header = "B", PropertyName = "B" },
            new KumikoColumn { Header = "C", PropertyName = "C" },
            new KumikoColumn { Header = "D", PropertyName = "D" },
        };
        var source = new KumikoSource();
        source.SetColumns(c);
        source.SetItems(new[] { new Row(), new Row(), new Row() });
        if (events != null) source.ColumnsChanged += (_, e) => events.Add(e);
        return source;
    }

    // ── ColumnsChanged ───────────────────────────────────────────

    [Fact]
    public void SettingIsVisible_RaisesVisibility_AndARedraw()
    {
        var events = new List<KumikoColumnsChangedEventArgs>();
        var source = NewSource(out var c, events);
        int views = 0;
        source.ViewChanged += (_, _) => views++;

        c[1].IsVisible = false;

        var e = Assert.Single(events);
        Assert.Equal(KumikoColumnsChangeKind.Visibility, e.Kind);
        Assert.Same(c[1], e.Column);
        Assert.Equal(-1, e.OldIndex);
        Assert.Equal(-1, e.NewIndex);
        Assert.True(views > 0);
        Assert.Equal(3, source.VisibleColumnCount);
    }

    [Fact]
    public void SettingTheSameVisibility_RaisesNothing()
    {
        var events = new List<KumikoColumnsChangedEventArgs>();
        var source = NewSource(out var c, events);

        c[1].IsVisible = true;

        Assert.Empty(events);
        Assert.Equal(4, source.VisibleColumnCount);
    }

    [Fact]
    public void SettingFreezeMode_RaisesFreeze()
    {
        var events = new List<KumikoColumnsChangedEventArgs>();
        NewSource(out var c, events);

        c[2].IsFrozen = true;

        var e = Assert.Single(events);
        Assert.Equal(KumikoColumnsChangeKind.Freeze, e.Kind);
        Assert.Same(c[2], e.Column);
    }

    [Fact]
    public void ReorderColumn_RaisesOrder_WithTheIndices_AndStillRaisesColumnOrderChanged()
    {
        var events = new List<KumikoColumnsChangedEventArgs>();
        var source = NewSource(out var c, events);
        int orderChanged = 0;
        source.ColumnOrderChanged += (_, _) => orderChanged++;

        source.ReorderColumn(0, 2);

        var e = Assert.Single(events);
        Assert.Equal(KumikoColumnsChangeKind.Order, e.Kind);
        Assert.Same(c[0], e.Column);
        Assert.Equal(0, e.OldIndex);
        Assert.Equal(2, e.NewIndex);
        Assert.Equal(1, orderChanged);
    }

    [Fact]
    public void OtherColumnProperties_RaiseNothing()
    {
        var events = new List<KumikoColumnsChangedEventArgs>();
        var source = NewSource(out var c, events);

        c[0].Width = 300;
        c[0].Header = "Renamed";
        source.SetSort(c[0], KumikoSortDirection.Ascending);

        Assert.Empty(events);
    }

    [Fact]
    public void InABatch_OneChange_IsRaisedOnceAtTheEnd_AsItWas()
    {
        var events = new List<KumikoColumnsChangedEventArgs>();
        var source = NewSource(out var c, events);

        using (source.BeginUpdate())
        {
            c[3].IsVisible = false;
            Assert.Empty(events);
        }

        var e = Assert.Single(events);
        Assert.Equal(KumikoColumnsChangeKind.Visibility, e.Kind);
        Assert.Same(c[3], e.Column);
    }

    [Fact]
    public void InABatch_SeveralChanges_AreRaisedOnceAtTheEnd_AsReset()
    {
        var events = new List<KumikoColumnsChangedEventArgs>();
        var source = NewSource(out var c, events);

        using (source.BeginUpdate())
        {
            c[3].IsVisible = false;
            c[0].IsFrozenRight = true;
            using (source.BeginUpdate()) source.ReorderColumn(1, 2);
            Assert.Empty(events); // nested batches wait for the outermost
        }

        var e = Assert.Single(events);
        Assert.Equal(KumikoColumnsChangeKind.Reset, e.Kind);
        Assert.Null(e.Column);
    }

    [Fact]
    public void SetColumns_RaisesNothing()
    {
        var events = new List<KumikoColumnsChangedEventArgs>();
        var source = NewSource(out _, events);

        source.SetColumns(new[] { new KumikoColumn { PropertyName = "A" } });

        Assert.Empty(events);
    }

    // ── SetColumnVisible ─────────────────────────────────────────

    [Fact]
    public void SetColumnVisible_HidesAndShows_AndRaisesColumnsChanged()
    {
        var events = new List<KumikoColumnsChangedEventArgs>();
        var source = NewSource(out var c, events);

        Assert.True(source.SetColumnVisible(c[2], false));
        Assert.False(c[2].IsVisible);
        Assert.True(source.SetColumnVisible(c[2], true));
        Assert.True(c[2].IsVisible);
        Assert.True(source.SetColumnVisible(c[2], true)); // already visible: nothing to do, not refused

        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(KumikoColumnsChangeKind.Visibility, e.Kind));
    }

    [Fact]
    public void SetColumnVisible_RefusesToHideTheLastVisibleColumn()
    {
        var source = NewSource(out var c);
        c[0].IsVisible = c[1].IsVisible = c[2].IsVisible = false;
        var events = new List<KumikoColumnsChangedEventArgs>();
        source.ColumnsChanged += (_, e) => events.Add(e);

        Assert.False(source.SetColumnVisible(c[3], false));

        Assert.True(c[3].IsVisible);
        Assert.Empty(events);
        Assert.Equal(1, source.VisibleColumnCount);
    }

    [Fact]
    public void SetColumnVisible_ASelectionColumnDoesNotCountAsTheLastVisibleColumn()
    {
        var source = NewSource(out var c);
        var selection = new KumikoSelectionColumn();
        source.SetColumns(new[] { selection, c[0], c[1] });
        c[1].IsVisible = false;

        Assert.False(source.SetColumnVisible(c[0], false));
        Assert.True(c[0].IsVisible);
    }

    [Fact]
    public void SetColumnVisible_RefusesToHideAColumnThatDoesNotAllowHiding_ButShowsIt()
    {
        var source = NewSource(out var c);
        c[1].AllowHide = false;

        Assert.False(source.SetColumnVisible(c[1], false));
        Assert.True(c[1].IsVisible);

        c[1].IsVisible = false; // code is not limited by AllowHide
        Assert.True(source.SetColumnVisible(c[1], true));
        Assert.True(c[1].IsVisible);
    }

    [Fact]
    public void SetColumnVisible_ThrowsForAForeignColumn()
    {
        var source = NewSource(out _);

        Assert.Throws<ArgumentException>(() => source.SetColumnVisible(new KumikoColumn(), false));
        Assert.Throws<ArgumentNullException>(() => source.SetColumnVisible(null!, false));
    }

    [Fact]
    public void ShowInChooserAndAllowHide_DefaultToTrue_AndNotify()
    {
        var column = new KumikoColumn();
        var changed = new List<string?>();
        column.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.True(column.ShowInChooser);
        Assert.True(column.AllowHide);
        column.ShowInChooser = false;
        column.AllowHide = false;

        Assert.Equal(new[] { nameof(KumikoColumn.ShowInChooser), nameof(KumikoColumn.AllowHide) }, changed);
    }

    // ── Default layout and Reset ─────────────────────────────────

    [Fact]
    public void ResetColumnLayout_RestoresTheOrderAndVisibilityFromSetColumns_WithOneResetEvent()
    {
        var source = NewSource(out var c);
        c[1].IsVisible = false;
        source.ReorderColumn(3, 0);
        c[0].Width = 333;
        var events = new List<KumikoColumnsChangedEventArgs>();
        source.ColumnsChanged += (_, e) => events.Add(e);
        int orderChanged = 0, views = 0;
        source.ColumnOrderChanged += (_, _) => orderChanged++;
        source.ViewChanged += (_, _) => views++;

        source.ResetColumnLayout();

        Assert.Equal(c, source.Columns);
        Assert.All(c, column => Assert.True(column.IsVisible));
        Assert.Equal(333f, c[0].Width); // only order and visibility are reset
        var e = Assert.Single(events);
        Assert.Equal(KumikoColumnsChangeKind.Reset, e.Kind);
        Assert.Equal(1, orderChanged);
        Assert.Equal(1, views);
    }

    [Fact]
    public void ResetColumnLayout_DoesNothing_WhenTheLayoutIsTheDefault()
    {
        var events = new List<KumikoColumnsChangedEventArgs>();
        var source = NewSource(out _, events);
        Assert.True(source.IsDefaultColumnLayout);

        source.ResetColumnLayout();

        Assert.Empty(events);
    }

    [Fact]
    public void MarkDefaultColumnLayout_MakesTheCurrentLayoutTheOneResetRestores()
    {
        var source = NewSource(out var c);
        c[2].IsVisible = false;
        source.ReorderColumn(0, 1);
        source.MarkDefaultColumnLayout();
        Assert.True(source.IsDefaultColumnLayout);

        c[2].IsVisible = true;
        source.ReorderColumn(1, 3);
        source.ResetColumnLayout();

        Assert.Equal(new[] { c[1], c[0], c[2], c[3] }, source.Columns);
        Assert.False(c[2].IsVisible);
    }

    [Fact]
    public void SetColumns_WithTheSameColumns_KeepsTheDefault_LikeAGridReconnecting()
    {
        var source = NewSource(out var c);
        c[1].IsVisible = false;
        source.ReorderColumn(0, 3);

        source.SetColumns(source.Columns.ToList()); // the same columns come back
        source.ResetColumnLayout();

        Assert.Equal(c, source.Columns);
        Assert.True(c[1].IsVisible);
    }

    [Fact]
    public void SetColumns_WithNewColumns_CapturesThemAsTheDefault()
    {
        var source = NewSource(out var c);
        var extra = new KumikoColumn { PropertyName = "A", IsVisible = false };

        source.SetColumns(new[] { c[2], extra, c[0] });

        Assert.True(source.IsDefaultColumnLayout);
        extra.IsVisible = true;
        source.ResetColumnLayout();
        Assert.False(extra.IsVisible);
        Assert.Equal(new[] { c[2], extra, c[0] }, source.Columns);
    }

    [Fact]
    public void ApplyingALayoutState_DoesNotMoveTheDefault()
    {
        var source = NewSource(out var c);
        c[3].IsVisible = false;
        source.ReorderColumn(3, 0);
        var saved = KumikoLayoutState.Capture(source);
        source.ResetColumnLayout();

        saved.ApplyTo(source);
        Assert.False(source.IsDefaultColumnLayout);
        source.ResetColumnLayout();

        Assert.Equal(c, source.Columns);
        Assert.True(c[3].IsVisible);
    }

    [Fact]
    public void ApplyingALayoutState_RaisesColumnsChangedOnce()
    {
        var source = NewSource(out var c);
        c[3].IsVisible = false;
        source.ReorderColumn(3, 0);
        var saved = KumikoLayoutState.Capture(source);
        source.ResetColumnLayout();
        var events = new List<KumikoColumnsChangedEventArgs>();
        source.ColumnsChanged += (_, e) => events.Add(e);

        saved.ApplyTo(source);

        Assert.Equal(KumikoColumnsChangeKind.Reset, Assert.Single(events).Kind);
    }

    // ── The current cell ─────────────────────────────────────────

    [Fact]
    public void HidingTheCurrentCellsColumn_MovesTheCurrentCellToTheNextVisibleColumn()
    {
        var source = NewSource(out var c);
        var selection = new KumikoSelectionModel(source);
        selection.CurrentCell = new CellPosition(1, 1);
        c[2].IsVisible = false;

        c[1].IsVisible = false;

        Assert.Equal(new CellPosition(1, 3), selection.CurrentCell);
    }

    [Fact]
    public void HidingTheLastDisplayedColumn_MovesTheCurrentCellLeft()
    {
        var source = NewSource(out var c);
        var selection = new KumikoSelectionModel(source);
        selection.CurrentCell = new CellPosition(0, 3);

        c[3].IsVisible = false;

        Assert.Equal(new CellPosition(0, 2), selection.CurrentCell);
    }

    [Fact]
    public void HidingTheCurrentColumn_FollowsTheDisplayOrder_AcrossPanes()
    {
        var source = NewSource(out var c);
        var selection = new KumikoSelectionModel(source);
        c[3].FreezeMode = ColumnFreezeMode.Left; // display order: D | A B C
        selection.CurrentCell = new CellPosition(2, 3);

        c[3].IsVisible = false;

        Assert.Equal(new CellPosition(2, 0), selection.CurrentCell); // right of D is A
    }

    [Fact]
    public void HidingAnotherColumn_LeavesTheCurrentCell()
    {
        var source = NewSource(out var c);
        var selection = new KumikoSelectionModel(source);
        selection.CurrentCell = new CellPosition(0, 1);

        c[2].IsVisible = false;

        Assert.Equal(new CellPosition(0, 1), selection.CurrentCell);
    }
}
