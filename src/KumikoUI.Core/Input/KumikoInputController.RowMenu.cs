using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>
/// The built-in row menu (#331): a <see cref="DrawnMenu"/> with copy, copy with headers, edit and delete (and expand
/// and collapse all on a group header row), the grid's swipe items and the app's own items, opened by a right click or
/// a long press on a row (after <see cref="ContextRequested"/>) and by the context-menu key.
/// </summary>
public sealed partial class KumikoInputController
{
    // Where the touch long press that started a row drag began: lifting the finger there, with no movement, opens the menu.
    private bool _rowDragHoldActive;
    private float _rowDragHoldX, _rowDragHoldY;

    /// <summary>
    /// Whether the grid shows its own row menu on a right click or a touch long press on a data cell, a row's drag
    /// handle or a group header row, and from the context-menu key (the Menu key or Shift+F10, for the current row; with
    /// the column menu on too, the key opens the row menu while a row is current). The click or long press raises
    /// <see cref="ContextRequested"/> first; the menu opens only when no handler set
    /// <see cref="KumikoContextRequestedEventArgs.Handled"/>. On a row that a long press would drag (touch with
    /// <see cref="KumikoStyle.AllowRowDragDrop"/>, or its drag handle) the finger lifted without moving opens it.
    /// <see cref="RowMenuOpening"/> lets an app change the items or keep the menu closed. Default false.
    /// </summary>
    public bool IsRowMenuEnabled { get; set; }

    /// <summary>
    /// Items the app adds to every row menu, after the grid's own items and a separator. Choosing one runs its
    /// <see cref="DrawnMenuItem.Action"/> and <see cref="DrawnMenuItem.Command"/>; the command's parameter is the row's
    /// data item (null on a group header row) unless <see cref="DrawnMenuItem.CommandParameter"/> is set. The menu shows
    /// copies, so changing an item while the menu is open does not change it. <see cref="RowMenuOpening"/> can still
    /// edit the result.
    /// </summary>
    public IList<DrawnMenuItem> RowMenuItems { get; } = new List<DrawnMenuItem>();

    /// <summary>
    /// Raised before a row menu opens, with the row and the items it will show (see <see cref="KumikoRowMenuItems"/> for
    /// the keys of the default items). A handler may remove, reorder, change or add items, or set
    /// <see cref="KumikoRowMenuOpeningEventArgs.Cancel"/> to keep the menu closed. It runs after the items from
    /// <see cref="RowMenuItems"/> were added.
    /// </summary>
    public event EventHandler<KumikoRowMenuOpeningEventArgs>? RowMenuOpening;

    /// <summary>Opens the row menu for a context request on a row that no handler took.</summary>
    private void OpenRowMenuForContextRequest(KumikoContextRequestedEventArgs args,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        if (!IsRowMenuEnabled || args.Handled) return;

        var region = args.Region;
        if (region == HitRegion.GroupChevron) region = HitRegion.GroupHeaderRow;
        bool onRow = region switch
        {
            HitRegion.Cell or HitRegion.RowDragHandle => IsDataRow(dataSource, args.RowIndex),
            HitRegion.GroupHeaderRow => args.RowIndex >= 0 && args.RowIndex < dataSource.RowCount
                && dataSource.IsGroupHeaderRow(args.RowIndex),
            _ => false,
        };
        if (!onRow) return;

        OpenRowMenu(args.RowIndex, args.Column, args.ColumnIndex, region, new KumikoPoint(args.X, args.Y),
            args.DeviceType, highlightFirst: false, scroll, selection, style, dataSource);
    }

