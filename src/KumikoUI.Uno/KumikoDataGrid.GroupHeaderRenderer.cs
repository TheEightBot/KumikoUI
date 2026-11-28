using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Uno;

/// <summary>
/// The custom group header renderer (#332): draws the content of group headers, right of the expand/collapse chevron,
/// instead of the built-in "Header: Key (count)" text.
/// </summary>
public partial class KumikoDataGrid
{
    /// <summary>Identifies the <see cref="GroupHeaderRenderer"/> dependency property.</summary>
    public static readonly DependencyProperty GroupHeaderRendererProperty =
        DependencyProperty.Register(
            nameof(GroupHeaderRenderer), typeof(IGroupHeaderRenderer), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) =>
            {
                var view = (KumikoDataGrid)d;
                view._renderer.GroupHeaderRenderer = (IGroupHeaderRenderer?)e.NewValue;
                view.Invalidate();
            }));

    /// <summary>
    /// Draws the content of every group header that has no <see cref="GroupDescription.CustomHeaderRenderer"/> for its
    /// level: the part right of the chevron, which the grid keeps. Null (the default) draws the title, the item count and
    /// the caption summary as text with <see cref="TextGroupHeaderRenderer"/>. It must not allocate per frame.
    /// </summary>
    public IGroupHeaderRenderer? GroupHeaderRenderer
    {
        get => (IGroupHeaderRenderer?)GetValue(GroupHeaderRendererProperty);
        set => SetValue(GroupHeaderRendererProperty, value);
    }
}
