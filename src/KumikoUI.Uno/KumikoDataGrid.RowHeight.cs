using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// Variable and auto row height (#320): <see cref="RowHeightMode"/>, <see cref="MinRowHeight"/>,
/// <see cref="MaxRowHeight"/>, <see cref="CellVerticalPadding"/>, <see cref="RowHeightResolver"/>,
/// <see cref="InvalidateRowHeights"/> and <see cref="InvalidateRowHeight"/>.
/// </summary>
/// <remarks>
/// These are style settings (<see cref="KumikoStyle"/>). Like <see cref="RowHeight"/>, a value you set is written into
/// the current style and re-applied after <see cref="GridStyle"/> or <see cref="Theme"/> is replaced, so it survives a
/// theme change.
/// </remarks>
public partial class KumikoDataGrid
{
    private readonly KumikoRowHeightOptions _rowHeightOptions = new();

    // ── RowHeightMode ──

    /// <summary>Identifies the <see cref="RowHeightMode"/> dependency property.</summary>
    public static readonly DependencyProperty RowHeightModeProperty =
        DependencyProperty.Register(
            nameof(RowHeightMode), typeof(KumikoRowHeightMode), typeof(KumikoDataGrid),
            new PropertyMetadata(KumikoRowHeightMode.Fixed, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._rowHeightOptions.SetMode((KumikoRowHeightMode)e.NewValue, view._style);
                view.Invalidate();
            }));

    /// <summary>
    /// How data row heights are determined (see <see cref="KumikoStyle.RowHeightMode"/>): every data row
    /// <see cref="RowHeight"/> tall (<see cref="KumikoRowHeightMode.Fixed"/>, the default), or as tall as its wrapped
    /// cells (<see cref="KumikoRowHeightMode.Auto"/>, with columns whose <see cref="KumikoColumn.TextWrap"/> wraps).
    /// </summary>
    public KumikoRowHeightMode RowHeightMode
    {
        get => (KumikoRowHeightMode)GetValue(RowHeightModeProperty);
        set => SetValue(RowHeightModeProperty, value);
    }

    // ── MinRowHeight / MaxRowHeight ──

    /// <summary>Identifies the <see cref="MinRowHeight"/> dependency property.</summary>
    public static readonly DependencyProperty MinRowHeightProperty =
        DependencyProperty.Register(
            nameof(MinRowHeight), typeof(double), typeof(KumikoDataGrid),
            new PropertyMetadata(double.NaN, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._rowHeightOptions.SetMinRowHeight((double)e.NewValue, view._style);
                view.Invalidate();
            }));

    /// <summary>
    /// <see cref="KumikoRowHeightMode.Auto"/> mode: the shortest a data row is laid out (see
    /// <see cref="KumikoStyle.MinRowHeight"/>). <see cref="double.NaN"/> (the default) means <see cref="RowHeight"/>.
    /// </summary>
    public double MinRowHeight
    {
        get => (double)GetValue(MinRowHeightProperty);
        set => SetValue(MinRowHeightProperty, value);
    }

    /// <summary>Identifies the <see cref="MaxRowHeight"/> dependency property.</summary>
    public static readonly DependencyProperty MaxRowHeightProperty =
        DependencyProperty.Register(
            nameof(MaxRowHeight), typeof(double), typeof(KumikoDataGrid),
            new PropertyMetadata(double.NaN, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._rowHeightOptions.SetMaxRowHeight((double)e.NewValue, view._style);
                view.Invalidate();
            }));

    /// <summary>
    /// <see cref="KumikoRowHeightMode.Auto"/> mode: the tallest a data row is laid out; wrapped text that needs more ends
    /// with an ellipsis (see <see cref="KumikoStyle.MaxRowHeight"/>). <see cref="double.NaN"/> (the default) means no
    /// limit.
    /// </summary>
    public double MaxRowHeight
    {
        get => (double)GetValue(MaxRowHeightProperty);
        set => SetValue(MaxRowHeightProperty, value);
    }

    // ── CellVerticalPadding ──

    /// <summary>Identifies the <see cref="CellVerticalPadding"/> dependency property.</summary>
    public static readonly DependencyProperty CellVerticalPaddingProperty =
        DependencyProperty.Register(
            nameof(CellVerticalPadding), typeof(double), typeof(KumikoDataGrid),
            new PropertyMetadata(6d, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._rowHeightOptions.SetCellVerticalPadding((double)e.NewValue, view._style);
                view.Invalidate();
            }));

    /// <summary>
    /// <see cref="KumikoRowHeightMode.Auto"/> mode: the space above and below the tallest cell content of a data row
    /// (see <see cref="KumikoStyle.CellVerticalPadding"/>). Default: 6.
    /// </summary>
    public double CellVerticalPadding
    {
        get => (double)GetValue(CellVerticalPaddingProperty);
        set => SetValue(CellVerticalPaddingProperty, value);
    }

    // ── RowHeightResolver ──

    /// <summary>Identifies the <see cref="RowHeightResolver"/> dependency property.</summary>
    public static readonly DependencyProperty RowHeightResolverProperty =
        DependencyProperty.Register(
            nameof(RowHeightResolver), typeof(KumikoRowHeightResolver), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._rowHeightOptions.SetResolver(e.NewValue as KumikoRowHeightResolver, view._style);
                view.Invalidate();
            }));

    /// <summary>
    /// Sets or adjusts the height of data rows in either <see cref="RowHeightMode"/> (see
    /// <see cref="KumikoStyle.RowHeightResolver"/>): return a height, or <c>null</c> for the mode's height. Bindable, so
    /// a view model can supply it.
    /// </summary>
    public KumikoRowHeightResolver? RowHeightResolver
    {
        get => (KumikoRowHeightResolver?)GetValue(RowHeightResolverProperty);
        set => SetValue(RowHeightResolverProperty, value);
    }

    // ── Invalidation ──

    /// <summary>
    /// Re-measures every row's height on the next frame, for heights that depend on something the grid cannot observe
    /// (see <see cref="KumikoSource.InvalidateRowHeights"/>), and redraws.
    /// </summary>
    public void InvalidateRowHeights()
    {
        _dataSource.InvalidateRowHeights();
        Invalidate();
    }

    /// <summary>
    /// Re-measures the height of the row showing <paramref name="item"/> on the next frame (see
    /// <see cref="KumikoSource.InvalidateRowHeight(object)"/>), and redraws.
    /// </summary>
    /// <param name="item">The item whose row height may have changed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    public void InvalidateRowHeight(object item)
    {
        _dataSource.InvalidateRowHeight(item);
        Invalidate();
    }
}
