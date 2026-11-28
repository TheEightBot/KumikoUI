using KumikoUI.Core.Rendering;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>The grid-wide header cell renderer (#333).</summary>
public partial class KumikoDataGrid
{
    /// <summary>Identifies the <see cref="HeaderCellRenderer"/> dependency property.</summary>
    public static readonly DependencyProperty HeaderCellRendererProperty =
        DependencyProperty.Register(
            nameof(HeaderCellRenderer), typeof(IHeaderCellRenderer), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._renderer.HeaderCellRenderer = (IHeaderCellRenderer?)e.NewValue;
                view.Invalidate();
            }));

    /// <summary>
    /// Draws the content of every column header that has no <see cref="Core.Models.KumikoColumn.CustomHeaderRenderer"/>
    /// of its own, instead of the header text. The grid keeps the background, the hover feedback, the sort and filter
    /// glyphs and the resize grips. It also measures the header for auto-fit, and must not allocate per frame. Null
    /// (the default) draws the text with <see cref="TextHeaderCellRenderer"/>.
    /// </summary>
    public IHeaderCellRenderer? HeaderCellRenderer
    {
        get => (IHeaderCellRenderer?)GetValue(HeaderCellRendererProperty);
        set => SetValue(HeaderCellRendererProperty, value);
    }
}
