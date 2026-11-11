using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// What a host does on teardown (#59), at the Core level: after Detach, a collection that outlives the grid,
/// such as a singleton view model's, no longer keeps the grid's objects alive. Navigating to and from a grid
/// page repeatedly must not grow what the long-lived collection roots.
/// </summary>
public class HostLifecycleLeakTests
{
    private static ObservableCollection<Person> MakeItems() =>
        new() { new Person { Name = "A" }, new Person { Name = "B" }, new Person { Name = "C" } };

    private static KumikoSource MakeSource(ObservableCollection<Person> items)
    {
        var source = new KumikoSource();
        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Name" } });
        source.SetItems(items);
        return source;
    }

    // Not inlined, so no local of the caller keeps the grid's objects reachable
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Source, WeakReference Binder, WeakReference Model) CreateDetachedGrid(
        ObservableCollection<Person> items, ObservableCollection<object> selected)
    {
        var source = MakeSource(items);
        var model = new KumikoSelectionModel(source);
        var binder = new KumikoSelectionBinder(model);
        binder.AttachSelectedItems(selected);

        // What Cleanup / OnUnloaded do
        source.Detach();
        binder.Detach();

        return (new WeakReference(source), new WeakReference(binder), new WeakReference(model));
    }

    private static void CollectAll()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [Fact]
    public void DetachedGrid_IsNotRootedByLongLivedCollections()
    {
        var items = MakeItems();
        var selected = new ObservableCollection<object>();

        var (source, binder, model) = CreateDetachedGrid(items, selected);
        CollectAll();

        Assert.False(source.IsAlive, "the source is still rooted by the item collection or the items");
        Assert.False(binder.IsAlive, "the binder is still rooted by the SelectedItems list");
        Assert.False(model.IsAlive);
        GC.KeepAlive(items);
        GC.KeepAlive(selected);
    }

    [Fact]
    public void NavigatingToAndFromAGridTwentyTimes_LeavesNothingRooted()
    {
        var items = MakeItems();
        var selected = new ObservableCollection<object>();
        var sources = new List<WeakReference>();
        var binders = new List<WeakReference>();

        for (int i = 0; i < 20; i++)
        {
            var (s, b, _) = CreateDetachedGrid(items, selected);
            sources.Add(s);
            binders.Add(b);
        }
        CollectAll();

        Assert.Equal(0, sources.Count(w => w.IsAlive));
        Assert.Equal(0, binders.Count(w => w.IsAlive));
        GC.KeepAlive(items);
        GC.KeepAlive(selected);
    }

    [Fact]
    public void Binder_AfterDetach_IgnoresListChanges_UntilAttach()
    {
        var items = MakeItems();
        var source = MakeSource(items);
        var model = new KumikoSelectionModel(source);
        var binder = new KumikoSelectionBinder(model);
        var selected = new ObservableCollection<object>();
        binder.AttachSelectedItems(selected);

        binder.Detach();
        selected.Add(items[1]);
        Assert.Empty(model.SelectedItems); // not followed while detached

        binder.Attach(); // the list changed meanwhile: the selection is set from it
        Assert.Single(model.SelectedItems);
        Assert.Same(items[1], model.SelectedItems.Single());

        selected.Add(items[2]); // followed again
        Assert.Equal(2, model.SelectedItems.Count());
    }

    [Fact]
    public void Binder_DetachAndAttach_WithTheOwnedList_AreNoOps()
    {
        var source = MakeSource(MakeItems());
        var model = new KumikoSelectionModel(source);
        var binder = new KumikoSelectionBinder(model);

        binder.Detach();
        binder.Attach();
        model.HandleRowSelection(1, false, false);

        Assert.Single(binder.SelectedItems.Cast<object>());
    }

    [Fact]
    public void Binder_AfterDetach_StillWritesTheGridSelectionIntoTheList()
    {
        var source = MakeSource(MakeItems());
        var model = new KumikoSelectionModel(source);
        var binder = new KumikoSelectionBinder(model);
        var selected = new ObservableCollection<object>();
        binder.AttachSelectedItems(selected);

        binder.Detach();
        model.HandleRowSelection(0, false, false);

        Assert.Single(selected);
    }
}
