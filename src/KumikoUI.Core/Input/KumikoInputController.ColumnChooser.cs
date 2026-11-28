using KumikoUI.Core.Components;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

/// <summary>
/// The column chooser (#335, spec #106): a <see cref="DrawnColumnChooser"/> opened by <see cref="ShowColumnChooser"/>,
/// the column menu's Columns... item, an unhandled header context request (<see cref="ColumnChooserOnHeaderContext"/>)
/// or a keyboard shortcut (<see cref="ColumnChooserShortcut"/>). One popup is open at a time: opening the chooser
/// closes the others, and opening the filter popup closes the chooser.
/// </summary>
public sealed partial class KumikoInputController
{
    // The room the chooser keeps from the viewport's edges.
    private const float ColumnChooserMargin = 4f;

    private DrawnColumnChooser? _columnChooser;
    private bool _columnChooserClosedWired;

    /// <summary>Whether the column chooser is open.</summary>
    public bool IsColumnChooserOpen => _columnChooser != null;

    /// <summary>The open column chooser, or null (tests and hosts).</summary>
    internal DrawnColumnChooser? ColumnChooser => _columnChooser;

    /// <summary>
    /// The key that opens the column chooser while the grid has the keyboard and no editor is open, or null (the
    /// default) for none. A common choice is Ctrl+Alt+C (Command+Option+C on Apple keyboards):
    /// <c>new KumikoShortcut(KumikoKey.C, InputModifiers.Control | InputModifiers.Alt)</c>. It is off by default
    /// because Ctrl+Alt is AltGr on Windows, and AltGr+C types a character on some layouts (Polish "ć"); see
    /// <see cref="KumikoShortcut"/>.
    /// </summary>
    public KumikoShortcut? ColumnChooserShortcut { get; set; }

    /// <summary>
    /// Whether a right click or a long press on a column header opens the column chooser at that header, when no
    /// <see cref="ContextRequested"/> handler set <see cref="KumikoContextRequestedEventArgs.Handled"/> and the column
    /// menu (<see cref="IsColumnMenuEnabled"/>, which has its own Columns... item) did not open. Default false.
    /// </summary>
    public bool ColumnChooserOnHeaderContext { get; set; }

    /// <summary>Raised when the column chooser opens (hosts start the search box's caret blink and show the soft keyboard).</summary>
    public event EventHandler? ColumnChooserOpened;

    /// <summary>
    /// Raised when the column chooser closes, however it closes: Escape, the close button, a press outside it,
    /// <see cref="CloseColumnChooser"/>, another popup opening, or the grid being disabled.
    /// </summary>
    public event EventHandler? ColumnChooserClosed;

    /// <summary>The chooser's search box while the chooser is open.</summary>
    private ITextInputSession? ColumnChooserTextInput => _columnChooser?.ActiveTextInput;

