using KumikoUI.Core.Components;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>
/// The built-in column header menu (#319): a <see cref="DrawnMenu"/> with sort, filter, group, hide and auto-fit
/// items, opened by a right click or a long press on a header (after <see cref="ContextRequested"/>), by the header's
/// menu button, by the context-menu key, or in code with <see cref="ShowColumnMenu"/>.
/// </summary>
public sealed partial class KumikoInputController
{
    // The room the menu keeps from the viewport's edges when it is placed at a point.
    private const float ColumnMenuViewportMargin = 4f;

    private DrawnMenu? _columnMenu;
    private bool _columnMenuClosedWired;

    /// <summary>
    /// Whether the grid shows its own column menu (sort ascending and descending, clear sort, filter, group by this
    /// column or ungroup, hide, auto-fit) on a right click or a touch long press on a column header, from the header's
    /// menu button (<see cref="ShowColumnMenuButton"/>) and from the context-menu key (the Menu key or Shift+F10, for
    /// the current cell's column). The click or long press raises <see cref="ContextRequested"/> first; the menu opens
    /// only when no handler set <see cref="KumikoContextRequestedEventArgs.Handled"/>. <see cref="ColumnMenuOpening"/>
    /// lets an app change the items or keep the menu closed. Default false.
    /// </summary>
    public bool IsColumnMenuEnabled { get; set; }

    /// <summary>
    /// Whether column headers have a menu button (three dots, left of the filter icon) that opens the column menu with
    /// a click or a tap, as a way to reach it without a right click or a long press. It opens the menu only while
    /// <see cref="IsColumnMenuEnabled"/> is on. Set the connected <see cref="KumikoRenderer.ShowColumnMenuButton"/> to
    /// the same value so the button is drawn. Default false.
    /// </summary>
    public bool ShowColumnMenuButton
    {
        get => _hitTester.ShowColumnMenuButton;
        set => _hitTester.ShowColumnMenuButton = value;
    }

    /// <summary>
    /// Raised before a column menu opens, with the column and the items it will show (see
    /// <see cref="KumikoColumnMenuItems"/> for the keys of the default items). A handler may remove, reorder, change
    /// or add items, or set <see cref="KumikoColumnMenuOpeningEventArgs.Cancel"/> to keep the menu closed.
    /// </summary>
    public event EventHandler<KumikoColumnMenuOpeningEventArgs>? ColumnMenuOpening;

    /// <summary>The open column menu, or null.</summary>
    internal DrawnMenu? ColumnMenu => _columnMenu;

    /// <summary>
    /// Raised with a text for the platform screen reader: a menu that opened ("Price column menu, 8 items") and the
    /// item the keyboard moved to. The MAUI and Uno grids announce it when their <c>AnnounceChanges</c> is on.
    /// </summary>
    internal event Action<string>? AnnouncementRequested;

