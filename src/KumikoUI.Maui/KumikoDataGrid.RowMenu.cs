using System.Collections.ObjectModel;
using System.Collections.Specialized;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;

namespace KumikoUI.Maui;

/// <summary>
/// The built-in row menu (#331): a drawn menu with copy, copy with headers, edit and delete (and expand and collapse all
/// on a group header row), the swipe items and the app's own items, opened by a right click or a long press on a row and
/// by the context-menu key. It looks the same on every platform, opens where the pointer is, and gives touch users a
/// way to copy rows, which a native MAUI menu cannot.
/// </summary>
public partial class KumikoDataGrid
{
    private readonly ObservableCollection<MauiRowMenuItem> _rowMenuItems = new();

    /// <summary>
    /// Wires <see cref="RowMenuItems"/>. It subscribes to the controller's <c>RowMenuOpening</c> before any app handler
    /// can, so the declared items are in the menu when handlers see it.
    /// </summary>
    private void InitializeRowMenu()
    {
        _rowMenuItems.CollectionChanged += OnRowMenuItemsChanged;
        BindingContextChanged += OnRowMenuBindingContextChanged;
        _inputController.RowMenuOpening += OnRowMenuOpeningAddDeclaredItems;
    }

    // ── IsRowMenuEnabled ──

    /// <summary>Identifies the <see cref="IsRowMenuEnabled"/> bindable property.</summary>
    public static readonly BindableProperty IsRowMenuEnabledProperty = BindableProperty.Create(
        nameof(IsRowMenuEnabled), typeof(bool), typeof(KumikoDataGrid), false,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._inputController.IsRowMenuEnabled = (bool)n);

    /// <summary>
    /// Whether the grid shows its own row menu (copy, copy with headers, edit, delete, the swipe items and
    /// <see cref="RowMenuItems"/>; expand all and collapse all on a group header row) on a right click or a long press on
    /// a data cell, a row's drag handle or a group header row, and from the context-menu key (the Menu key or Shift+F10,
    /// for the current row). <see cref="ContextMenuRequested"/> is raised first, and the menu opens only when no handler
    /// set <see cref="KumikoContextRequestedEventArgs.Handled"/>: an app that shows its own row menu from that event sets
    /// it before turning this on. Delete shows with <see cref="AllowDeleting"/> on; copy needs the clipboard
    /// (<see cref="Clipboard"/>). Default: false.
    /// </summary>
    public bool IsRowMenuEnabled
    {
        get => (bool)GetValue(IsRowMenuEnabledProperty);
        set => SetValue(IsRowMenuEnabledProperty, value);
    }

    /// <summary>
    /// Items the app adds to every row menu, after the grid's own items and a separator. Declare them in XAML as
    /// <see cref="MauiRowMenuItem"/>s: the grid hands them its <c>BindingContext</c>, so
    /// <c>Command="{Binding DuplicateCommand}"</c> binds to the page's view model, and the command receives the row's data
    /// item (null on a group header row) unless <see cref="MauiRowMenuItem.CommandParameter"/> is set. Items of the
    /// controller's own <c>RowMenuItems</c> (<see cref="DrawnMenuItem"/>s) come first. <see cref="RowMenuOpening"/> can
    /// still edit the result.
    /// </summary>
    public ObservableCollection<MauiRowMenuItem> RowMenuItems => _rowMenuItems;

    /// <summary>
    /// Raised before a row menu opens, with the row and its items (see <see cref="KumikoRowMenuItems"/> for the keys of
    /// the default ones): remove, reorder, change or add items, or set <see cref="KumikoRowMenuOpeningEventArgs.Cancel"/>
    /// to keep the menu closed. It re-surfaces <see cref="KumikoInputController.RowMenuOpening"/>.
    /// </summary>
    public event EventHandler<KumikoRowMenuOpeningEventArgs>? RowMenuOpening
    {
        add => _inputController.RowMenuOpening += value;
        remove => _inputController.RowMenuOpening -= value;
    }

    // ── Wiring ──

    /// <summary>Gives every declared item this grid's <c>BindingContext</c> (items are not in the visual tree).</summary>
    private void SyncRowMenuItemContexts()
    {
        var context = BindingContext;
        foreach (var item in _rowMenuItems.Where(i => !ReferenceEquals(i.BindingContext, context)))
            item.BindingContext = context;
    }

    private void OnRowMenuItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncRowMenuItemContexts();

    private void OnRowMenuBindingContextChanged(object? sender, EventArgs e) => SyncRowMenuItemContexts();

    private void OnRowMenuOpeningAddDeclaredItems(object? sender, KumikoRowMenuOpeningEventArgs e)
    {
        if (_rowMenuItems.Count == 0) return;
        SyncRowMenuItemContexts();
        if (e.Items.Count > 0 && !e.Items[^1].IsSeparator) e.Items.Add(DrawnMenuItem.Separator());
        foreach (var item in _rowMenuItems) e.Items.Add(item.ToDrawnMenuItem(e.DataItem));
    }
}