    /// <summary>
    /// The context-menu key: opens the menu of the current row below its current cell. Returns whether the key was
    /// taken: the row menu is enabled and a data row or a group header row is current (even when
    /// <see cref="RowMenuOpening"/> kept the menu closed).
    /// </summary>
    private bool TryOpenRowMenuFromKeyboard(
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        if (!IsRowMenuEnabled || !selection.CurrentCell.IsValid) return false;
        int row = selection.CurrentCell.Row;
        if (row < 0 || row >= dataSource.RowCount) return false;
        bool group = dataSource.IsGroupHeaderRow(row);
        if (!group && dataSource.IsNonDataRow(row)) return false;

        int columnIndex = group ? -1 : selection.CurrentCell.Column;
        var columns = dataSource.Columns;
        var column = columnIndex >= 0 && columnIndex < columns.Count ? columns[columnIndex] : null;

        // As a click would: the menu acts on the selection, so the current row is selected when it is not
        if (!group && !selection.IsRowSelected(row)) selection.HandleRowSelection(row, shift: false, control: false);
        EnsureCurrentCellVisible(scroll, selection, style, columns, dataSource);
        OpenRowMenu(row, column, column == null ? -1 : columnIndex,
            group ? HitRegion.GroupHeaderRow : HitRegion.Cell, point: null, KumikoPointerDeviceType.Mouse,
            highlightFirst: true, scroll, selection, style, dataSource);
        return true;
    }

    /// <summary>
    /// Builds the menu's items, lets <see cref="RowMenuOpening"/> edit them, and shows the menu: at
    /// <paramref name="point"/> (a right click or a long press), or below the row's cell when it is null.
    /// </summary>
    private bool OpenRowMenu(int row, KumikoColumn? column, int columnIndex, HitRegion region, KumikoPoint? point,
        KumikoPointerDeviceType device, bool highlightFirst,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        EnsurePopupRedrawWired();
        bool group = region == HitRegion.GroupHeaderRow;
        object? item = group ? null : dataSource.GetItem(row);

        var items = BuildRowMenuItems(row, item, column, columnIndex, group, scroll, selection, style, dataSource);
        if (RowMenuOpening is { } handler)
        {
            var args = new KumikoRowMenuOpeningEventArgs(item, row, column, region, items);
            handler(this, args);
            if (args.Cancel) return false;
        }

        var menu = new DrawnMenu
        {
            ItemHeight = device == KumikoPointerDeviceType.Touch ? DrawnMenu.TouchItemHeight : DrawnMenu.DefaultItemHeight,
        };
        menu.ApplyTheme(style);
        foreach (var entry in items.Where(i => i != null)) menu.Items.Add(entry);
        if (menu.SelectableCount == 0) return false;

        if (_popupManager.HasActivePopups) _popupManager.CloseAll();
        if (!_columnMenuClosedWired)
        {
            _popupManager.PopupClosed += OnColumnMenuPopupClosed; // closes any DrawnMenu: redraws and calls OnClosed
            _columnMenuClosedWired = true;
        }

        menu.RedrawRequested += (_, _) => Redraw();
        menu.CloseRequested += m => _popupManager.Close(m);

        // At the point asked for, or below the current cell (following it as the grid scrolls); kept inside the
        // viewport and no taller than the room on the roomier side, so a long menu scrolls instead of running off.
        KumikoRect Anchor()
        {
            var viewport = new KumikoRect(0, 0, scroll.ViewportWidth, scroll.ViewportHeight);
            _popupManager.Viewport = viewport;
            KumikoRect anchor;
            if (point is { } p)
            {
                anchor = new KumikoRect(p.X, p.Y, 0, 0);
            }
            else
            {
                float groupPanelOffset = dataSource.IsGroupingActive ? style.GroupPanelHeight : 0;
                float topSummaryHeight = dataSource.TopSummaryCount * style.SummaryRowHeight;
                var cell = GetCellScreenBounds(row, Math.Max(columnIndex, 0), scroll, style, dataSource.Columns,
                    groupPanelOffset, topSummaryHeight, dataSource);
                anchor = new KumikoRect(cell.X, cell.Y + cell.Height, cell.Width, 0);
            }
            menu.MaxHeight = PopupLayout.RoomFor(anchor, viewport) - ColumnMenuViewportMargin;
            return anchor;
        }

        _popupManager.Show(menu, Anchor, PopupPlacement.Below);
        if (highlightFirst) menu.HighlightFirst();

        Announce(KumikoAnnouncements.MenuOpened(dataSource.Strings, dataSource.Strings.RowMenuName, menu.SelectableCount));
        if (highlightFirst && menu.HighlightedIndex >= 0)
            Announce(KumikoAnnouncements.MenuItem(dataSource.Strings, menu.Items[menu.HighlightedIndex]));
        menu.KeyboardHighlightChanged += entry => Announce(KumikoAnnouncements.MenuItem(dataSource.Strings, entry));
        Redraw();
        return true;
    }

