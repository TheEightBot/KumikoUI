using KumikoUI.Core.Components;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui;

/// <summary>
/// Grid-wide search (#317): <see cref="SearchText"/> highlights every matching cell (and with
/// <see cref="SearchFiltersRows"/> hides the rows with none), <see cref="FindNext"/> and <see cref="FindPrevious"/> move
/// between the matches, and the drawn search panel (<see cref="ShowSearchPanel"/>, or Ctrl/Cmd+F when
/// <see cref="AllowSearchPanel"/>) gives the user a search box. The matching, the highlight and the panel live in
/// Core; this file binds them to the control and announces the results to screen readers.
/// </summary>
public partial class KumikoDataGrid
{
    private const int SearchAnnounceDelayMs = 500;

    private bool _syncingSearchText;
    private IDispatcherTimer? _searchAnnounceTimer;

    // ── SearchText ──

    /// <summary>Identifies the <see cref="SearchText"/> bindable property.</summary>
    public static readonly BindableProperty SearchTextProperty = BindableProperty.Create(
        nameof(SearchText), typeof(string), typeof(KumikoDataGrid), string.Empty, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b).OnSearchTextPropertyChanged((string?)n));

    /// <summary>
    /// The text the grid-wide search looks for; empty (the default) for none. Every cell whose display text contains
    /// it (under the source's culture and case rules) is highlighted; see <see cref="KumikoSource.SearchText"/>.
    /// Two-way: it follows what the user types in the search panel. Bound to a search box of your own, it searches
    /// on every change, so set it when the user pauses typing if <see cref="SearchFiltersRows"/> is on and the grid
    /// is large.
    /// </summary>
    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    // ── SearchFiltersRows ──

    /// <summary>Identifies the <see cref="SearchFiltersRows"/> bindable property.</summary>
    public static readonly BindableProperty SearchFiltersRowsProperty = BindableProperty.Create(
        nameof(SearchFiltersRows), typeof(bool), typeof(KumikoDataGrid), false,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._dataSource.SearchFiltersRows = (bool)n);

    /// <summary>
    /// Whether the search also hides the rows in which no cell matches <see cref="SearchText"/>. False (the default)
    /// only highlights the matches. Combines with the column filters.
    /// </summary>
    public bool SearchFiltersRows
    {
        get => (bool)GetValue(SearchFiltersRowsProperty);
        set => SetValue(SearchFiltersRowsProperty, value);
    }

    // ── AllowSearchPanel ──

    /// <summary>Identifies the <see cref="AllowSearchPanel"/> bindable property.</summary>
    public static readonly BindableProperty AllowSearchPanelProperty = BindableProperty.Create(
        nameof(AllowSearchPanel), typeof(bool), typeof(KumikoDataGrid), false,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b)._inputController.AllowSearchPanel = (bool)n);

    /// <summary>
    /// Whether Ctrl+F (Command+F on Apple keyboards) opens the grid's drawn search panel while the grid has the
    /// keyboard. False by default, which leaves the shortcut to the page. <see cref="ShowSearchPanel"/> opens the
    /// panel either way.
    /// </summary>
    public bool AllowSearchPanel
    {
        get => (bool)GetValue(AllowSearchPanelProperty);
        set => SetValue(AllowSearchPanelProperty, value);
    }

    /// <summary>
    /// Raised when <see cref="SearchText"/> or <see cref="SearchFiltersRows"/> changes, and when the matches of a
    /// large grid have been counted (<see cref="SearchMatchCount"/>).
    /// </summary>
    public event EventHandler? SearchChanged;

    /// <summary>
    /// The number of cells that match <see cref="SearchText"/>, collapsed groups included; 0 without a search, -1
    /// while a large grid is still counting (<see cref="SearchChanged"/> is raised when it is done).
    /// </summary>
    public int SearchMatchCount => _dataSource.SearchMatchCount;

    /// <summary>
    /// Moves the current cell to the next match of <see cref="SearchText"/> in reading order, wrapping at the end,
    /// expanding a collapsed group that holds it and scrolling it into view.
    /// </summary>
    /// <returns>True when the current cell moved to a match.</returns>
    public bool FindNext() => _inputController.FindNext(_scroll, _selection, _style, _dataSource);

    /// <summary>Moves the current cell to the previous match of <see cref="SearchText"/>, wrapping at the start.</summary>
    /// <returns>True when the current cell moved to a match.</returns>
    public bool FindPrevious() => _inputController.FindPrevious(_scroll, _selection, _style, _dataSource);

    /// <summary>
    /// Opens the drawn search panel at the top right of the data area (or gives it the keyboard when it is open):
    /// a search box with the position among the matches, previous and next buttons and a close button. Enter moves
    /// to the next match, Shift+Enter to the previous one, Escape closes the panel and clears the search.
    /// </summary>
    public void ShowSearchPanel() => _inputController.ShowSearchPanel(_scroll, _selection, _style, _dataSource);

    /// <summary>Closes the drawn search panel and clears <see cref="SearchText"/>, as Escape in the panel does.</summary>
    public void CloseSearchPanel() => _inputController.CloseSearchPanel();

    // ── Wiring (the source and the controller are the grid's own, so nothing here needs removing) ──

    private void InitializeSearch()
    {
        _dataSource.SearchChanged += OnDataSourceSearchChanged;
        _inputController.SearchPanelStateChanged += OnSearchPanelStateChanged;
        _inputController.SearchMatchNavigated += OnSearchMatchNavigated;
    }

    private void OnSearchTextPropertyChanged(string? text)
    {
        if (_syncingSearchText) return;
        _dataSource.SearchText = text ?? string.Empty;
    }

    private void OnDataSourceSearchChanged(object? sender, EventArgs e)
    {
        if (!string.Equals(SearchText, _dataSource.SearchText, StringComparison.Ordinal))
        {
            _syncingSearchText = true;
            try { SearchText = _dataSource.SearchText; }
            finally { _syncingSearchText = false; }
        }
        InvalidateSurface();
        SearchChanged?.Invoke(this, EventArgs.Empty);
        QueueSearchAnnouncement();
    }

    /// <summary>The panel opened, closed, or took or gave back the keyboard: its caret blinks while it types.</summary>
    private void OnSearchPanelStateChanged(object? sender, EventArgs e)
    {
        _filterPopupActive = _inputController.SearchPanel is { IsFocused: true } ||
                             _inputController.ActivePopup is DrawnFilterPopup;
        if (_filterPopupActive)
            StartCursorBlinkTimer();
        else if (!_editSession.IsEditing)
            StopCursorBlinkTimer();
#if ANDROID
        ScheduleAndroidImeUpdate(); // the soft keyboard follows the search box
#endif
        SyncNativeTextInput();
        InvalidateSurface();
    }

    // ── Screen reader ──

    private void OnSearchMatchNavigated(object? sender, EventArgs e)
    {
        var cell = _selection.CurrentCell;
        if (!cell.IsValid || cell.Column >= _dataSource.Columns.Count || cell.Row >= _dataSource.RowCount) return;
        _announceTimer?.Stop(); // the match is announced instead of the plain current cell
        var column = _dataSource.Columns[cell.Column];
        Announce(KumikoAnnouncements.SearchMatch(_dataSource.Strings, column, DisplayColumnIndex(cell.Column), cell.Row,
            _dataSource.GetCellDisplayText(cell.Row, column)));
    }

    /// <summary>Announce the number of matches once the search has settled (typing, then a count in the background).</summary>
    private void QueueSearchAnnouncement()
    {
        if (!AnnounceChanges) return;
        if (_searchAnnounceTimer == null)
        {
            _searchAnnounceTimer = Dispatcher.CreateTimer();
            _searchAnnounceTimer.Interval = TimeSpan.FromMilliseconds(SearchAnnounceDelayMs);
            _searchAnnounceTimer.IsRepeating = false;
            _searchAnnounceTimer.Tick += OnSearchAnnounceTimerTick;
        }
        _searchAnnounceTimer.Stop();
        _searchAnnounceTimer.Start();
    }

    private void OnSearchAnnounceTimerTick(object? sender, EventArgs e)
    {
        if (!_dataSource.IsSearchActive) return;
        int count = _dataSource.SearchMatchCount;
        if (count >= 0) Announce(KumikoAnnouncements.SearchMatchCount(_dataSource.Strings, count));
    }
}
