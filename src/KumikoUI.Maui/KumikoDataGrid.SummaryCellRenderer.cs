using KumikoUI.Core.Rendering;

namespace KumikoUI.Maui;

/// <summary>The grid-wide custom summary cell renderer (#334).</summary>
public partial class KumikoDataGrid
{
    /// <summary>Identifies the <see cref="SummaryCellRenderer"/> bindable property.</summary>
    public static readonly BindableProperty SummaryCellRendererProperty = BindableProperty.Create(
        nameof(SummaryCellRenderer), typeof(ISummaryCellRenderer), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) =>
        {
            var view = (KumikoDataGrid)b;
            view._renderer.SummaryCellRenderer = (ISummaryCellRenderer?)n;
            view.InvalidateSurface();
        });

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
