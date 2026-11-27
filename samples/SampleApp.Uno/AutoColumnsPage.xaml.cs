using System;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SampleApp.Shared;

namespace SampleApp.Uno;

/// <summary>
/// Auto columns (#315, ← SampleApp.Maui AutoColumnsPage): the grid declares no columns and generates them from
/// <see cref="AutoOrder"/> and <see cref="AutoProduct"/> (<c>AutoGenerateColumns="Auto"</c>).
/// <c>AutoGeneratingColumn</c> adjusts a few of them.
/// </summary>
public sealed partial class AutoColumnsPage : Page
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

    private void OnToggleItemsClicked(object sender, RoutedEventArgs e)
    {
        _products = !_products;
        kumiko.ItemsSource = _products ? AutoColumnsData.Products() : AutoColumnsData.Orders();
        itemsToggle.Content = _products ? "Show orders" : "Show products";
    }
}
