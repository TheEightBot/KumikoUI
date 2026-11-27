using KumikoUI.Core.Models;
using SampleApp.Shared;

namespace SampleApp.Maui;

/// <summary>
/// Auto columns (#315): the grid declares no columns and generates them from <see cref="AutoOrder"/> and
/// <see cref="AutoProduct"/> (<c>AutoGenerateColumns="Auto"</c>). <c>AutoGeneratingColumn</c> adjusts a few of them.
/// </summary>
public partial class AutoColumnsPage : ContentPage
{
    private bool _products;

    public AutoColumnsPage()
    {
        InitializeComponent();
        kumiko.ItemsSource = AutoColumnsData.Orders();
    }

    // Runs once per generated column, before it is added: tweak, replace or cancel it.
    private void OnAutoGeneratingColumn(object? sender, KumikoAutoGeneratingColumnEventArgs e)
    {
        switch (e.Metadata.PropertyName)
        {
            case nameof(AutoOrder.Id):
                e.Column.Width = 90;
                e.Column.FreezeMode = ColumnFreezeMode.Left;
                break;
            case nameof(AutoOrder.Customer):
            case nameof(AutoProduct.Name):
                e.Column.Width = 160;
                break;
        }
    }

    private void OnToggleItemsClicked(object? sender, EventArgs e)
    {
        _products = !_products;
        kumiko.ItemsSource = _products ? AutoColumnsData.Products() : AutoColumnsData.Orders();
        itemsToggle.Text = _products ? "Show orders" : "Show products";
    }
}
