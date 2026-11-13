using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

public class KumikoColumnTests
{
    // ── Defaults ──────────────────────────────────────────────────

    [Fact]
    public void DefaultValues_AreReasonable()
    {
        var col = new KumikoColumn();
        Assert.Equal(string.Empty, col.Header);
        Assert.Equal(string.Empty, col.PropertyName);
        Assert.Equal(KumikoColumnType.Text, col.ColumnType);
        Assert.Equal(120f, col.Width);
        Assert.Equal(40f, col.MinWidth);
        Assert.Equal(float.MaxValue, col.MaxWidth);
        Assert.Equal(ColumnSizeMode.Fixed, col.SizeMode);
        Assert.Equal(1f, col.StarWeight);
        Assert.True(col.IsVisible);
        Assert.False(col.IsReadOnly);
        Assert.True(col.AllowTabStop);
        Assert.False(col.AllowSorting); // no property to sort by yet (#15)
        Assert.True(new KumikoColumn { PropertyName = "Name" }.AllowSorting);
        Assert.True(col.AllowResize);
        Assert.True(col.AllowReorder);
        Assert.True(col.AllowFiltering);
        Assert.Equal(KumikoSortDirection.None, col.SortDirection);
        Assert.Equal(0, col.SortOrder);
        Assert.Equal(ColumnFreezeMode.None, col.FreezeMode);
        Assert.Null(col.ActiveFilter);
    }

    [Fact]
    public void Width_IsExplicitOnlyOnceSet()
    {
        var col = new KumikoColumn();
        Assert.False(col.HasExplicitWidth);
        Assert.Equal(KumikoColumn.DefaultWidth, col.Width);
        Assert.Equal(0f, col.HeaderFitWidth);

        col.Width = KumikoColumn.DefaultWidth;

        Assert.True(col.HasExplicitWidth);
    }

    [Fact]
    public void ConfigurationChange_RaisesPropertyChanged()
    {
        var col = new KumikoColumn();
        var names = new List<string?>();
        col.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        col.IsVisible = false;
        col.Width = 200f;
        col.MinWidth = 10f;
        col.MaxWidth = 500f;
        col.SizeMode = ColumnSizeMode.Star;
        col.Header = "H";
        col.Format = "N2";
        col.IsReadOnly = true;
        col.FreezeMode = ColumnFreezeMode.Left;
        col.AllowSorting = false;

        Assert.Equal(new[]
        {
            nameof(KumikoColumn.IsVisible), nameof(KumikoColumn.Width), nameof(KumikoColumn.MinWidth),
            nameof(KumikoColumn.MaxWidth), nameof(KumikoColumn.SizeMode), nameof(KumikoColumn.Header),
            nameof(KumikoColumn.Format), nameof(KumikoColumn.IsReadOnly), nameof(KumikoColumn.FreezeMode),
            nameof(KumikoColumn.AllowSorting),
        }, names);
    }

    [Fact]
    public void SettingSameValue_RaisesNothing()
    {
        var col = new KumikoColumn { Header = "A", Width = 100f };
        int count = 0;
        col.PropertyChanged += (_, _) => count++;

        col.Header = "A";
        col.Width = 100f;
        col.IsVisible = true;

        Assert.Equal(0, count);
    }

    [Fact]
    public void LayoutWidth_IsAnOutput_AndRaisesNothing()
    {
        var col = new KumikoColumn { SizeMode = ColumnSizeMode.Star };
        int count = 0;
        col.PropertyChanged += (_, _) => count++;

        col.SetLayoutWidth(333f);

        Assert.Equal(333f, col.Width);
        Assert.Equal(0, count);
    }

    [Fact]
    public void RuntimeState_HasNoPublicSetter()
    {
        foreach (var name in new[]
                 {
                     nameof(KumikoColumn.SortDirection), nameof(KumikoColumn.SortOrder),
                     nameof(KumikoColumn.ActiveFilter), nameof(KumikoColumn.HeaderFitWidth),
                     nameof(KumikoColumn.ContentFitWidth), nameof(KumikoColumn.HasExplicitWidth),
                 })
        {
            var setter = typeof(KumikoColumn).GetProperty(name)!.SetMethod;
            Assert.True(setter == null || !setter.IsPublic, $"{name} must not have a public setter");
        }
        Assert.Null(typeof(KumikoColumn).GetProperty("Id"));
    }

