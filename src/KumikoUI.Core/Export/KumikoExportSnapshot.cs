using System.Diagnostics;
using System.Globalization;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Export;

/// <summary>
/// An immutable copy of what an export writes, taken from a <see cref="KumikoSource"/> on its thread: the rows in
/// output order (item references, the group structure including collapsed groups, the computed summaries), the
/// columns and the culture. A format writer consumes <see cref="Rows"/> (or <see cref="ForEachRowAsync"/>), so
/// every format agrees on scope, order, grouping and values. Row membership and order are frozen; the values are read
/// from the items as the rows are enumerated, so an item that changes during the export shows its value at that moment.
/// </summary>
/// <remarks>
/// Creating a snapshot reads the source and must happen on the source's thread; enumerating it does not touch the
/// source, so it can run anywhere. One enumeration at a time: the readers it uses are not thread-safe.
/// </remarks>
public sealed class KumikoExportSnapshot
{
    private const int CancelCheckMask = 255;     // check cancellation every 256 rows
    private const int CallerThreadChunk = 2000;  // rows read per chunk on the caller's thread

    private readonly ExportEntry[] _entries;
    private readonly KumikoExportColumn[] _columns;
    private readonly KumikoComparer _comparer;
    private readonly bool _raw;
    private readonly int _firstDataColumn;

    private KumikoExportSnapshot(KumikoExportOptions options, ExportEntry[] entries, KumikoExportColumn[] columns,
        KumikoComparer comparer, bool raw, KumikoExportValueMode valueMode)
    {
        Options = options;
        _entries = entries;
        _columns = columns;
        _comparer = comparer;
        _raw = raw;
        ValueMode = valueMode;
        int frozen = 0;
        while (frozen < columns.Length && columns[frozen].IsFrozen) frozen++;
        FrozenColumnCount = frozen;
        for (_firstDataColumn = 0; _firstDataColumn < columns.Length && columns[_firstDataColumn].IsGroupKey; _firstDataColumn++) { }
    }

    /// <summary>The options the snapshot was taken with.</summary>
    public KumikoExportOptions Options { get; }

    /// <summary>The columns, in output order; group key columns (<see cref="KumikoExportGroupLayout.Columns"/>) come first.</summary>
    public IReadOnlyList<KumikoExportColumn> Columns => _columns;

    /// <summary>The culture of display text: the options' culture, else the source's, else the current culture when the snapshot was taken.</summary>
    public CultureInfo Culture => _comparer.Culture;

    /// <summary>How values are written: <see cref="KumikoExportValueMode.DisplayText"/> or <see cref="KumikoExportValueMode.RawValue"/> (never <see cref="KumikoExportValueMode.Auto"/>).</summary>
    public KumikoExportValueMode ValueMode { get; }

    /// <summary>The number of rows the export writes, not counting the header row. Known up front.</summary>
    public int TotalRows => _entries.Length;

    /// <summary>Whether the stream starts with a header row.</summary>
    public bool IncludeHeaders => Options.IncludeHeaders;

    /// <summary>The number of leading columns that are frozen at the left edge in the grid.</summary>
    public int FrozenColumnCount { get; }

    /// <summary>Whether <see cref="ForEachRowAsync"/> reads values on the calling thread.</summary>
    public bool ReadOnCallerThread => Options.ReadOnCallerThread;

    /// <summary>
    /// Takes a snapshot of what <paramref name="options"/> select from <paramref name="source"/>. Call it on the
    /// source's thread (the UI thread of a grid).
    /// </summary>
    /// <param name="source">The data source.</param>
    /// <param name="options">What to export; <c>null</c> exports what the grid shows.</param>
    /// <param name="autoValueMode">The mode <see cref="KumikoExportValueMode.Auto"/> stands for in the calling format.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ArgumentException">The options are invalid, for example <see cref="KumikoExportRows.Selected"/> without a selection, or no column to write.</exception>
    public static KumikoExportSnapshot Create(KumikoSource source, KumikoExportOptions? options = null,
        KumikoExportValueMode autoValueMode = KumikoExportValueMode.DisplayText)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new KumikoExportOptions();
        if (!Enum.IsDefined(options.Rows)) throw new ArgumentException("Unknown row scope.", nameof(options));
        if (!Enum.IsDefined(options.GroupLayout)) throw new ArgumentException("Unknown group layout.", nameof(options));
        if (options.Rows == KumikoExportRows.Selected && options.Selection == null)
            throw new ArgumentException("Rows is Selected but no Selection was given.", nameof(options));

