using KumikoUI.Core.Rendering;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>Hands the grid's <c>DataContext</c> to <see cref="UnoActionButtonDefinition"/>s declared in XAML.</summary>
public partial class KumikoDataGrid
{
    /// <summary>
    /// Gives every <see cref="UnoActionButtonDefinition"/> in an <see cref="ActionButtonsCellRenderer"/>
    /// column this grid's <c>DataContext</c> (definitions are not in the visual tree, so they cannot inherit
    /// it), and redraws the grid when a definition changes.
    /// </summary>
    private void SyncActionButtonDefinitions()
    {
        var context = DataContext;
        var renderers = Columns
            .Select(column => column.CustomCellRenderer)
            .OfType<ActionButtonsCellRenderer>();
        foreach (var renderer in renderers)
        {
            foreach (var definition in renderer.Buttons.OfType<UnoActionButtonDefinition>())
            {
                if (!ReferenceEquals(definition.DataContext, context))
                    definition.DataContext = context;

                // Subtract first so a repeated sync never double-subscribes
                definition.Changed -= OnActionButtonDefinitionChanged;
                definition.Changed += OnActionButtonDefinitionChanged;
            }
        }
    }

    private void OnActionButtonDefinitionChanged(object? sender, EventArgs e) => Invalidate();

    private void OnGridDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        => SyncActionButtonDefinitions();
}
