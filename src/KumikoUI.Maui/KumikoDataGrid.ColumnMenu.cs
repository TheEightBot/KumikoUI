using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui;

/// <summary>
/// The built-in column header menu (#319): a drawn menu with sort, filter, group, hide and auto-fit items, opened by a
/// right click or a long press on a column header, by the header's menu button and by the context-menu key. It looks
/// the same on every platform and opens where the pointer is, which a native MAUI menu cannot.
/// </summary>
public partial class KumikoDataGrid
{
    /// <summary>Wires the column menu's screen-reader announcements; the controller is the grid's own.</summary>
    private void InitializeColumnMenu() =>
        _inputController.AnnouncementRequested += OnColumnMenuAnnouncementRequested;

    private void OnColumnMenuAnnouncementRequested(string message) => Announce(message);

    // ── IsColumnMenuEnabled ──

    /// <summary>Identifies the <see cref="IsColumnMenuEnabled"/> bindable property.</summary>
    public static readonly BindableProperty IsColumnMenuEnabledProperty = BindableProperty.Create(
        nameof(IsColumnMenuEnabled), typeof(bool), typeof(KumikoDataGrid), false,
        propertyChanged: (b, _, n) =>
        {
            var grid = (KumikoDataGrid)b;
            grid._inputController.IsColumnMenuEnabled = (bool)n;
            grid.UpdateColumnMenuButton();
        });

    /// <summary>
    /// Whether the grid shows its own column menu (sort ascending and descending, clear sort, filter, group by this
    /// column or ungroup, hide, auto-fit) on a right click or a long press on a column header, from the header's menu
    /// button (<see cref="ShowColumnMenuButton"/>) and from the context-menu key (the Menu key or Shift+F10, for the
    /// current cell's column). <see cref="ContextMenuRequested"/> is raised first, and the menu opens only when no
    /// handler set <see cref="KumikoContextRequestedEventArgs.Handled"/>: an app that shows its own header menu from
    /// that event sets it before turning this on. Default: false.
    /// </summary>
    public bool IsColumnMenuEnabled
    {
        get => (bool)GetValue(IsColumnMenuEnabledProperty);
        set => SetValue(IsColumnMenuEnabledProperty, value);
    }

    // ── ShowColumnMenuButton ──

    /// <summary>Identifies the <see cref="ShowColumnMenuButton"/> bindable property.</summary>
    public static readonly BindableProperty ShowColumnMenuButtonProperty = BindableProperty.Create(
        nameof(ShowColumnMenuButton), typeof(bool), typeof(KumikoDataGrid), false,
        propertyChanged: (b, _, _) => ((KumikoDataGrid)b).UpdateColumnMenuButton());

    /// <summary>
    /// Whether column headers show a menu button (three dots, left of the filter icon) that opens the column menu with
    /// a tap, so touch users reach it without a long press. Shown only while <see cref="IsColumnMenuEnabled"/> is on.
    /// Default: false.
    /// </summary>
    public bool ShowColumnMenuButton
    {
        get => (bool)GetValue(ShowColumnMenuButtonProperty);
        set => SetValue(ShowColumnMenuButtonProperty, value);
    }

    private void UpdateColumnMenuButton()
    {
        bool show = ShowColumnMenuButton && IsColumnMenuEnabled;
        _inputController.ShowColumnMenuButton = show;
        _renderer.ShowColumnMenuButton = show;
        InvalidateSurface();
    }

    /// <summary>
    /// Raised before a column menu opens, with the column and its items (see <see cref="KumikoColumnMenuItems"/> for
    /// the keys of the default ones): remove, reorder, change or add items, or set
    /// <see cref="KumikoColumnMenuOpeningEventArgs.Cancel"/> to keep the menu closed. It re-surfaces
    /// <see cref="KumikoInputController.ColumnMenuOpening"/>.
    /// </summary>
    public event EventHandler<KumikoColumnMenuOpeningEventArgs>? ColumnMenuOpening
    {
        add => _inputController.ColumnMenuOpening += value;
        remove => _inputController.ColumnMenuOpening -= value;
    }

    /// <summary>
    /// Opens the column menu of <paramref name="column"/> below its header, with its first item highlighted, whether
    /// or not <see cref="IsColumnMenuEnabled"/> is on. <see cref="ColumnMenuOpening"/> is raised first.
    /// </summary>
    /// <param name="column">One of the grid's visible columns.</param>
    /// <returns>Whether the menu opened: false for a column that is not shown, when a handler canceled it, or when it
    /// has no item to choose.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="column"/> is null.</exception>
    public bool ShowColumnMenu(KumikoColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        bool shown = _inputController.ShowColumnMenu(column, _scroll, _selection, _style, _dataSource);
        InvalidateSurface();
        return shown;
    }
}