    /// <summary>
    /// Opens the column menu of <paramref name="column"/> below its header, with its first item highlighted, as the
    /// context-menu key does. It works whether or not <see cref="IsColumnMenuEnabled"/> is on, and raises
    /// <see cref="ColumnMenuOpening"/>. Any open popup closes first.
    /// </summary>
    /// <param name="column">The column; it must be one of <paramref name="dataSource"/>'s visible columns.</param>
    /// <param name="scroll">The grid's scroll state.</param>
    /// <param name="selection">The grid's selection.</param>
    /// <param name="style">The grid's style.</param>
    /// <param name="dataSource">The grid's data source.</param>
    /// <returns>Whether the menu opened: false for a column that is not shown, when a handler canceled it, or when it
    /// has no item to choose.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public bool ShowColumnMenu(KumikoColumn column, ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style,
        KumikoSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(scroll);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(dataSource);
        return OpenColumnMenu(column, point: null, KumikoPointerDeviceType.Mouse, highlightFirst: true,
            scroll, selection, style, dataSource);
    }

    /// <summary>Opens the column menu for a context request on a header that no handler took.</summary>
    private void OpenColumnMenuForContextRequest(KumikoContextRequestedEventArgs args,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        if (!IsColumnMenuEnabled || args.Handled || !args.IsHeader || args.Column is not { } column) return;
        OpenColumnMenu(column, new KumikoPoint(args.X, args.Y), args.DeviceType, highlightFirst: false,
            scroll, selection, style, dataSource);
    }

    /// <summary>
    /// A release on a header's menu button: opens the column menu below the button when the press landed on the same
    /// button. Returns false for a release elsewhere.
    /// </summary>
    private bool TryHandleColumnMenuButtonRelease(KumikoPointerEventArgs e, KumikoHitTestResult hit,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        if (hit.Region != HitRegion.HeaderMenuButton || hit.Column is not { } column) return false;
        if (IsColumnMenuEnabled && _pressRegion == HitRegion.HeaderMenuButton && _pressColumnIndex == hit.ColumnIndex)
        {
            OpenColumnMenu(column, point: null, _pressDeviceType, highlightFirst: false,
                scroll, selection, style, dataSource);
        }
        e.Handled = true;
        return true;
    }

    /// <summary>The context-menu key: opens the menu of the current cell's column (or the first shown column).</summary>
    private bool TryOpenColumnMenuFromKeyboard(
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        if (!IsColumnMenuEnabled) return false;
        var columns = dataSource.Columns;
        KumikoColumn? column = null;
        int current = selection.CurrentCell.IsValid ? selection.CurrentCell.Column : -1;
        if (current >= 0 && current < columns.Count && columns[current].IsVisible)
        {
            column = columns[current];
        }
        else
        {
            var display = dataSource.DisplayColumns;
            if (display.Count > 0) column = display[0];
        }
        if (column == null) return false;

        // The menu is about the column the user will act on: bring its header into view first
        int index = IndexOfColumn(columns, column);
        if (index >= 0)
        {
            float offsetX = KumikoScroller.GetColumnOffsetX(columns, style, scroll, index);
            scroll.ScrollTo(offsetX, scroll.OffsetY);
        }
        return OpenColumnMenu(column, point: null, KumikoPointerDeviceType.Mouse, highlightFirst: true,
            scroll, selection, style, dataSource);
    }

    /// <summary>
    /// Builds the menu's items, lets <see cref="ColumnMenuOpening"/> edit them, and shows the menu: at
    /// <paramref name="point"/> (a right click or a long press), or below the column's header when it is null.
    /// </summary>
    private bool OpenColumnMenu(KumikoColumn column, KumikoPoint? point, KumikoPointerDeviceType device,
        bool highlightFirst, ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        EnsurePopupRedrawWired();
        int columnIndex = IndexOfColumn(dataSource.Columns, column);
        if (columnIndex < 0 || !column.IsVisible || column.ColumnType == KumikoColumnType.Selection) return false; // chrome: no menu

        var items = BuildColumnMenuItems(column, columnIndex, scroll, selection, style, dataSource);
        if (ColumnMenuOpening is { } handler)
        {
            var args = new KumikoColumnMenuOpeningEventArgs(column, columnIndex, items);
            handler(this, args);
            if (args.Cancel) return false;
        }

        var menu = new DrawnMenu
        {
            ItemHeight = device == KumikoPointerDeviceType.Touch ? DrawnMenu.TouchItemHeight : DrawnMenu.DefaultItemHeight,
        };
        menu.ApplyTheme(style);
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null) menu.Items.Add(items[i]);
        if (menu.SelectableCount == 0) return false;

        if (_popupManager.HasActivePopups) _popupManager.CloseAll();
        if (!_columnMenuClosedWired)
        {
            _popupManager.PopupClosed += OnColumnMenuPopupClosed;
            _columnMenuClosedWired = true;
        }

        menu.RedrawRequested += (_, _) => Redraw();
        menu.CloseRequested += m => _popupManager.Close(m);

        // At the point asked for, or below the header (following it as the grid scrolls); kept inside the viewport and
        // no taller than the room on the roomier side, so a long menu scrolls instead of running off the grid.
        KumikoRect Anchor()
        {
            var viewport = new KumikoRect(0, 0, scroll.ViewportWidth, scroll.ViewportHeight);
            _popupManager.Viewport = viewport;
            var anchor = point is { } p
                ? new KumikoRect(p.X, p.Y, 0, 0)
                : HeaderAnchor(column, scroll, style, dataSource);
            menu.MaxHeight = PopupLayout.RoomFor(anchor, viewport) - ColumnMenuViewportMargin;
            return anchor;
        }

        _popupManager.Show(menu, Anchor, PopupPlacement.Below);
        _columnMenu = menu;
        if (highlightFirst) menu.HighlightFirst();

        // The menu first, then the item the keyboard is on, then every item the keyboard reaches
        Announce(KumikoAnnouncements.MenuOpened(dataSource.Strings,
            KumikoAnnouncements.ColumnMenuName(dataSource.Strings, column, DisplayIndexOrIndex(dataSource, columnIndex)),
            menu.SelectableCount));
        if (highlightFirst && menu.HighlightedIndex >= 0)
            Announce(KumikoAnnouncements.MenuItem(dataSource.Strings, menu.Items[menu.HighlightedIndex]));
        menu.KeyboardHighlightChanged += item => Announce(KumikoAnnouncements.MenuItem(dataSource.Strings, item));
        Redraw();
        return true;
    }

    private void OnColumnMenuPopupClosed(DrawnComponent popup)
    {
        if (popup is not DrawnMenu menu) return;
        if (ReferenceEquals(menu, _columnMenu)) _columnMenu = null;
        menu.OnClosed();
        Redraw();
    }

    /// <summary>The bottom edge of the column's header, from its left edge to its right, in viewport coordinates.</summary>
    private static KumikoRect HeaderAnchor(KumikoColumn column, ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        float x = HeaderScreenX(column, scroll, dataSource, style);
        float bottom = (dataSource.IsGroupingActive ? style.GroupPanelHeight : 0f) + style.HeaderHeight;
        return new KumikoRect(x, bottom, column.Width, 0);
    }

    /// <summary>
    /// The grid's default items for <paramref name="column"/>, as far as the grid and the column allow: sort
    /// ascending and descending and clear sort (sorting allowed), filter (filtering allowed), group by this column or
    /// ungroup (the column has a property name), hide, and auto-fit (the column can be resized).
    /// </summary>
    internal List<DrawnMenuItem> BuildColumnMenuItems(KumikoColumn column, int columnIndex,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        var strings = dataSource.Strings;
        var items = new List<DrawnMenuItem>();

        if (AllowSorting && column.AllowSorting)
        {
            items.Add(new DrawnMenuItem(KumikoColumnMenuItems.SortAscending,
                strings.SortLabel(column.ColumnType, KumikoSortDirection.Ascending),
                () => ApplyMenuSort(column, KumikoSortDirection.Ascending, selection, dataSource))
            {
                Icon = KumikoIcon.SortAscending,
                IsChecked = column.SortDirection == KumikoSortDirection.Ascending ? true : null,
            });
            items.Add(new DrawnMenuItem(KumikoColumnMenuItems.SortDescending,
                strings.SortLabel(column.ColumnType, KumikoSortDirection.Descending),
                () => ApplyMenuSort(column, KumikoSortDirection.Descending, selection, dataSource))
            {
                Icon = KumikoIcon.SortDescending,
                IsChecked = column.SortDirection == KumikoSortDirection.Descending ? true : null,
            });
            items.Add(new DrawnMenuItem(KumikoColumnMenuItems.ClearSort, strings.ColumnMenuClearSort,
                () => ApplyMenuSort(column, KumikoSortDirection.None, selection, dataSource))
            {
                IsEnabled = column.SortDirection != KumikoSortDirection.None,
            });
        }

        if (AllowFiltering && column.AllowFiltering)
        {
            AddSeparator(items);
            bool filtered = column.ActiveFilter is { IsActive: true };
            items.Add(new DrawnMenuItem(KumikoColumnMenuItems.Filter, strings.ColumnMenuFilter, () =>
            {
                if (!CommitEditBeforeViewChange(dataSource, selection)) return;
                ShowFilterPopup(column, IndexOfColumn(dataSource.Columns, column), scroll, style, dataSource);
            })
            {
                Icon = filtered ? KumikoIcon.FilterActive : KumikoIcon.Filter,
            });
        }

        if (!string.IsNullOrEmpty(column.PropertyName))
        {
            AddSeparator(items);
            string property = column.PropertyName;
            if (IsGroupedBy(dataSource, property))
            {
                items.Add(new DrawnMenuItem(KumikoColumnMenuItems.Ungroup, strings.ColumnMenuUngroup, () =>
                {
                    if (!CommitEditBeforeViewChange(dataSource, selection)) return;
                    dataSource.RemoveGroupDescription(property);
                    Redraw();
                }));
            }
            else
            {
                items.Add(new DrawnMenuItem(KumikoColumnMenuItems.GroupBy, strings.ColumnMenuGroupBy, () =>
                {
                    if (!CommitEditBeforeViewChange(dataSource, selection) || IsGroupedBy(dataSource, property)) return;
                    dataSource.AddGroupDescription(new GroupDescription(property));
                    Redraw();
                }));
            }
        }

        AddSeparator(items);
        items.Add(new DrawnMenuItem(KumikoColumnMenuItems.Hide, strings.ColumnMenuHide, () =>
        {
            if (!column.IsVisible || VisibleColumnCount(dataSource) <= 1) return;
            if (!CommitEditBeforeViewChange(dataSource, selection)) return;
            column.IsVisible = false;
            Redraw();
        })
        {
            IsEnabled = VisibleColumnCount(dataSource) > 1,
        });

        if (KumikoHitTester.IsResizable(column))
        {
            items.Add(new DrawnMenuItem(KumikoColumnMenuItems.AutoFit, strings.ColumnMenuAutoFit, () =>
            {
                int index = IndexOfColumn(dataSource.Columns, column);
                if (index < 0) return;
                AutoFitColumnRequested?.Invoke(this, new AutoFitColumnEventArgs(index, column));
                Redraw();
            }));
        }

        return items;

        static void AddSeparator(List<DrawnMenuItem> list)
        {
            if (list.Count > 0 && !list[^1].IsSeparator) list.Add(DrawnMenuItem.Separator());
        }
    }

    /// <summary>Sorts by the column alone (or removes it from the sort), after committing an open edit.</summary>
    private void ApplyMenuSort(KumikoColumn column, KumikoSortDirection direction,
        KumikoSelectionModel selection, KumikoSource dataSource)
    {
        if (!CommitEditBeforeViewChange(dataSource, selection)) return;
        if (IndexOfColumn(dataSource.Columns, column) < 0) return;
        dataSource.SetSort(column, direction);
        Redraw();
    }

    private void Announce(string text)
    {
        if (!string.IsNullOrEmpty(text)) AnnouncementRequested?.Invoke(text);
    }

    private static bool IsGroupedBy(KumikoSource dataSource, string propertyName)
    {
        var groups = dataSource.GroupDescriptions;
        for (int i = 0; i < groups.Count; i++)
            if (groups[i].PropertyName == propertyName) return true;
        return false;
    }

    private static int VisibleColumnCount(KumikoSource dataSource)
    {
        int count = 0;
        var columns = dataSource.Columns;
        for (int i = 0; i < columns.Count; i++)
            if (columns[i].IsVisible) count++;
        return count;
    }

    private static int IndexOfColumn(IReadOnlyList<KumikoColumn> columns, KumikoColumn column)
    {
        for (int i = 0; i < columns.Count; i++)
            if (ReferenceEquals(columns[i], column)) return i;
        return -1;
    }

    private static int DisplayIndexOrIndex(KumikoSource dataSource, int columnIndex)
    {
        int display = dataSource.ToDisplayIndex(columnIndex);
        return display < 0 ? columnIndex : display;
    }
}
