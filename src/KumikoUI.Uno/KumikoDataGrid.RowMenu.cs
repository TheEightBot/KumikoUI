using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// The built-in row menu (#331): a drawn menu with copy, copy with headers, edit and delete (and expand and collapse all
/// on a group header row), the swipe items and the app's own items, opened by a right click or a long press on a row and
/// by the context-menu key. It is drawn like the rest of the grid, so it looks the same on every head and matches the
/// MAUI grid, and gives touch users a way to copy rows.
/// </summary>
public partial class KumikoDataGrid
{
    // ── IsRowMenuEnabled ──

    /// <summary>Identifies the <see cref="IsRowMenuEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsRowMenuEnabledProperty =
        DependencyProperty.Register(
            nameof(IsRowMenuEnabled), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(false, (d, e) => ((KumikoDataGrid)d)._inputController.IsRowMenuEnabled = (bool)e.NewValue));

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
    /// Items the app adds to every row menu, after the grid's own items and a separator. Choosing one runs its
    /// <see cref="DrawnMenuItem.Action"/> and <see cref="DrawnMenuItem.Command"/>; the command's parameter is the row's
    /// data item (null on a group header row) unless <see cref="DrawnMenuItem.CommandParameter"/> is set.
    /// </summary>
    public IList<DrawnMenuItem> RowMenuItems => _inputController.RowMenuItems;

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
}