        var gridColumns = KumikoExportColumns.Resolve(source, options);
        if (gridColumns.Count == 0)
            throw new ArgumentException("The options select no column to export.", nameof(options));

        var valueMode = options.ValueMode == KumikoExportValueMode.Auto ? autoValueMode : options.ValueMode;
        if (valueMode == KumikoExportValueMode.Auto) valueMode = KumikoExportValueMode.DisplayText;
        var culture = options.Culture ?? source.Culture ?? CultureInfo.CurrentCulture;
        var comparer = new KumikoComparer(culture, source.StringCompareOptions);

        var layout = options.GroupLayout;
        bool all = options.Rows == KumikoExportRows.All;
        bool rowLayout = layout == KumikoExportGroupLayout.Rows && !all;
        bool summaries = options.IncludeSummaries ?? layout == KumikoExportGroupLayout.Rows;
        var selected = options.Rows == KumikoExportRows.Selected ? options.Selection!.Resolve(source) : null;

        var view = source.CaptureExportView(options.Rows, layout, out var allItems);
        var entries = BuildEntries(view, allItems, rowLayout, summaries && !all && selected == null, layout, selected);

        var columns = new List<KumikoExportColumn>(gridColumns.Count + 2);
        if (layout == KumikoExportGroupLayout.Columns && !all)
        {
            for (int level = 0; level < source.GroupDescriptions.Count; level++)
            {
                var desc = source.GroupDescriptions[level];
                var column = source.Columns.FirstOrDefault(c =>
                    string.Equals(c.PropertyName, desc.PropertyName, StringComparison.OrdinalIgnoreCase));
                columns.Add(new KumikoExportColumn(null, desc.Header ?? column?.Header ?? desc.PropertyName, level));
            }
        }
        foreach (var c in gridColumns)
            columns.Add(new KumikoExportColumn(c, options.HeaderSelector?.Invoke(c) ?? c.Header ?? string.Empty, -1));

