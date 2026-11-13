using System.Text.Json;

namespace KumikoUI.Core.Models;

/// <summary>
/// A grid's user-adjustable layout: column order, width, visibility and freeze mode, the sort, the column filters
/// and the grouping. Take one with <see cref="Capture"/> (or the hosts' <c>GetLayoutState()</c>), store it with
/// <see cref="ToJson"/>, and put it back with <see cref="FromJson"/> and <see cref="ApplyTo"/> (or the hosts'
/// <c>ApplyLayoutState()</c>).
/// </summary>
/// <remarks>
/// <para>
/// Columns are matched by name: <see cref="KumikoColumn.Key"/>, else <see cref="KumikoColumn.PropertyName"/>. A column
/// with neither is not saved. Applying a layout tolerates a different set of columns: a saved column the grid no
/// longer has is ignored, and a column the layout does not mention keeps its position and settings (it is only
/// removed from the sort, which the layout replaces as a whole).
/// </para>
/// <para>
/// Not saved: a predicate set with <see cref="KumikoSource.SetFilter"/>, delegates such as
/// <see cref="GroupDescription.KeySelector"/>, and which groups are expanded. The JSON is produced by a
/// System.Text.Json source generator, so it works under Native AOT and full trimming.
/// </para>
/// </remarks>
public sealed class KumikoLayoutState
{
    /// <summary>The format version <see cref="Capture"/> writes. Newer properties are ignored by older readers.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The format version the layout was saved with. Default: <see cref="CurrentVersion"/>.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// The columns, in <see cref="KumikoSource.Columns"/> order (the on-screen order follows from it and each
    /// column's <see cref="KumikoColumnLayoutState.FreezeMode"/>). Null leaves the columns, sort and filters as they
    /// are when the layout is applied.
    /// </summary>
    public List<KumikoColumnLayoutState>? Columns { get; set; } = new();

    /// <summary>
    /// The grouping, outermost level first. Empty removes the grouping when the layout is applied; null leaves it
    /// as it is.
    /// </summary>
    public List<KumikoGroupLayoutState>? Groups { get; set; } = new();

    /// <summary>Take the current layout of <paramref name="source"/>.</summary>
    /// <param name="source">The data source whose columns and grouping to read.</param>
    /// <returns>A new layout, independent of the source.</returns>
    public static KumikoLayoutState Capture(KumikoSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var state = new KumikoLayoutState();
        var columns = source.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            string key = column.LayoutKey;
            if (string.IsNullOrEmpty(key)) continue;

            bool sorted = column.SortDirection != KumikoSortDirection.None;
            state.Columns!.Add(new KumikoColumnLayoutState
            {
                Key = key,
                IsVisible = column.IsVisible,
                FreezeMode = column.FreezeMode,
                SizeMode = column.SizeMode,
                // Star and Auto widths are layout output, and a never-set width is the default: neither is the user's.
                Width = column.SizeMode == ColumnSizeMode.Fixed && column.HasExplicitWidth ? column.Width : null,
                StarWeight = column.SizeMode == ColumnSizeMode.Star ? column.StarWeight : null,
                SortDirection = sorted ? column.SortDirection : KumikoSortDirection.None,
                SortOrder = sorted ? column.SortOrder : 0,
                Filter = KumikoFilterLayoutState.From(column.ActiveFilter),
            });
        }