    [Fact]
    public void Source_ChangingIsVisibleOrWidth_RaisesRedraw_WithoutRefresh()
    {
        var a = new KumikoColumn { PropertyName = "A" };
        var b = new KumikoColumn { PropertyName = "B", SizeMode = ColumnSizeMode.Star };
        var source = new KumikoSource();
        source.SetColumns(new[] { a, b });
        var kinds = new List<KumikoViewChangeKind>();
        source.ViewChanged += (_, e) => kinds.Add(e.Kind);
        int version = source.ColumnLayoutVersion;

        a.Width = 80f;
        b.IsVisible = false;

        Assert.Equal(2, kinds.Count);
        Assert.All(kinds, k => Assert.Equal(KumikoViewChangeKind.Redraw, k));
        Assert.NotEqual(version, source.ColumnLayoutVersion);
        Assert.Equal(new[] { a }, source.DisplayColumns);
    }

    [Fact]
    public void Source_StopsObservingReplacedDetachedAndDisposedColumns()
    {
        var a = new KumikoColumn { PropertyName = "A" };
        var b = new KumikoColumn { PropertyName = "B" };
        var source = new KumikoSource();
        int raised = 0;
        source.SetColumns(new[] { a });
        source.ViewChanged += (_, _) => raised++;

        source.SetColumns(new[] { b });
        raised = 0;
        a.Width = 50f;
        Assert.Equal(0, raised);

        source.Detach();
        b.Width = 50f;
        Assert.Equal(0, raised);

        source.Attach();
        raised = 0;
        b.Width = 60f;
        Assert.Equal(1, raised);

        source.Dispose();
        raised = 0;
        b.Width = 70f;
        Assert.Equal(0, raised);
    }

    // ── IsFrozen / FreezeMode ─────────────────────────────────────

    [Fact]
    public void IsFrozen_SetTrue_SetsFreezeModeToLeft()
    {
        var col = new KumikoColumn();
        col.IsFrozen = true;
        Assert.Equal(ColumnFreezeMode.Left, col.FreezeMode);
    }

    [Fact]
    public void IsFrozen_SetFalse_SetsFreezeModeToNone()
    {
        var col = new KumikoColumn { FreezeMode = ColumnFreezeMode.Left };
        col.IsFrozen = false;
        Assert.Equal(ColumnFreezeMode.None, col.FreezeMode);
    }

    [Fact]
    public void IsFrozen_GetTrue_WhenFreezeModeIsLeft()
    {
        var col = new KumikoColumn { FreezeMode = ColumnFreezeMode.Left };
        Assert.True(col.IsFrozen);
    }

    [Fact]
    public void IsFrozen_GetFalse_WhenFreezeModeIsRight()
    {
        var col = new KumikoColumn { FreezeMode = ColumnFreezeMode.Right };
        Assert.False(col.IsFrozen);
    }

    [Fact]
    public void IsFrozenRight_SetTrue_SetsFreezeModeToRight()
    {
        var col = new KumikoColumn();
        col.IsFrozenRight = true;
        Assert.Equal(ColumnFreezeMode.Right, col.FreezeMode);
    }

    [Fact]
    public void IsFrozenRight_SetFalse_SetsFreezeModeToNone()
    {
        var col = new KumikoColumn { FreezeMode = ColumnFreezeMode.Right };
        col.IsFrozenRight = false;
        Assert.Equal(ColumnFreezeMode.None, col.FreezeMode);
    }

    [Fact]
    public void IsFrozenRight_GetTrue_WhenFreezeModeIsRight()
    {
        var col = new KumikoColumn { FreezeMode = ColumnFreezeMode.Right };
        Assert.True(col.IsFrozenRight);
    }

    // ── EditorItemsString ─────────────────────────────────────────

