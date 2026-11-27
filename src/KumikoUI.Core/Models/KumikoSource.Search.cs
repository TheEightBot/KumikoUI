namespace KumikoUI.Core.Models;

// Grid-wide search (#317): a text matched against the display text of every searchable column. In highlight mode
// (the default) the view is unchanged and the renderer marks the matches; with SearchFiltersRows the view also hides
// the rows with no match, through a search stage of the view build (ViewBuilder.Passes), so it runs on the background
// worker like the column filters. The number of matching cells is counted on demand over the published view (on a
// worker for a large source) and kept until the view, the data or the search changes.
public partial class KumikoSource
{
    private string _searchText = string.Empty;
    private bool _searchFiltersRows;

    // The match count over the published view, the state it was counted for, and a count running on a worker.
    private SearchResults? _searchResults;
    private bool _searchCountRunning;
    private bool _searchCountWanted;
    private int _searchCountGeneration;

    /// <summary>
    /// Raised on the source's thread when <see cref="SearchText"/> or <see cref="SearchFiltersRows"/> changes, and
    /// when a match count that ran in the background (see <see cref="SearchMatchCount"/>) is ready.
    /// </summary>
    public event EventHandler? SearchChanged;

    /// <summary>
    /// The text the grid-wide search looks for; empty (the default) for no search. A cell matches when its display
    /// text (the value formatted as the cell shows it, see <see cref="FormatCellValue"/>) contains the text under
    /// <see cref="Culture"/> and <see cref="StringCompareOptions"/>, the rules sorting and filtering use. Only
    /// searchable columns are read (see <see cref="KumikoColumn.AllowSearching"/>). The grid highlights the matches;
    /// with <see cref="SearchFiltersRows"/> it also hides the rows that have none. Null is taken as empty.
    /// Changing it raises <see cref="SearchChanged"/>; in filter mode it also rebuilds the view (in the background
    /// for a large source, see <see cref="BackgroundRebuildThreshold"/>), so a search box should set it once the
    /// user pauses typing, as the grid's own search panel does. The search is not part of
    /// <see cref="KumikoLayoutState"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Set from a thread other than the source's.</exception>
    public string SearchText
    {
        get => _searchText;
        set
        {
            VerifyAccess();
            value ??= string.Empty;
            if (string.Equals(value, _searchText, StringComparison.Ordinal)) return;
            bool rows = _searchFiltersRows && (value.Length > 0 || _searchText.Length > 0);
            _searchText = value;
            ApplySearchChange(rows);
        }
    }

    /// <summary>
    /// Whether the search also hides the rows in which no cell matches <see cref="SearchText"/>. False (the
    /// default) only highlights the matches and keeps every row. Combines with the column filters and
    /// <see cref="SetFilter"/>: a row must pass all of them.
    /// </summary>
    /// <exception cref="InvalidOperationException">Set from a thread other than the source's.</exception>
    public bool SearchFiltersRows
    {
        get => _searchFiltersRows;
        set
        {
            VerifyAccess();
            if (value == _searchFiltersRows) return;
            _searchFiltersRows = value;
            ApplySearchChange(rebuild: _searchText.Length > 0);
        }
    }

    /// <summary>Whether a search is active: <see cref="SearchText"/> is not empty.</summary>
    public bool IsSearchActive => _searchText.Length > 0;

    /// <summary>
    /// The number of cells that match <see cref="SearchText"/> in the current view, including the rows of
    /// collapsed groups; 0 when no search is active. Counted when it is first read after the search, the view or
    /// the data changed. For a source of <see cref="BackgroundRebuildThreshold"/> rows or more (with background
    /// rebuilds enabled) the count runs on a worker: the property is -1 until it is ready, and
    /// <see cref="SearchChanged"/> is raised when it is.
    /// </summary>
    public int SearchMatchCount
    {
        get
        {
            if (_searchText.Length == 0) return 0;
            return EnsureSearchResults() is { } results ? results.Total : -1;
        }
    }

