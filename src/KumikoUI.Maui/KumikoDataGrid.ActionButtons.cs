using System.Collections.ObjectModel;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Maui;

/// <summary>Hands the grid's <c>BindingContext</c> to <see cref="MauiActionButtonDefinition"/>s declared in XAML.</summary>
public partial class KumikoDataGrid
{
    /// <summary>
    /// Gives every <see cref="MauiActionButtonDefinition"/> in an <see cref="ActionButtonsCellRenderer"/> column this
    /// grid's <c>BindingContext</c> (definitions are not in the visual tree, so they cannot inherit it), and redraws
    /// the grid when a definition changes.
    /// </summary>
    private void SyncActionButtonDefinitions()
    {
        if (GetValue(ColumnsProperty) is not ObservableCollection<KumikoColumn> columns) return; // still constructing
        var context = BindingContext;
        foreach (var column in columns)
        {
            if (column.CustomCellRenderer is not ActionButtonsCellRenderer renderer) continue;

            foreach (var button in renderer.Buttons)
            {
                if (button is not MauiActionButtonDefinition definition) continue;

                if (!ReferenceEquals(definition.BindingContext, context))
                    definition.BindingContext = context;

                // Subtract first so a repeated sync never double-subscribes
                definition.Changed -= OnActionButtonDefinitionChanged;
                definition.Changed += OnActionButtonDefinitionChanged;
            }
        }
    }

    /// <summary>Stops listening to the definitions, so they do not root a grid that left the page.</summary>
    private void UnsubscribeActionButtonDefinitions()
    {
        if (GetValue(ColumnsProperty) is not ObservableCollection<KumikoColumn> columns) return;
        foreach (var column in columns)
        {
            if (column.CustomCellRenderer is not ActionButtonsCellRenderer renderer) continue;

            foreach (var button in renderer.Buttons)
            {
                if (button is MauiActionButtonDefinition definition)
                    definition.Changed -= OnActionButtonDefinitionChanged;
            }
        }
    }

    private void OnActionButtonDefinitionChanged(object? sender, EventArgs e) => Invalidate();

    private void OnGridBindingContextChanged(object? sender, EventArgs e) => SyncActionButtonDefinitions();
}
