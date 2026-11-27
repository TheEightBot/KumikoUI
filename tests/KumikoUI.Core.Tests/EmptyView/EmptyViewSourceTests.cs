using System.Collections.ObjectModel;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.EmptyView;

/// <summary><see cref="KumikoSource.ItemCount"/> and <see cref="KumikoSource.IsFiltered"/> (#326).</summary>
public class EmptyViewSourceTests
{
    private sealed class Item
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    private static KumikoSource Create(IEnumerable<Item?>? items = null)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Name", PropertyName = "Name" },
            new KumikoColumn { Header = "Value", PropertyName = "Value", ColumnType = KumikoColumnType.Numeric },
        });
        if (items != null) source.SetItems(items);
        return source;
    }

    private static List<Item?> Items(int count) =>
        Enumerable.Range(0, count).Select(i => (Item?)new Item { Name = $"N{i}", Value = i }).ToList();

    // ── ItemCount ──

    [Fact]
    public void ItemCount_IsZero_WithoutItems()
    {
        Assert.Equal(0, Create().ItemCount);
        Assert.Equal(0, Create(new List<Item?>()).ItemCount);
    }

    [Fact]
    public void ItemCount_CountsTheBoundCollection_WhateverTheFilterHides()
    {
        var source = Create(Items(5));
        source.SetFilter(_ => false);

        Assert.Equal(0, source.RowCount);
        Assert.Equal(5, source.ItemCount);
    }

    [Fact]
    public void ItemCount_SkipsNullItems()
    {
        var items = Items(3);
        items.Insert(1, null);
        items.Add(null);

        Assert.Equal(3, Create(items).ItemCount);
    }

    [Fact]
    public void ItemCount_FollowsAnObservableCollection()
    {
        var items = new ObservableCollection<Item?>(Items(2));
        var source = Create(items);
        Assert.Equal(2, source.ItemCount);

        items.Add(new Item());
        items.Add(null);
        Assert.Equal(3, source.ItemCount);

        items.RemoveAt(0);
        Assert.Equal(2, source.ItemCount);

        items.Clear();
        Assert.Equal(0, source.ItemCount);
    }

    [Fact]
    public void ItemCount_FollowsAReplacedCollection()
    {
        var source = Create(Items(4));
        Assert.Equal(4, source.ItemCount);

        source.SetItems(Items(1));

        Assert.Equal(1, source.ItemCount);
    }

    [Fact]
    public void ItemCount_FollowsAPlainListEditedThenRefreshed()
    {
        var list = Items(2);
        var source = Create(list);
        Assert.Equal(2, source.ItemCount);

        list.Add(new Item());
        Assert.Equal(3, source.ItemCount); // the list's size changed
    }

    [Fact]
    public void ItemCount_CountsItemsOfAnEnumerableThatIsNotAList()
    {
        var source = Create(Items(3).Where(i => i != null));

        Assert.Equal(3, source.ItemCount);
    }

    // ── IsFiltered ──

    [Fact]
    public void IsFiltered_IsFalse_ByDefault()
    {
        Assert.False(Create(Items(3)).IsFiltered);
    }

    [Fact]
    public void IsFiltered_IsTrue_WithAPredicate_UntilItIsCleared()
    {
        var source = Create(Items(3));

        source.SetFilter(i => ((Item)i).Value > 0);
        Assert.True(source.IsFiltered);

        source.SetFilter(null);
        Assert.False(source.IsFiltered);
    }

    [Fact]
    public void IsFiltered_IsTrue_WithAnActiveColumnFilter_UntilItIsCleared()
    {
        var source = Create(Items(3));
        var name = source.Columns[0];

        source.SetColumnFilter(name, new FilterDescription(name) { SelectedValues = new HashSet<string> { "N1" } });
        Assert.True(source.IsFiltered);

        source.SetColumnFilter(name, null);
        Assert.False(source.IsFiltered);
    }

    [Fact]
    public void IsFiltered_IsFalse_ForAnInactiveColumnFilter()
    {
        var source = Create(Items(3));
        var name = source.Columns[0];

        source.SetColumnFilter(name, new FilterDescription(name));

        Assert.False(source.IsFiltered);
    }

    [Fact]
    public void IsFiltered_IsTrue_ForARowFilteringSearch_ButNotAHighlightOnlyOne()
    {
        var source = Create(Items(3));

        source.SearchText = "N1";
        Assert.False(source.IsFiltered); // highlights only, every row stays

        source.SearchFiltersRows = true;
        Assert.True(source.IsFiltered);

        source.SearchText = "";
        Assert.False(source.IsFiltered);
    }

    [Fact]
    public void ClearAllFilters_ClearsIsFiltered()
    {
        var source = Create(Items(3));
        source.SetFilter(_ => false);
        source.SetColumnFilter(source.Columns[0], new FilterDescription(source.Columns[0]) { SelectedValues = new HashSet<string> { "x" } });

        source.ClearAllFilters();

        Assert.False(source.IsFiltered);
    }

    // ── Kind and text ──

    [Fact]
    public void Kind_IsNoMatches_OnlyWhenItemsExistAndAFilterIsActive()
    {
        var source = Create(new List<Item?>());
        Assert.Equal(KumikoEmptyViewKind.NoItems, source.EmptyViewKind);

        source.SetFilter(_ => false);
        Assert.Equal(KumikoEmptyViewKind.NoItems, source.EmptyViewKind); // nothing to hide

        source.SetItems(Items(2));
        Assert.Equal(KumikoEmptyViewKind.NoMatches, source.EmptyViewKind);

        source.SetFilter(null);
        Assert.Equal(KumikoEmptyViewKind.NoItems, source.EmptyViewKind);
    }

    [Fact]
    public void Text_PrefersTheOverride_ElseTheStrings()
    {
        var strings = new KumikoStrings { EmptyViewNoItems = "A", EmptyViewNoMatches = "B" };

        Assert.Equal("A", EmptyViewState.TextFor(strings, KumikoEmptyViewKind.NoItems, null));
        Assert.Equal("B", EmptyViewState.TextFor(strings, KumikoEmptyViewKind.NoMatches, null));
        Assert.Equal("X", EmptyViewState.TextFor(strings, KumikoEmptyViewKind.NoMatches, "X"));
        Assert.Equal("B", KumikoAnnouncements.EmptyView(strings, KumikoEmptyViewKind.NoMatches));
        Assert.Throws<ArgumentNullException>(() => KumikoAnnouncements.EmptyView(null!, KumikoEmptyViewKind.NoItems));
    }

    [Fact]
    public void CurrentText_IsNull_WithRows_OrWhenDisabled()
    {
        var source = Create(Items(2));
        var style = new KumikoStyle();
        Assert.Null(EmptyViewState.CurrentText(source, style, null, false));

        source.SetFilter(_ => false);
        Assert.Equal(source.Strings.EmptyViewNoMatches, EmptyViewState.CurrentText(source, style, null, false));
        Assert.Null(EmptyViewState.CurrentText(source, style, null, true)); // busy: the data may still be coming

        style.ShowEmptyView = false;
        Assert.Null(EmptyViewState.CurrentText(source, style, null, false));
    }

    // ── Announcer ──

    [Fact]
    public void Announcer_SpeaksOnce_WhenAFilterEmptiesTheView()
    {
        var source = Create(Items(3));
        var style = new KumikoStyle();
        var announcer = new EmptyViewAnnouncer();
        Assert.Null(announcer.Observe(source, style, null, false)); // rows

        source.SetFilter(_ => false);
        Assert.Equal(source.Strings.EmptyViewNoMatches, announcer.Observe(source, style, null, false));
        Assert.Null(announcer.Observe(source, style, null, false)); // still empty: once

        source.SetFilter(null);
        Assert.Null(announcer.Observe(source, style, null, false));
        source.SetFilter(_ => false);
        Assert.NotNull(announcer.Observe(source, style, null, false)); // a new emptying speaks again
    }

    [Fact]
    public void Announcer_StaysQuiet_WhenTheCollectionItselfEmpties_OrTheViewIsDisabled()
    {
        var items = new ObservableCollection<Item?>(Items(2));
        var source = Create(items);
        var style = new KumikoStyle();
        var announcer = new EmptyViewAnnouncer();
        announcer.Observe(source, style, null, false);

        items.Clear();
        Assert.Null(announcer.Observe(source, style, null, false)); // no filter: nothing to explain

        source.SetItems(Items(2));
        announcer.Observe(source, style, null, false);
        style.ShowEmptyView = false;
        source.SetFilter(_ => false);
        Assert.Null(announcer.Observe(source, style, null, false));
    }
}
