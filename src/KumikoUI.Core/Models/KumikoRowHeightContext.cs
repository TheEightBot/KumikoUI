namespace KumikoUI.Core.Models;

/// <summary>
/// Measures the content height of one data row for <see cref="KumikoRowHeightContext.MeasureContentHeight"/>;
/// implemented by the renderer's row measurer.
/// </summary>
internal interface IRowContentMeasurer
{
    /// <summary>The <see cref="KumikoRowHeightMode.Auto"/> height of <paramref name="row"/>, clamped to the style's limits.</summary>
    float MeasureContentHeight(int row, object item);
}

/// <summary>
/// The data row a <see cref="KumikoRowHeightResolver"/> is asked about. It is valid only during the resolver call.
/// </summary>
public readonly struct KumikoRowHeightContext
{
    private readonly IRowContentMeasurer? _measurer;

    internal KumikoRowHeightContext(object item, int row, KumikoRowHeightMode mode, float defaultHeight,
        IRowContentMeasurer? measurer)
    {
        Item = item;
        Row = row;
        Mode = mode;
        DefaultHeight = defaultHeight;
        _measurer = measurer;
    }

    /// <summary>The row's data item.</summary>
    public object Item { get; }

    /// <summary>The row's index in the flat view; it changes with sorting, filtering and grouping.</summary>
    public int Row { get; }

    /// <summary>The grid's <see cref="KumikoStyle.RowHeightMode"/>.</summary>
    public KumikoRowHeightMode Mode { get; }

    /// <summary><see cref="KumikoStyle.RowHeight"/>: the height of the row in <see cref="KumikoRowHeightMode.Fixed"/> mode.</summary>
    public float DefaultHeight { get; }

    /// <summary>
    /// The <see cref="KumikoRowHeightMode.Auto"/> height of this row, measured now and clamped to
    /// <see cref="KumikoStyle.MinRowHeight"/> and <see cref="KumikoStyle.MaxRowHeight"/>, whatever the mode. A
    /// <see cref="KumikoRowHeightMode.Fixed"/> grid can use it to size selected rows to their content, and an
    /// <see cref="KumikoRowHeightMode.Auto"/> grid to adjust the measured height. Returns <see cref="DefaultHeight"/>
    /// for a context the grid did not create.
    /// </summary>
    /// <returns>The row's content height in pixels.</returns>
    public float MeasureContentHeight() => _measurer?.MeasureContentHeight(Row, Item) ?? DefaultHeight;
}