    /// <summary>Search state changed: drop the count, rebuild (filter mode) or redraw, and tell the listeners.</summary>
    private void ApplySearchChange(bool rebuild)
    {
        _searchResults = null;
        if (rebuild) RequestRebuild();
        else NotifyRedraw();
        SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Hooks for the view build ─────────────────────────────────

    /// <summary>The search as a view build stage: null unless it filters rows.</summary>
    private SearchSpec? CaptureSearchFilter(KumikoComparer comparer) =>
        _searchFiltersRows && _searchText.Length > 0 ? CaptureSearchSpec(comparer) : null;

    /// <summary>The searchable columns in display order, with readers for one pass over the items.</summary>
    internal SearchSpec CaptureSearchSpec(KumikoComparer comparer)
    {
        var columns = DisplayColumns.Where(column => column.IsSearchable).ToList();
        return new SearchSpec(_searchText, comparer, columns);
    }

    /// <summary>Whether a change of an item property hides or shows rows through the search (filter mode).</summary>
    private bool SearchFilterReads(string propName)
    {
        if (!_searchFiltersRows || _searchText.Length == 0) return false;
        foreach (var col in _columns.Where(c => c.IsSearchable))
        {
            // A value accessor or the item itself (empty path) may depend on any property.
            if (col.ValueAccessor != null || string.IsNullOrEmpty(col.PropertyName) ||
                PropertyPathAffected(col.PropertyName, propName))
                return true;
        }
        return false;
    }

    /// <summary>Whether a change of a column setting changes which rows the search (filter mode) keeps.</summary>
    private bool SearchFilterAffectedBy(string? columnProperty) =>
        _searchFiltersRows && _searchText.Length > 0 &&
        columnProperty is nameof(KumikoColumn.AllowSearching) or nameof(KumikoColumn.IsVisible)
            or nameof(KumikoColumn.Format) or nameof(KumikoColumn.PropertyName) or nameof(KumikoColumn.ValueAccessor)
            or nameof(KumikoColumn.ColumnType) or nameof(KumikoColumn.CustomCellRenderer);

    // ── Match count ──────────────────────────────────────────────

    /// <summary>
    /// The match count of the current search over the published view: kept while it is current, otherwise counted
    /// now (synchronously), or started on a worker for a large source and null until it is ready.
    /// </summary>
    internal SearchResults? EnsureSearchResults()
    {
        if (_searchText.Length == 0) return null;
        EnsureView();
        var results = _searchResults;
        int version = ContentVersion;
        if (results != null && results.Version == version && ReferenceEquals(results.Text, _searchText))
            return results;

        var spec = CaptureSearchSpec(_comparer);
        var view = _view;
        int count = SearchRowCount(view);
        if (!BackgroundRebuildsEnabled || count < _backgroundRebuildThreshold)
        {
            var rows = new object?[count];
            for (int p = 0; p < count; p++) rows[p] = view.Items[SearchSourceIndex(view, p)];
            _searchResults = SearchResults.Count(spec, rows, _searchText, version, CancellationToken.None);
            return _searchResults;
        }

        _searchCountWanted = true;
        if (!_searchCountRunning) StartSearchCount(spec, view, count, version);
        return null;
    }

    private void StartSearchCount(SearchSpec spec, ViewSnapshot view, int count, int version)
    {
        _searchCountWanted = false;
        _searchCountRunning = true;
        int generation = ++_searchCountGeneration;
        // The items in search order, read on this thread: the view's copy of the items can change under a worker.
        var rows = new object?[count];
        for (int p = 0; p < count; p++) rows[p] = view.Items[SearchSourceIndex(view, p)];
        // A comparer with a fixed culture: the worker must not read another thread's current culture.
        spec = spec.WithComparer(new KumikoComparer(_comparer.Culture, _comparer.Options));
        string text = _searchText;
        var context = _ownerContext!;
        Task.Run(() =>
        {
            SearchResults counted;
            try { counted = SearchResults.Count(spec, rows, text, version, CancellationToken.None); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A reader or a formatter threw on the worker: report no matches rather than count again forever.
                counted = SearchResults.None(text, version, rows.Length);
            }
            context.Post(static state =>
            {
                var (source, gen, result) = ((KumikoSource, int, SearchResults))state!;
                source.CompleteSearchCount(gen, result);
            }, (this, generation, counted));
        });
    }

    /// <summary>A background count finished (on the source's thread): keep it if it is still current.</summary>
    private void CompleteSearchCount(int generation, SearchResults results)
    {
        if (_disposed || generation != _searchCountGeneration) return;
        _searchCountRunning = false;
        if (results.Version == ContentVersion && ReferenceEquals(results.Text, _searchText))
        {
            _searchResults = results;
            SearchChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        // The search, the view or the data changed while it ran: count again if anyone still asks.
        if (_searchCountWanted && _searchText.Length > 0) _ = EnsureSearchResults();
    }

    // ── Navigation helpers (KumikoInputController.FindNext / FindPrevious) ──

    /// <summary>The number of data rows the search walks: every filtered row, collapsed groups included.</summary>
    private static int SearchRowCount(ViewSnapshot view) => view.GroupOrder?.Length ?? view.ViewIndices.Count;

    /// <summary>The source index of the data row at <paramref name="position"/> in search order.</summary>
    private static int SearchSourceIndex(ViewSnapshot view, int position) =>
        view.GroupOrder is { } order ? order[position] : view.ViewIndices[position];

    /// <summary>
    /// The position in search order of the first data row at or after <paramref name="visibleRow"/>: the number
    /// of data rows before it, counting the rows of collapsed groups. For a data row it is the row's own position.
    /// </summary>
    private int SearchPositionOfRow(int visibleRow)
    {
        var view = _view;
        if (view.FlatView is not { } flat) return visibleRow;
        int position = 0;
        int end = Math.Min(visibleRow, flat.Count);
        for (int r = 0; r < end; r++)
        {
            var entry = flat[r];
            if (entry.Kind == FlatRowKind.Data) position++;
            else if (entry.Kind == FlatRowKind.GroupHeader && entry.GroupInfo is { IsExpanded: false } collapsed)
                position += collapsed.ItemCount;
        }
        return position;
    }

    /// <summary>
    /// The next (or previous) cell after <paramref name="from"/> whose display text matches the search, in reading
    /// order over the searchable columns in display order and the data rows (collapsed groups included), wrapping
    /// at the end. A <paramref name="from"/> that is not a valid cell starts before the first (after the last)
    /// cell. The answer may be <paramref name="from"/> itself when it is the only match. False when nothing matches.
    /// </summary>
    internal bool FindSearchMatch(CellPosition from, bool backwards, out object item, out KumikoColumn column)
    {
        item = null!;
        column = null!;
        if (_searchText.Length == 0) return false;
        EnsureView();
        var view = _view;
        var spec = CaptureSearchSpec(_comparer);
        int m = spec.ColumnCount;
        int n = SearchRowCount(view);
        if (m == 0 || n == 0) return false;

        // Where to start: a position in search order and, for the start row, the columns before (after) the cell.
        int start;
        int first = 0, last = m - 1; // the columns to look at in the start row on the first pass
        bool startsInRow = false;
        int rowCount = RowCount;
        if (!from.IsValid || from.Row >= rowCount)
        {
            start = backwards ? n - 1 : 0;
        }
        else if (!IsNonDataRow(from.Row))
        {
            start = SearchPositionOfRow(from.Row);
            startsInRow = true;
            int display = from.Column >= 0 && from.Column < _columns.Count ? GetDisplayIndex(_columns[from.Column]) : -1;
            int before = 0; // searchable columns left of the cell
            bool onSearchable = false;
            for (int k = 0; k < m; k++)
            {
                int d = GetDisplayIndex(spec.GetColumn(k));
                if (d < display) before++;
                else if (d == display) onSearchable = true;
            }
            if (backwards) { first = 0; last = before - 1; }
            else { first = onSearchable ? before + 1 : before; last = m - 1; }
        }
        else
        {
            start = SearchPositionOfRow(from.Row) - (backwards ? 1 : 0);
            if (start < 0) start = n - 1;
            if (start >= n) start = 0;
        }

        for (int step = 0; step <= n; step++)
        {
            int position = backwards ? ((start - step) % n + n) % n : (start + step) % n;
            int lo = 0, hi = m - 1;
            if (step == 0) { lo = first; hi = last; }
            else if (step == n)
            {
                if (!startsInRow) break;
                // Back at the start row: the columns the first pass skipped, the cell itself included.
                if (backwards) { lo = last + 1; hi = m - 1; }
                else { lo = 0; hi = first - 1; }
            }
            if (lo > hi) continue;

            var candidate = view.Items[SearchSourceIndex(view, position)];
            if (candidate is null) continue;
            for (int i = 0; i <= hi - lo; i++)
            {
                int k = backwards ? hi - i : lo + i;
                if (!spec.CellMatches(candidate, k)) continue;
                item = candidate;
                column = spec.GetColumn(k);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The one-based number of the match at <paramref name="cell"/> among all matches in search order, 0 when the
    /// cell is not a match, or -1 when the count is not ready yet (see <see cref="SearchMatchCount"/>).
    /// </summary>
    internal int GetSearchMatchOrdinal(CellPosition cell)
    {
        if (_searchText.Length == 0 || !cell.IsValid || cell.Row >= RowCount || IsNonDataRow(cell.Row)) return 0;
        if (cell.Column < 0 || cell.Column >= _columns.Count) return 0;
        if (EnsureSearchResults() is not { } results) return -1;

        var column = _columns[cell.Column];
        if (!column.IsSearchable) return 0;
        var item = GetItem(cell.Row);
        var spec = CaptureSearchSpec(_comparer);
        int target = -1;
        for (int k = 0; k < spec.ColumnCount; k++)
            if (ReferenceEquals(spec.GetColumn(k), column)) { target = k; break; }
        if (target < 0 || !spec.CellMatches(item, target)) return 0;

        int position = SearchPositionOfRow(cell.Row);
        if (position >= results.RowCount) return 0;
        int ordinal = results.MatchesBefore(position) + 1;
        for (int k = 0; k < target; k++)
            if (spec.CellMatches(item, k)) ordinal++;
        return ordinal;
    }
}

/// <summary>
/// The grid-wide search captured for one pass over the items: the text, the comparer and a reader for each searchable
/// column (in display order). Readers are not thread-safe, so each pass or build captures its own.
/// </summary>
internal sealed class SearchSpec
{
    private readonly KumikoColumn[] _columns;
    private readonly ItemReader[] _readers;
    private readonly string?[] _formats;

    public SearchSpec(string text, KumikoComparer comparer, IReadOnlyList<KumikoColumn> columns)
    {
        Text = text;
        Comparer = comparer;
        _columns = new KumikoColumn[columns.Count];
        _readers = new ItemReader[columns.Count];
        _formats = new string?[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            _columns[i] = columns[i];
            _readers[i] = new ItemReader(columns[i]);
            _formats[i] = columns[i].DisplayFormat;
        }
    }

    private SearchSpec(SearchSpec other, KumikoComparer comparer)
    {
        Text = other.Text;
        Comparer = comparer;
        _columns = other._columns;
        _formats = other._formats;
        _readers = new ItemReader[other._readers.Length];
        for (int i = 0; i < _readers.Length; i++) _readers[i] = new ItemReader(other._columns[i]);
    }

    /// <summary>The text searched for (not empty).</summary>
    public string Text { get; }

    /// <summary>The text rules.</summary>
    public KumikoComparer Comparer { get; }

    /// <summary>The number of searchable columns.</summary>
    public int ColumnCount => _readers.Length;

    /// <summary>The searchable column <paramref name="index"/> (display order).</summary>
    public KumikoColumn GetColumn(int index) => _columns[index];

    /// <summary>A copy with its own readers and another comparer, for a pass on a worker.</summary>
    public SearchSpec WithComparer(KumikoComparer comparer) => new(this, comparer);

    /// <summary>Whether any searchable cell of <paramref name="item"/> matches.</summary>
    public bool Matches(object item)
    {
        for (int i = 0; i < _readers.Length; i++)
            if (CellMatches(item, i)) return true;
        return false;
    }

    /// <summary>The number of searchable cells of <paramref name="item"/> that match.</summary>
    public int CountMatches(object item)
    {
        int count = 0;
        for (int i = 0; i < _readers.Length; i++)
            if (CellMatches(item, i)) count++;
        return count;
    }

    /// <summary>Whether the display text of column <paramref name="index"/> of <paramref name="item"/> contains the text.</summary>
    public bool CellMatches(object item, int index) =>
        Comparer.TextContains(Comparer.Format(_readers[index].Read(item), _formats[index]), Text);
}

/// <summary>
/// The matches of one search over one view: the total, and the number of matches before each data row in search
/// order (for "match 3 of 12").
/// </summary>
internal sealed class SearchResults
{
    private readonly int[] _prefix;

    private SearchResults(string text, int version, int[] prefix)
    {
        Text = text;
        Version = version;
        _prefix = prefix;
    }

    /// <summary>The search text counted (the instance, so a changed text never matches by accident).</summary>
    public string Text { get; }

    /// <summary>The source's <see cref="KumikoSource.ContentVersion"/> the count belongs to.</summary>
    public int Version { get; }

    /// <summary>The number of matching cells.</summary>
    public int Total => _prefix[^1];

    /// <summary>The number of data rows counted.</summary>
    public int RowCount => _prefix.Length - 1;

    /// <summary>The number of matches in the rows before <paramref name="position"/>.</summary>
    public int MatchesBefore(int position) => _prefix[position];

    /// <summary>A count that found nothing, for a pass that could not read the rows.</summary>
    public static SearchResults None(string text, int version, int rowCount) => new(text, version, new int[rowCount + 1]);

    /// <summary>Count the matches of <paramref name="spec"/> in <paramref name="rows"/> (in search order).</summary>
    public static SearchResults Count(SearchSpec spec, object?[] rows, string text, int version, CancellationToken ct)
    {
        var prefix = new int[rows.Length + 1];
        int total = 0;
        for (int p = 0; p < rows.Length; p++)
        {
            if ((p & 0x3FF) == 0) ct.ThrowIfCancellationRequested();
            prefix[p] = total;
            if (rows[p] is { } item) total += spec.CountMatches(item);
        }
        prefix[rows.Length] = total;
        return new SearchResults(text, version, prefix);
    }
}