    [Fact]
    public void EditorItemsString_Set_PopulatesEditorItems()
    {
        var col = new KumikoColumn();
        col.EditorItemsString = "Alpha,Beta,Gamma";
        var items = col.EditorItems!.Cast<string>().ToList();
        Assert.Equal(3, items.Count);
        Assert.Contains("Alpha", items);
        Assert.Contains("Beta", items);
        Assert.Contains("Gamma", items);
    }

    [Fact]
    public void EditorItemsString_TrimsWhitespace()
    {
        var col = new KumikoColumn();
        col.EditorItemsString = " Alpha , Beta , Gamma ";
        var items = col.EditorItems!.Cast<string>().ToList();
        Assert.Contains("Alpha", items);
        Assert.Contains("Beta", items);
        Assert.Contains("Gamma", items);
    }

    [Fact]
    public void EditorItemsString_Empty_DoesNotPopulateEditorItems()
    {
        var col = new KumikoColumn();
        col.EditorItemsString = "";
        // Empty string should not override EditorItems
        Assert.Null(col.EditorItems);
    }

    [Fact]
    public void EditorItemsString_Null_DoesNotPopulateEditorItems()
    {
        var col = new KumikoColumn();
        col.EditorItemsString = null;
        Assert.Null(col.EditorItems);
    }

    [Fact]
    public void EditorItemsString_Get_ReturnsSetValue()
    {
        var col = new KumikoColumn();
        col.EditorItemsString = "A,B,C";
        Assert.Equal("A,B,C", col.EditorItemsString);
    }

    [Fact]
    public void EditorItemsString_RemovesEmptyEntries()
    {
        var col = new KumikoColumn();
        col.EditorItemsString = "A,,B,,,C";
        var items = col.EditorItems!.Cast<string>().ToList();
        Assert.Equal(3, items.Count);
    }

    // ── EditTriggers ──────────────────────────────────────────────

    [Fact]
    public void EditTriggers_Default_IsNull()
    {
        var col = new KumikoColumn();
        Assert.Null(col.EditTriggers);
    }

    [Fact]
    public void EditTriggers_SetToNone_StoresNone()
    {
        var col = new KumikoColumn();
        col.EditTriggers = KumikoUI.Core.Editing.EditTrigger.None;
        Assert.Equal(KumikoUI.Core.Editing.EditTrigger.None, col.EditTriggers);
    }

    [Fact]
    public void EditTriggers_SetToSingleTap_StoresSingleTap()
    {
        var col = new KumikoColumn();
        col.EditTriggers = KumikoUI.Core.Editing.EditTrigger.SingleTap;
        Assert.Equal(KumikoUI.Core.Editing.EditTrigger.SingleTap, col.EditTriggers);
    }

    [Fact]
    public void EditTriggers_SetToCombinedFlags_StoresCombinedFlags()
    {
        var col = new KumikoColumn();
        col.EditTriggers = KumikoUI.Core.Editing.EditTrigger.DoubleTap | KumikoUI.Core.Editing.EditTrigger.F2Key;
        Assert.Equal(
            KumikoUI.Core.Editing.EditTrigger.DoubleTap | KumikoUI.Core.Editing.EditTrigger.F2Key,
            col.EditTriggers);
    }

    [Fact]
    public void EditTriggers_SetThenClearedToNull_IsNull()
    {
        var col = new KumikoColumn { EditTriggers = KumikoUI.Core.Editing.EditTrigger.SingleTap };
        col.EditTriggers = null;
        Assert.Null(col.EditTriggers);
    }

    [Fact]
    public void EditTriggers_TwoColumns_IndependentValues()
    {
        var col1 = new KumikoColumn { EditTriggers = KumikoUI.Core.Editing.EditTrigger.SingleTap };
        var col2 = new KumikoColumn { EditTriggers = KumikoUI.Core.Editing.EditTrigger.None };
        Assert.Equal(KumikoUI.Core.Editing.EditTrigger.SingleTap, col1.EditTriggers);
        Assert.Equal(KumikoUI.Core.Editing.EditTrigger.None, col2.EditTriggers);
    }
}