        foreach (var group in source.GroupDescriptions)
        {
            state.Groups!.Add(new KumikoGroupLayoutState
            {
                PropertyName = group.PropertyName,
                Header = group.Header,
                SortDirection = group.GroupSortDirection,
            });
        }
        return state;
    }

    /// <summary>
    /// Apply this layout to <paramref name="source"/> as one change (one rebuild, one
    /// <see cref="KumikoSource.ViewChanged"/>): the matched columns take the saved order among themselves (the
    /// others keep their places), visibility, freeze mode, sizing and filter; the sort and the grouping are replaced.
    /// </summary>
    /// <remarks>
    /// The sort is set with <see cref="KumikoSource.SetSort"/> and the filters with
    /// <see cref="KumikoSource.SetColumnFilter"/>, so their events are raised and a filter on a column that does not
    /// allow filtering is skipped. A saved group whose property is already grouped by keeps that
    /// <see cref="GroupDescription"/> (and its selectors) with the saved sort direction; others are created from
    /// the saved property name and header. On a MAUI or Uno grid call the grid's <c>ApplyLayoutState</c> instead, which
    /// also updates its <c>GroupDescriptions</c> collection.
    /// </remarks>
    /// <param name="source">The data source to change.</param>
    public void ApplyTo(KumikoSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.BeginUpdate();
        try
        {
            if (Columns != null)
                ApplyColumns(source, Columns);
            if (Groups != null)
                ApplyGroups(source, Groups);
        }
        finally
        {
            source.EndUpdate();
        }
    }

    private static void ApplyColumns(KumikoSource source, List<KumikoColumnLayoutState> saved)
    {
        // The current columns by name; with duplicate names only the first can be matched.
        var byKey = new Dictionary<string, KumikoColumn>(StringComparer.Ordinal);
        foreach (var column in source.Columns)
        {
            string key = column.LayoutKey;
            if (!string.IsNullOrEmpty(key)) byKey.TryAdd(key, column);
        }

        // The saved columns the source still has, in saved order (the first entry for a name wins).
        var matched = new List<(KumikoColumnLayoutState State, KumikoColumn Column)>();
        var matchedColumns = new HashSet<KumikoColumn>();
        matched.AddRange(saved
            .Where(s => s is not null && !string.IsNullOrEmpty(s.Key))
            .Select(s => (State: s, Column: byKey.GetValueOrDefault(s.Key!)))
            .Where(m => m.Column is not null && matchedColumns.Add(m.Column))
            .Select(m => (m.State, m.Column!)));

        // Order: the positions the matched columns hold now get the matched columns in saved order, so columns the
        // layout does not know stay where they are.
        var desired = source.Columns.ToList();
        int next = 0;
        for (int i = 0; i < desired.Count; i++)
        {
            if (matchedColumns.Contains(desired[i]))
                desired[i] = matched[next++].Column;
        }
        for (int i = 0; i < desired.Count; i++)
        {
            int from = IndexOf(source.Columns, desired[i]);
            if (from != i) source.ReorderColumn(from, i);
        }

        foreach (var (state, column) in matched)
        {
            column.IsVisible = state.IsVisible;
            column.FreezeMode = state.FreezeMode;
            if (state.SizeMode == ColumnSizeMode.Fixed && state.Width is { } width && width > 0 && float.IsFinite(width))
            {
                column.ApplyUserWidth(width); // switches a Star or Auto column to Fixed
            }
            else
            {
                column.SizeMode = state.SizeMode;
            }
            if (state.StarWeight is { } weight && weight > 0 && float.IsFinite(weight))
                column.StarWeight = weight;
        }

        // The sort is replaced as a whole, in saved priority order.
        source.ClearSort();
        var sorted = matched
            .Where(m => m.State.SortDirection != KumikoSortDirection.None)
            .OrderBy(m => m.State.SortOrder <= 0 ? int.MaxValue : m.State.SortOrder)
            .ToList();
        int order = 1;
        foreach (var (state, column) in sorted)
            source.SetSort(column, state.SortDirection, order++);

        foreach (var (state, column) in matched)
        {
            var filter = state.Filter?.ToFilterDescription(column);
            if (filter != null || column.ActiveFilter != null)
                source.SetColumnFilter(column, filter);
        }
    }

    private static void ApplyGroups(KumikoSource source, List<KumikoGroupLayoutState> saved)
    {
        var groups = new List<GroupDescription>(saved.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in saved.Where(s => s is not null && !string.IsNullOrEmpty(s.PropertyName) && seen.Add(s.PropertyName)))
        {

            GroupDescription? group = null;
            foreach (var existing in source.GroupDescriptions)
            {
                if (existing.PropertyName == state.PropertyName) { group = existing; break; }
            }
            group ??= new GroupDescription(state.PropertyName) { Header = state.Header };
            group.GroupSortDirection = state.SortDirection;
            groups.Add(group);
        }
        source.SetGroupDescriptions(groups);
    }

    private static int IndexOf(IReadOnlyList<KumikoColumn> columns, KumikoColumn column)
    {
        for (int i = 0; i < columns.Count; i++)
            if (ReferenceEquals(columns[i], column)) return i;
        return -1;
    }

    /// <summary>This layout as compact JSON (camelCase names, enums as strings, null values left out).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, KumikoLayoutJsonContext.Default.KumikoLayoutState);

    /// <summary>
    /// Read a layout written by <see cref="ToJson"/>. Unknown properties are ignored and missing ones take their
    /// defaults, so layouts saved by other versions still load.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">The text is not valid JSON for a layout, or is the JSON literal null.</exception>
    public static KumikoLayoutState FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize(json, KumikoLayoutJsonContext.Default.KumikoLayoutState)
               ?? throw new JsonException("The JSON does not contain a layout.");
    }
}
