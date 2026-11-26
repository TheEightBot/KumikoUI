using KumikoUI.Core.Components;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Input;

// Grid-wide search (#317): match navigation and the drawn search panel. The matching itself lives in the source
// (KumikoSource.Search.cs) and the highlighting in the renderer (Rendering/SearchHighlighter.cs).
public sealed partial class KumikoInputController
{
    /// <summary>How long the panel waits after the last keystroke before searching, in milliseconds.</summary>
    internal const int DefaultSearchDebounceMs = 150;

    private DrawnSearchPanel? _searchPanel;
    private KumikoSource? _searchSource;
    private EventHandler? _searchSourceChanged;
    private int _searchDebounceGeneration;
    private bool _searchDebouncePending;

    /// <summary>
    /// Whether Ctrl+F (Command+F on Apple keyboards) opens the drawn search panel (<see cref="ShowSearchPanel"/>).
    /// False by default, which leaves the shortcut unhandled so the page's own find command still works.
    /// </summary>
    public bool AllowSearchPanel { get; set; }

    /// <summary>Whether the drawn search panel is open.</summary>
    public bool IsSearchPanelOpen => _searchPanel != null;

    /// <summary>The open search panel, or null (tests and hosts).</summary>
    internal DrawnSearchPanel? SearchPanel => _searchPanel;

    /// <summary>
    /// The delay between the last keystroke in the search panel and the search; 0 searches at once. The delay
    /// needs a <see cref="SynchronizationContext"/> to come back on; without one the panel searches at once.
    /// </summary>
    internal int SearchDebounceMs { get; set; } = DefaultSearchDebounceMs;

    /// <summary>Raised when the search panel opens, closes, or gains or loses the keyboard (hosts resync text input).</summary>
    internal event EventHandler? SearchPanelStateChanged;

    /// <summary>Raised after <see cref="FindNext"/> or <see cref="FindPrevious"/> moved the current cell to a match (hosts announce it).</summary>
    internal event EventHandler? SearchMatchNavigated;

    /// <summary>The search panel's text box while it has the keyboard.</summary>
    private ITextInputSession? SearchPanelTextInput => _searchPanel is { IsFocused: true } panel ? panel.TextInput : null;

    /// <summary>
    /// Moves the current cell to the next cell that matches <see cref="KumikoSource.SearchText"/>, in reading order
    /// over the searchable columns in display order (the order Tab uses), wrapping from the last match to the first.
    /// Group header and summary rows are skipped; a match inside a collapsed group expands it
    /// (<see cref="KumikoSource.ExpandToItem"/>). The row is selected and scrolled into view. An open edit is
    /// committed first.
    /// </summary>
    /// <param name="scroll">The grid's scroll state.</param>
    /// <param name="selection">The grid's selection.</param>
    /// <param name="style">The grid's style.</param>
    /// <param name="dataSource">The grid's data.</param>
    /// <returns>True when the current cell moved to a match; false when there is no search or no match, or the
    /// edit could not be committed.</returns>
    public bool FindNext(ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource) =>
        Find(scroll, selection, style, dataSource, backwards: false);

    /// <summary>
    /// Moves the current cell to the previous cell that matches <see cref="KumikoSource.SearchText"/>, wrapping from
    /// the first match to the last; otherwise as <see cref="FindNext"/>.
    /// </summary>
    /// <param name="scroll">The grid's scroll state.</param>
    /// <param name="selection">The grid's selection.</param>
    /// <param name="style">The grid's style.</param>
    /// <param name="dataSource">The grid's data.</param>
    /// <returns>True when the current cell moved to a match.</returns>
    public bool FindPrevious(ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource) =>
        Find(scroll, selection, style, dataSource, backwards: true);

    private bool Find(ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource,
        bool backwards)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(dataSource);

        FlushSearchText(dataSource);
        if (!dataSource.IsSearchActive) return false;
        if (!CommitEditBeforeViewChange(dataSource, selection)) return false;
        if (!dataSource.FindSearchMatch(selection.CurrentCell, backwards, out var item, out var column))
        {
            Redraw();
            return false;
        }