    /// <summary>
    /// Opens the column chooser, or gives it the keyboard when it is already open. Without an
    /// <paramref name="anchor"/> it opens at the top right of the data area, below the header; with one (a button's
    /// rectangle, in grid viewport coordinates) it opens below it, or above it when there is no room below. Any other
    /// open popup closes first. The chooser lists <paramref name="dataSource"/>'s columns and changes them live.
    /// </summary>
    /// <param name="scroll">The grid's scroll state.</param>
    /// <param name="selection">The grid's selection (an open edit is committed before a column changes).</param>
    /// <param name="style">The grid's style.</param>
    /// <param name="dataSource">The grid's data source.</param>
    /// <param name="anchor">Where to open it, in grid viewport coordinates; null for the top right of the data area.</param>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="anchor"/> is null.</exception>
    public void ShowColumnChooser(ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style,
        KumikoSource dataSource, KumikoRect? anchor = null)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(dataSource);
        if (!_isEnabled) return;
        if (_columnChooser is { } open)
        {
            FocusColumnChooser(open);
            return;
        }
        Func<KumikoRect>? fixedAnchor = anchor is { } a ? () => a : null;
        OpenColumnChooser(fixedAnchor, scroll, selection, style, dataSource);
    }

    /// <summary>Closes the column chooser. Does nothing when it is not open.</summary>
    public void CloseColumnChooser()
    {
        if (_columnChooser is { } chooser) _popupManager.Close(chooser); // OnColumnChooserPopupClosed clears it
    }

    /// <summary>
    /// A <see cref="KumikoSource.ColumnsChanged"/> of the grid's source: an open edit whose column was hidden is committed,
    /// or canceled when the commit is refused, since no editor can stay on a column that is not drawn. The MAUI and Uno
    /// grids call it; the chooser itself commits before it changes a column.
    /// </summary>
    internal void OnColumnsChanged(KumikoSelectionModel selection, KumikoSource dataSource)
    {
        if (_editSession is not { IsEditing: true, EditColumn: { IsVisible: false } }) return;
        if (!CommitEditBeforeViewChange(dataSource, selection) && _editSession.IsEditing)
        {
            _editSession.CancelEdit();
            selection.IsEditing = false;
        }
        Redraw();
    }

    /// <summary>Builds the chooser, places it and gives it the keyboard.</summary>
    private void OpenColumnChooser(Func<KumikoRect>? anchor, ScrollState scroll, KumikoSelectionModel selection,
        KumikoStyle style, KumikoSource dataSource)
    {
        EnsurePopupRedrawWired();
        if (_popupManager.HasActivePopups) _popupManager.CloseAll();
        if (!_columnChooserClosedWired)
        {
            _popupManager.PopupClosed += OnColumnChooserPopupClosed;
            _columnChooserClosedWired = true;
        }

        var chooser = new DrawnColumnChooser(dataSource);
        chooser.ApplyTheme(style);
        chooser.BeforeChange = () => CommitEditBeforeViewChange(dataSource, selection);
        chooser.RedrawRequested += (_, _) => Redraw();
        chooser.CloseRequested += (_, _) => CloseColumnChooser();
        chooser.KeyboardFocusRequested += (_, _) => KeyboardFocusRequested?.Invoke(this, EventArgs.Empty);
        chooser.AnnouncementRequested += Announce;
        chooser.SearchCopyRequested += OnEditorCopyRequested;
        chooser.SearchPasteRequested += (_, e) =>
            ReadClipboardForPaste(e, chooser, static (c, text) => c.PasteSearchText(text), static c => c.SearchTextInput);

        // Below the anchor, or at the top right of the data area; never taller than the viewport, kept inside it.
        KumikoRect Anchor()
        {
            var viewport = new KumikoRect(0, 0, scroll.ViewportWidth, scroll.ViewportHeight);
            _popupManager.Viewport = viewport;
            chooser.PopupViewport = viewport;
            var rect = anchor?.Invoke() ?? DefaultColumnChooserAnchor(scroll, style, dataSource);
            float room = PopupLayout.HasArea(viewport)
                ? Math.Max(PopupLayout.RoomFor(rect, viewport), viewport.Height - 2 * ColumnChooserMargin) - ColumnChooserMargin
                : DrawnColumnChooser.DefaultMaxHeight;
            chooser.MaxHeight = Math.Min(DrawnColumnChooser.DefaultMaxHeight, room);
            return rect;
        }

        _popupManager.Show(chooser, Anchor, PopupPlacement.Below);
        _columnChooser = chooser;
        FocusColumnChooser(chooser);
        Announce(KumikoAnnouncements.MenuOpened(dataSource.Strings, chooser.AccessibleName, chooser.EntryCount));
        ColumnChooserOpened?.Invoke(this, EventArgs.Empty);
        Redraw();
    }

    /// <summary>The top right of the data area, below the header and the grouping panel.</summary>
    private static KumikoRect DefaultColumnChooserAnchor(ScrollState scroll, KumikoStyle style, KumikoSource dataSource)
    {
        float top = (dataSource.IsGroupingActive ? style.GroupPanelHeight : 0) + style.HeaderHeight + ColumnChooserMargin;
        float x = Math.Max(ColumnChooserMargin, scroll.ViewportWidth - DrawnColumnChooser.DefaultWidth - 12);
        return new KumikoRect(x, top, DrawnColumnChooser.DefaultWidth, 0);
    }

    private void FocusColumnChooser(DrawnColumnChooser chooser)
    {
        chooser.IsFocused = true;
        // The platform keyboard (the soft keyboard on mobile) goes to the search box, and its caret blinks.
        KeyboardFocusRequested?.Invoke(this, EventArgs.Empty);
        Redraw();
    }

    private void OnColumnChooserPopupClosed(DrawnComponent popup)
    {
        if (popup is not DrawnColumnChooser chooser || !ReferenceEquals(chooser, _columnChooser)) return;
        _columnChooser = null;
        chooser.IsFocused = false;
        chooser.BeforeChange = null;
        ColumnChooserClosed?.Invoke(this, EventArgs.Empty);
        Redraw();
    }

    /// <summary>Opens the chooser for a context request on a header that no handler and no column menu took.</summary>
    private void OpenColumnChooserForContextRequest(KumikoContextRequestedEventArgs args,
        ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        if (!ColumnChooserOnHeaderContext || args.Handled || !args.IsHeader || _columnMenu != null) return;
        if (args.Column is not { } column || IndexOfColumn(dataSource.Columns, column) < 0) return;
        OpenColumnChooser(() => HeaderAnchor(column, scroll, style, dataSource), scroll, selection, style, dataSource);
    }

    /// <summary>The column chooser shortcut, while no editor is open: opens (or focuses) the chooser.</summary>
    private bool TryHandleColumnChooserKey(KumikoKeyEventArgs e, ScrollState scroll, KumikoSelectionModel selection,
        KumikoStyle style, KumikoSource dataSource)
    {
        if (ColumnChooserShortcut is not { } shortcut || !shortcut.Matches(e)) return false;
        if (_editSession is { IsEditing: true }) return false; // the editor keeps its keys
        ShowColumnChooser(scroll, selection, style, dataSource);
        e.Handled = true;
        return true;
    }

    /// <summary>The column menu's Columns... item: the chooser below the column's header.</summary>
    private DrawnMenuItem ColumnChooserMenuItem(KumikoColumn column, ScrollState scroll, KumikoSelectionModel selection,
        KumikoStyle style, KumikoSource dataSource) =>
        new(KumikoColumnMenuItems.Columns, dataSource.Strings.ColumnMenuColumns, () =>
        {
            if (_columnChooser != null) CloseColumnChooser();
            OpenColumnChooser(() => HeaderAnchor(column, scroll, style, dataSource), scroll, selection, style, dataSource);
        });
}
