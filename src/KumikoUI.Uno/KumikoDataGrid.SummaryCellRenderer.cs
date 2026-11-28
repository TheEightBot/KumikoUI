using KumikoUI.Core.Rendering;

namespace KumikoUI.Uno;

/// <summary>The grid-wide custom summary cell renderer (#334).</summary>
public partial class KumikoDataGrid
{
    /// <summary>Identifies the <see cref="SummaryCellRenderer"/> dependency property.</summary>
    public static readonly DependencyProperty SummaryCellRendererProperty =
        DependencyProperty.Register(
            nameof(SummaryCellRenderer), typeof(ISummaryCellRenderer), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._renderer.SummaryCellRenderer = (ISummaryCellRenderer?)e.NewValue;
                view.Invalidate();
            }));

    /// <summary>
    /// Draws every summary value cell that has no <see cref="KumikoUI.Core.Models.SummaryColumnDescription.CustomCellRenderer"/>.
    /// A cell it draws is never moved to the text after the row's title. Must not allocate per frame. Null (the
    /// default) draws the text.
    /// </summary>
    public ISummaryCellRenderer? SummaryCellRenderer
    {
        get => (ISummaryCellRenderer?)GetValue(SummaryCellRendererProperty);
        set => SetValue(SummaryCellRendererProperty, value);
    }
}