        int row = dataSource.ExpandToItem(item);
        int columnIndex = IndexOfColumn(dataSource, column);
        if (row < 0 || columnIndex < 0) return false;

        selection.CurrentCell = new CellPosition(row, columnIndex);
        if (selection.Mode is KumikoSelectionMode.Single or KumikoSelectionMode.Extended)
            selection.HandleRowSelection(row, false, false);
        EnsureCurrentCellVisible(scroll, selection, style, dataSource.Columns, dataSource);
        SearchMatchNavigated?.Invoke(this, EventArgs.Empty);
        Redraw();
        return true;
    }

    private static int IndexOfColumn(KumikoSource dataSource, KumikoColumn column)
    {
        var columns = dataSource.Columns;
        for (int i = 0; i < columns.Count; i++)
            if (ReferenceEquals(columns[i], column)) return i;
        return -1;
    }

    /// <summary>
    /// Opens the drawn search panel at the top right of the data area, or gives it the keyboard when it is already
    /// open, with its text selected. The panel shows <see cref="KumikoSource.SearchText"/> and sets it as the user
    /// types (after a short pause); Enter moves to the next match, Shift+Enter to the previous one, and Escape or
    /// the close button close it (<see cref="CloseSearchPanel"/>). A press on the grid takes the keyboard back
    /// without closing it.
    /// </summary>
    /// <param name="scroll">The grid's scroll state.</param>
    /// <param name="selection">The grid's selection.</param>
    /// <param name="style">The grid's style.</param>
    /// <param name="dataSource">The grid's data.</param>
    public void ShowSearchPanel(ScrollState scroll, KumikoSelectionModel selection, KumikoStyle style, KumikoSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(dataSource);
        if (!_isEnabled) return;

        if (_searchPanel is { } open)
        {
            FocusSearchPanel(open);
            open.SelectAll(); // after the focus, which puts the caret at the end
            return;
        }

        EnsurePopupRedrawWired();
        var panel = new DrawnSearchPanel(dataSource, () => selection.CurrentCell);
        panel.ApplyTheme(style);
        panel.Text = dataSource.SearchText;

        panel.TextEdited += (_, _) => ScheduleSearchText(panel, dataSource);
        panel.NextRequested += (_, _) => FindNext(scroll, selection, style, dataSource);
        panel.PreviousRequested += (_, _) => FindPrevious(scroll, selection, style, dataSource);
        panel.CloseRequested += (_, _) => CloseSearchPanel();
        panel.RedrawRequested += (_, _) => Redraw();
        panel.KeyboardFocusRequested += (_, _) =>
        {
            SearchPanelStateChanged?.Invoke(this, EventArgs.Empty);
            KeyboardFocusRequested?.Invoke(this, EventArgs.Empty);
        };
        panel.CopyRequested += OnEditorCopyRequested;
        panel.PasteRequested += (_, e) =>
            ReadClipboardForPaste(e, panel, static (p, text) => p.PasteText(text), static p => p.TextInput);

        // The panel follows the source's search when something else changes it (a bound property), and redraws
        // when a background count arrives.
        _searchSourceChanged = (_, _) =>
        {
            if (!_searchDebouncePending && panel.Text != dataSource.SearchText)
                panel.Text = dataSource.SearchText;
            Redraw();
        };
        dataSource.SearchChanged += _searchSourceChanged;

        // The top right of the data area, below the header (and the grouping panel), inside the viewport.
        KumikoRect Anchor()
        {
            _popupManager.Viewport = new KumikoRect(0, 0, scroll.ViewportWidth, scroll.ViewportHeight);
            float top = (dataSource.IsGroupingActive ? style.GroupPanelHeight : 0) + style.HeaderHeight + 6;
            float x = Math.Max(4, scroll.ViewportWidth - DrawnSearchPanel.PanelWidth - 12);
            return new KumikoRect(x, top, DrawnSearchPanel.PanelWidth, 0);
        }

        _popupManager.Show(panel, Anchor, PopupPlacement.Below, modeless: true);
        _searchPanel = panel;
        _searchSource = dataSource;
        FocusSearchPanel(panel);
        panel.SelectAll();
    }

    /// <summary>
    /// Closes the drawn search panel and clears the search (<see cref="KumikoSource.SearchText"/> becomes empty), as
    /// Escape in the panel does. Does nothing when the panel is not open.
    /// </summary>
    public void CloseSearchPanel()
    {
        if (_searchPanel is not { } panel) return;
        _popupManager.Close(panel); // OnPopupClosed → OnSearchPanelClosed
    }

    /// <summary>The popup manager closed the panel (Escape, the close button, <see cref="CloseSearchPanel"/>, disabling).</summary>
    private void OnSearchPanelClosed()
    {
        var panel = _searchPanel;
        var source = _searchSource;
        _searchPanel = null;
        _searchSource = null;
        _searchDebounceGeneration++;
        _searchDebouncePending = false;
        if (panel != null) panel.IsFocused = false;
        if (source != null)
        {
            if (_searchSourceChanged != null) source.SearchChanged -= _searchSourceChanged;
            source.SearchText = string.Empty;
        }
        _searchSourceChanged = null;
        SearchPanelStateChanged?.Invoke(this, EventArgs.Empty);
        Redraw();
    }

    private void FocusSearchPanel(DrawnSearchPanel panel)
    {
        panel.IsFocused = true;
        // The platform keyboard (the soft keyboard on mobile) goes to the panel's box, and its caret blinks.
        KeyboardFocusRequested?.Invoke(this, EventArgs.Empty);
        SearchPanelStateChanged?.Invoke(this, EventArgs.Empty);
        Redraw();
    }

    /// <summary>A press outside the panel gives the keyboard back to the grid; the panel stays open.</summary>
    private void BlurSearchPanelOnPress(KumikoPointerEventArgs e, InputAction action)
    {
        if (_searchPanel is not { IsFocused: true } panel) return;
        if (action is not (InputAction.Pressed or InputAction.LongPress) || panel.HitTest(e.X, e.Y)) return;
        panel.IsFocused = false;
        SearchPanelStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ctrl/Cmd+F: open (or focus) the panel when <see cref="AllowSearchPanel"/>; otherwise left unhandled.</summary>
    private bool TryHandleSearchKey(KumikoKeyEventArgs e, ScrollState scroll, KumikoSelectionModel selection,
        KumikoStyle style, KumikoSource dataSource)
    {
        if (e.Key != KumikoKey.F || !e.HasCommand || e.HasAlt || !AllowSearchPanel) return false;
        if (CommitEditBeforeViewChange(dataSource, selection))
            ShowSearchPanel(scroll, selection, style, dataSource);
        e.Handled = true;
        return true;
    }

    // ── Typing in the panel ──────────────────────────────────────

    /// <summary>Search for the panel's text once the user pauses typing (or at once without a delay).</summary>
    private void ScheduleSearchText(DrawnSearchPanel panel, KumikoSource dataSource)
    {
        int generation = ++_searchDebounceGeneration;
        var context = SynchronizationContext.Current;
        if (SearchDebounceMs <= 0 || context == null)
        {
            ApplySearchText(panel, dataSource);
            return;
        }

        _searchDebouncePending = true;
        Task.Delay(SearchDebounceMs).ContinueWith(
            _ => context.Post(_ =>
            {
                if (generation == _searchDebounceGeneration && ReferenceEquals(panel, _searchPanel))
                    ApplySearchText(panel, dataSource);
            }, null),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void ApplySearchText(DrawnSearchPanel panel, KumikoSource dataSource)
    {
        _searchDebouncePending = false;
        dataSource.SearchText = panel.Text;
        Redraw();
    }

    /// <summary>Apply typing the panel has not searched for yet (Enter right after typing).</summary>
    private void FlushSearchText(KumikoSource dataSource)
    {
        if (_searchPanel is not { } panel || !ReferenceEquals(_searchSource, dataSource)) return;
        if (!_searchDebouncePending && panel.Text == dataSource.SearchText) return;
        _searchDebounceGeneration++;
        ApplySearchText(panel, dataSource);
    }
}