        return new KumikoExportSnapshot(options, entries, columns.ToArray(), comparer,
            valueMode == KumikoExportValueMode.RawValue, valueMode);
    }

    private static ExportEntry[] BuildEntries(ViewSnapshot? view, System.Collections.IList? allItems, bool rowLayout,
        bool summaries, KumikoExportGroupLayout layout, HashSet<object>? selected)
    {
        var entries = new List<ExportEntry>(view?.ViewIndices.Count ?? allItems?.Count ?? 0);

        if (allItems != null)
        {
            for (int i = 0; i < allItems.Count; i++)
                if (allItems[i] is { } item) entries.Add(new ExportEntry(KumikoExportRowKind.Data, 0, item, null, null));
            return entries.ToArray();
        }

        var v = view!;
        if (summaries)
            foreach (var row in v.TopSummaries) entries.Add(new ExportEntry(KumikoExportRowKind.TableSummary, 0, null, null, row));

        if (v.FlatView != null)
        {
            var flatRows = v.FlatView!;
            var groups = new Dictionary<GroupHeaderInfo, ExportGroup>(ReferenceEqualityComparer.Instance);
            ExportGroup Map(GroupHeaderInfo info)
            {
                if (groups.TryGetValue(info, out var known)) return known;
                var made = new ExportGroup(info, info.Parent == null ? null : Map(info.Parent));
                groups[info] = made;
                return made;
            }

            ExportGroup? current = null;
            var pending = new List<ExportEntry>(); // group headers of a selection, written once a selected row follows
            for (int r = 0; r < flatRows.Count; r++)
            {
                var row = flatRows[r];
                switch (row.Kind)
                {
                    case FlatRowKind.GroupHeader:
                        var opened = Map(row.GroupInfo!);
                        current = opened;
                        if (!rowLayout) break;
                        var header = new ExportEntry(KumikoExportRowKind.GroupHeader, opened.Level, null, opened, null);
                        if (selected == null)
                        {
                            entries.Add(header);
                        }
                        else
                        {
                            pending.RemoveAll(p => p.Group!.Level >= opened.Level);
                            pending.Add(header);
                        }
                        break;
                    case FlatRowKind.Data:
                        if (v.Items[row.SourceIndex] is not { } item) break;
                        if (selected != null)
                        {
                            if (!selected.Contains(item)) break;
                            entries.AddRange(pending);
                            pending.Clear();
                        }
                        entries.Add(new ExportEntry(KumikoExportRowKind.Data, rowLayout && current != null ? current.Level + 1 : 0,
                            item, current, null));
                        break;
                    case FlatRowKind.GroupSummary:
                        if (!rowLayout || !summaries) break;
                        var info = row.GroupInfo!;
                        if (info.Summaries is { } rowsOfGroup && row.GroupSummaryIndex < rowsOfGroup.Count)
                        {
                            var g = Map(info);
                            entries.Add(new ExportEntry(KumikoExportRowKind.GroupSummary, g.Level + 1, null, g, rowsOfGroup[row.GroupSummaryIndex]));
                        }
                        break;
                }
            }
        }
        else
        {
            var items = v.ViewIndices.Select(index => v.Items[index]).OfType<object>();
            foreach (var item in selected == null ? items : items.Where(selected.Contains))
            {
                entries.Add(new ExportEntry(KumikoExportRowKind.Data, 0, item, null, null));
            }
        }

        if (summaries)
            foreach (var row in v.BottomSummaries) entries.Add(new ExportEntry(KumikoExportRowKind.TableSummary, 0, null, null, row));
        return entries.ToArray();
    }

    /// <summary>
    /// The display text of <paramref name="value"/> for <paramref name="column"/>: its format applied in
    /// <see cref="Culture"/>. A typed writer uses it as the text of a cell whose format it cannot express.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="column">The column whose format applies.</param>
    /// <returns>The text; empty for <c>null</c>.</returns>
    public string FormatDisplayText(object? value, KumikoExportColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return _comparer.Format(value, column.Format);
    }

    /// <summary>
    /// The rows of the export, one at a time: the header row (when <see cref="IncludeHeaders"/>), then the table
    /// summaries at the top, the data, group and summary rows, and the table summaries at the bottom. The same
    /// <see cref="KumikoExportRow"/> instance is returned for every row and overwritten by the next, so nothing
    /// proportional to the row count is allocated. Each enumeration starts over; run one at a time.
    /// </summary>
    public IEnumerable<KumikoExportRow> Rows => Enumerate();

    private IEnumerable<KumikoExportRow> Enumerate()
    {
        var columns = _columns;
        var readers = new ItemReader?[columns.Length];
        for (int c = 0; c < columns.Length; c++)
            if (!columns[c].IsGroupKey) readers[c] = new ItemReader(columns[c].Accessor, columns[c].PropertyName);
        var row = new KumikoExportRow(columns.Length);

        if (IncludeHeaders)
        {
            row.Begin(KumikoExportRowKind.Header, 0, null, columns.Length);
            for (int c = 0; c < columns.Length; c++) row.Set(c, columns[c].Header, columns[c].Header);
            yield return row;
        }

        foreach (var entry in _entries)
        {
            row.Begin(entry.Kind, entry.Level, entry.Kind == KumikoExportRowKind.Data ? entry.Item : null, columns.Length);
            switch (entry.Kind)
            {
                case KumikoExportRowKind.Data:
                    FillData(row, entry, readers);
                    break;
                case KumikoExportRowKind.GroupHeader:
                    row.Set(0, entry.Group!.Title, entry.Group.Title);
                    break;
                default:
                    FillSummary(row, entry.Summary!);
                    break;
            }
            yield return row;
        }
    }

    private void FillData(KumikoExportRow row, in ExportEntry entry, ItemReader?[] readers)
    {
        var columns = _columns;
        var item = entry.Item!;
        for (int c = 0; c < columns.Length; c++)
        {
            if (readers[c] is not { } reader)
            {
                string key = entry.Group?.KeyTextAt(columns[c].GroupLevel) ?? string.Empty;
                row.Set(c, key, key);
                continue;
            }
            object? value = reader.Read(item);
            row.Set(c, value, _raw ? KumikoExportText.Invariant(value) : _comparer.Format(value, columns[c].Format));
        }
    }

    private void FillSummary(KumikoExportRow row, ComputedSummaryRow summary)
    {
        var columns = _columns;
        for (int c = _firstDataColumn; c < columns.Length; c++)
        {
            string property = columns[c].PropertyName;
            if (property.Length == 0 || !summary.Values.TryGetValue(property, out var text)) continue;
            summary.RawValues.TryGetValue(property, out var raw);
            // The text is what the grid shows (the label and the value; several aggregates are joined, as the renderer does).
            row.Set(c, raw, _raw ? KumikoExportText.Invariant(raw) : text, FirstSummaryType(summary, property));
        }

        // The title goes in the first column when no value does.
        if (!string.IsNullOrEmpty(summary.Title) && row.Cells[0].Text.Length == 0)
            row.Set(0, summary.Title, summary.Title);
    }

    /// <summary>The summary type of the first aggregate of <paramref name="property"/>, the one in <c>RawValues</c>.</summary>
    private static SummaryType? FirstSummaryType(ComputedSummaryRow summary, string property)
    {
        var aggregates = summary.Aggregates;
        for (int i = 0; i < aggregates.Count; i++)
        {
            if (string.Equals(aggregates[i].PropertyName, property, StringComparison.OrdinalIgnoreCase))
                return aggregates[i].SummaryType;
        }

        return null;
    }

    /// <summary>
    /// Calls <paramref name="onRow"/> for every row of <see cref="Rows"/>, with the export's async contract: the
    /// values are read on a thread-pool thread, or (<see cref="ReadOnCallerThread"/>) on the calling thread in chunks of
    /// about 2,000 rows with <paramref name="flushAsync"/> and a yield between chunks. The token is checked every 256
    /// rows; <paramref name="progress"/> gets a report at most every 1% of the rows and every 100 ms, and one at the end.
    /// </summary>
    /// <param name="onRow">Writes one row. The row is reused: read it before returning. Runs on the reading thread.</param>
    /// <param name="flushAsync">Called between chunks when reading on the calling thread, so a writer can send what it buffered without blocking.</param>
    /// <param name="progress">Receives the progress.</param>
    /// <param name="cancellationToken">Cancels the walk with an <see cref="OperationCanceledException"/>.</param>
    /// <returns>A task that completes after the last row.</returns>
    public Task ForEachRowAsync(Action<KumikoExportRow> onRow, Func<CancellationToken, ValueTask>? flushAsync = null,
        IProgress<KumikoExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onRow);
        cancellationToken.ThrowIfCancellationRequested();
        return ReadOnCallerThread
            ? WalkAsync(onRow, flushAsync, progress, chunked: true, cancellationToken)
            : Task.Run(() => WalkAsync(onRow, flushAsync, progress, chunked: false, cancellationToken), CancellationToken.None);
    }

    private async Task WalkAsync(Action<KumikoExportRow> onRow, Func<CancellationToken, ValueTask>? flushAsync,
        IProgress<KumikoExportProgress>? progress, bool chunked, CancellationToken ct)
    {
        int total = TotalRows;
        int step = Math.Max(1, total / 100);
        int written = 0, lastReported = 0, seen = 0, inChunk = 0;
        var clock = Stopwatch.StartNew();
        long lastReportMs = 0;

        foreach (var row in Rows)
        {
            if ((seen++ & CancelCheckMask) == 0) ct.ThrowIfCancellationRequested();
            onRow(row);
            if (row.Kind == KumikoExportRowKind.Header) continue;

            written++;
            if (progress != null && written - lastReported >= step && clock.ElapsedMilliseconds - lastReportMs >= 100)
            {
                progress.Report(new KumikoExportProgress(written, total));
                lastReported = written;
                lastReportMs = clock.ElapsedMilliseconds;
            }

            if (chunked && ++inChunk >= CallerThreadChunk)
            {
                inChunk = 0;
                if (flushAsync != null) await flushAsync(ct).ConfigureAwait(true);
                await Task.Yield();
                ct.ThrowIfCancellationRequested();
            }
        }

        progress?.Report(new KumikoExportProgress(written, total));
    }
}
