using System.Globalization;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Finds the grid-wide search match of each drawn cell (#317). <see cref="BeginFrame"/> captures the search text,
/// the source's text rules and the current cell once per frame; <see cref="Apply"/> then sets a cell's
/// <see cref="CellRenderContext.SearchMatchStart"/>, <see cref="CellRenderContext.SearchMatchLength"/> and
/// <see cref="CellRenderContext.IsCurrentSearchMatch"/> with one allocation-free culture-aware IndexOf.
/// </summary>
internal sealed class SearchHighlighter
{
    private string _text = string.Empty;
    private CompareInfo _compareInfo = CultureInfo.InvariantCulture.CompareInfo;
    private CompareOptions _options;
    private CellPosition _current = CellPosition.Invalid;

    /// <summary>Whether the frame has a search to highlight.</summary>
    public bool IsActive => _text.Length > 0;

    /// <summary>Capture the search for one frame.</summary>
    public void BeginFrame(KumikoSource source, KumikoSelectionModel selection)
    {
        _text = source.SearchText;
        if (_text.Length == 0) return;
        var comparer = source.Comparer;
        _compareInfo = comparer.Culture.CompareInfo;
        _options = comparer.Options;
        _current = selection.CurrentCell;
    }

    /// <summary>
    /// The cell with its search match set: the first match of the search text in its display text, under the
    /// source's rules, when its column is searched. <paramref name="columnIndex"/> is the cell's column in
    /// <see cref="KumikoSource.Columns"/>.
    /// </summary>
    public CellRenderContext Apply(in CellRenderContext cell, int columnIndex)
    {
        string text = cell.DisplayText;
        if (text.Length == 0 || !cell.Column.IsSearchable) return cell;
        int index = _compareInfo.IndexOf(text.AsSpan(), _text.AsSpan(), _options, out int length);
        if (index < 0 || length <= 0) return cell;
        bool current = _current.Row == cell.Row && _current.Column == columnIndex;
        return cell with { SearchMatchStart = index, SearchMatchLength = length, IsCurrentSearchMatch = current };
    }
}