    /// <summary>
    /// The grid's default items for a row, as far as the grid allows, then the swipe items and
    /// <see cref="RowMenuItems"/>. Data row: copy and copy with headers (disabled with nowhere to copy to); edit (the
    /// cell's column and the grid allow editing); delete (<see cref="AllowDeleting"/> on in a writable grid, with the
    /// number of selected rows when it is more than one). Group header row: expand all and collapse all.
    /// </summary>
    internal List<DrawnMenuItem> BuildRowMenuItems(int row, object? dataItem, KumikoColumn? column, int columnIndex, bool group,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        var strings = dataSource.Strings;
        var items = new List<DrawnMenuItem>();

        if (group)
        {
            items.Add(new DrawnMenuItem(KumikoRowMenuItems.ExpandAllGroups, strings.RowMenuExpandAllGroups, () =>
            {
                if (!CommitEditBeforeViewChange(dataSource, selection)) return;
                dataSource.ExpandAllGroups();
                Redraw();
            }));
            items.Add(new DrawnMenuItem(KumikoRowMenuItems.CollapseAllGroups, strings.RowMenuCollapseAllGroups, () =>
            {
                if (!CommitEditBeforeViewChange(dataSource, selection)) return;
                dataSource.CollapseAllGroups();
                Redraw();
            }));
        }
        else
        {
            int selectedCount = selection.SelectedRows.Count(r => IsDataRow(dataSource, r));
            bool canCopy = selectedCount > 0 && (Clipboard != null || ClipboardCopyRequested != null);
            items.Add(new DrawnMenuItem(KumikoRowMenuItems.Copy, strings.RowMenuCopy,
                () => CopyFromRowMenu(withHeaders: false, selection, dataSource))
            {
                IsEnabled = canCopy,
            });
            items.Add(new DrawnMenuItem(KumikoRowMenuItems.CopyWithHeaders, strings.RowMenuCopyWithHeaders,
                () => CopyFromRowMenu(withHeaders: true, selection, dataSource))
            {
                IsEnabled = canCopy,
            });

            bool canEdit = column != null && columnIndex >= 0 && CanEditFromRowMenu(column);
            bool canDelete = AllowDeleting && !GridReadOnly;
            if (canEdit || canDelete) AddSeparator(items);

            if (canEdit)
            {
                items.Add(new DrawnMenuItem(KumikoRowMenuItems.Edit, strings.RowMenuEdit, () =>
                {
                    // The grid may have changed while the menu was open: edit only the cell the menu was asked on
                    if (!IsDataRow(dataSource, row) || !ReferenceEquals(dataSource.GetItem(row), dataItem)) return;
                    if (!CommitEditBeforeViewChange(dataSource, selection)) return;
                    selection.CurrentCell = new CellPosition(row, columnIndex);
                    TryBeginEdit(selection, dataSource, scroll, style, null);
                    Redraw();
                }));
            }

            if (canDelete)
            {
                string text = selectedCount > 1
                    ? string.Format(dataSource.Culture, strings.RowMenuDeleteRows, selectedCount)
                    : strings.RowMenuDelete;
                items.Add(new DrawnMenuItem(KumikoRowMenuItems.Delete, text,
                    () => DeleteSelectedRows(scroll, selection, style, dataSource))
                {
                    IsEnabled = selectedCount > 0,
                });
            }

            if (dataItem != null)
            {
                AddSwipeItems(items, KumikoRowMenuItems.SwipeItemKeyPrefix + "Leading:", LeadingSwipeItems, dataItem);
                AddSwipeItems(items, KumikoRowMenuItems.SwipeItemKeyPrefix + "Trailing:", TrailingSwipeItems, dataItem);
            }
        }

        var appItems = RowMenuItems.Where(i => i != null).ToList();
        if (appItems.Count > 0)
        {
            AddSeparator(items);
            foreach (var appItem in appItems) items.Add(CopyForRow(appItem, dataItem));
        }

        // A separator with nothing before it (or two in a row) is never drawn; none is left dangling at the end either
        while (items.Count > 0 && items[^1].IsSeparator) items.RemoveAt(items.Count - 1);
        return items;

        static void AddSeparator(List<DrawnMenuItem> list)
        {
            if (list.Count > 0 && !list[^1].IsSeparator) list.Add(DrawnMenuItem.Separator());
        }

        void AddSwipeItems(List<DrawnMenuItem> list, string keyPrefix, IList<IActionButtonDefinition> definitions, object rowItem)
        {
            if (definitions.Count == 0) return;
            AddSeparator(list);
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                list.Add(new DrawnMenuItem(keyPrefix + i, definition.Label, () =>
                {
                    ActionButtonPainter.TryInvoke(definition, rowItem);
                    Redraw();
                })
                {
                    IsEnabled = ActionButtonPainter.IsEnabled(definition, rowItem),
                });
            }
        }
    }

    /// <summary>A copy of an app item for one menu: its command gets the row's item unless the item names a parameter.</summary>
    private static DrawnMenuItem CopyForRow(DrawnMenuItem source, object? dataItem)
    {
        if (source.IsSeparator) return DrawnMenuItem.Separator();
        return new DrawnMenuItem(source.Key, source.Text, source.Action)
        {
            Icon = source.Icon,
            IsEnabled = source.IsEnabled,
            IsChecked = source.IsChecked,
            Command = source.Command,
            CommandParameter = source.CommandParameter ?? dataItem,
        };
    }

    /// <summary>Copies the selection, with or without the header row, and announces how many rows went.</summary>
    private void CopyFromRowMenu(bool withHeaders, KumikoSelectionModel selection, KumikoSource dataSource)
    {
        int rows = selection.SelectedRows.Count(r => IsDataRow(dataSource, r));
        if (CopyRows(selection, dataSource, withHeaders))
            Announce(KumikoAnnouncements.RowsCopied(dataSource.Strings, rows, dataSource.Culture));
    }

    /// <summary>Whether the row menu offers Edit for a cell of <paramref name="column"/>: an editor can open on it.</summary>
    private bool CanEditFromRowMenu(KumikoColumn column) =>
        _editSession is { IsReadOnly: false } && !column.IsReadOnly && column.IsVisible &&
        column.ColumnType is not (KumikoColumnType.Selection or KumikoColumnType.ProgressBar) &&
        GetInteractiveRenderer(column) == null &&
        GetEffectiveEditTriggers(column) != EditTrigger.None;

    /// <summary>
    /// A touch long press that started a row drag, lifted with no movement and with the row menu on, opens the menu
    /// instead of dropping the row (the drag ends where it began). Returns whether it did.
    /// </summary>
    private bool TryOpenRowMenuOnDragHoldRelease(KumikoPointerEventArgs e, IReadOnlyList<KumikoColumn> columns, int totalRows,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        bool held = _rowDragHoldActive && IsRowMenuEnabled &&
            MathF.Abs(e.X - _rowDragHoldX) <= DragThreshold && MathF.Abs(e.Y - _rowDragHoldY) <= DragThreshold;
        if (!held) return false;

        EndRowDrag();
        var hit = _hitTester.HitTest(e.X, e.Y, scroll, style, columns, totalRows, dataSource, e.DeviceType);
        RequestContext(KumikoContextTrigger.LongPress, e, hit, selection, dataSource, scroll, style);
        Redraw();
        return true;
    }
}
