using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace SampleApp.Uno;

/// <summary>
/// Runtime theming demo (← SampleApp.Maui ThemingPage). Switches built-in themes via
/// <see cref="KumikoTheme.Create"/> (the Uno control has no <c>Theme</c> DP, so the produced
/// <see cref="KumikoStyle"/> is assigned directly), applies hand-built custom palettes, and live-
/// adjusts the header colour from RGB sliders.
/// </summary>
public sealed partial class ThemingPage : Page
{
    private readonly ThemingViewModel _viewModel = new();

    public ThemingPage()
    {
        this.InitializeComponent();
        DataContext = _viewModel;
    }

    // --- Built-in themes ---

    private void OnLightThemeClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => kumiko.GridStyle = KumikoTheme.Create(KumikoThemeMode.Light);

    private void OnDarkThemeClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => kumiko.GridStyle = KumikoTheme.Create(KumikoThemeMode.Dark);

    private void OnHighContrastThemeClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => kumiko.GridStyle = KumikoTheme.Create(KumikoThemeMode.HighContrast);

    // --- Custom themes ---

    private void OnOceanBlueClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var style = new KumikoStyle();
        style.BackgroundColor = new KumikoColor(235, 245, 255);
        style.HeaderBackgroundColor = new KumikoColor(20, 80, 160);
        style.HeaderTextColor = new KumikoColor(255, 255, 255);
        style.HeaderBorderColor = new KumikoColor(15, 60, 130);
        style.CellTextColor = new KumikoColor(30, 30, 50);
        style.SelectionColor = new KumikoColor(100, 160, 230, 80);
        style.SelectionTextColor = new KumikoColor(255, 255, 255);
        style.AccentColor = new KumikoColor(0, 120, 215);
        style.GridLineColor = new KumikoColor(180, 210, 240);
        style.AlternateRowColor = new KumikoColor(220, 235, 252);
        style.CurrentCellBorderColor = new KumikoColor(0, 100, 200);
        style.FocusedRowColor = new KumikoColor(200, 225, 255);
        style.SortIndicatorColor = new KumikoColor(255, 255, 255);
        style.FilterIconColor = new KumikoColor(200, 220, 255);
        style.FilterActiveIconColor = new KumikoColor(255, 220, 100);
        style.FrozenColumnBackgroundColor = new KumikoColor(225, 238, 255);
        style.FrozenColumnDividerColor = new KumikoColor(100, 160, 220);
        style.RightFrozenColumnBackgroundColor = new KumikoColor(225, 238, 255);
        style.RightFrozenColumnDividerColor = new KumikoColor(100, 160, 220);
        style.FrozenRowBackgroundColor = new KumikoColor(225, 238, 255);
        style.FrozenRowDividerColor = new KumikoColor(100, 160, 220);
        style.GroupHeaderBackgroundColor = new KumikoColor(210, 230, 252);
        style.GroupHeaderTextColor = new KumikoColor(20, 60, 120);
        style.GroupHeaderCountColor = new KumikoColor(80, 120, 180);
        style.GroupChevronColor = new KumikoColor(20, 80, 160);
        style.GroupChevronBackgroundColor = new KumikoColor(190, 215, 245);
        style.GroupPanelBackgroundColor = new KumikoColor(215, 235, 255);
        style.GroupPanelTextColor = new KumikoColor(40, 80, 140);
        style.GroupPanelLabelColor = new KumikoColor(80, 120, 180);
        style.GroupPanelChipBackgroundColor = new KumikoColor(200, 225, 252);
        style.GroupPanelChipBorderColor = new KumikoColor(140, 180, 225);
        style.GroupPanelChipAccentColor = new KumikoColor(0, 100, 200);
        style.GroupPanelChipTextColor = new KumikoColor(20, 60, 120);
        style.GroupPanelChipRemoveColor = new KumikoColor(80, 120, 180);
        style.SummaryRowBackgroundColor = new KumikoColor(210, 230, 252);
        style.SummaryRowTextColor = new KumikoColor(20, 50, 100);
        style.SummaryRowLabelColor = new KumikoColor(80, 120, 180);
        style.SummaryRowBorderColor = new KumikoColor(140, 180, 225);
        style.GroupSummaryRowBackgroundColor = new KumikoColor(220, 238, 255);
        style.GroupSummaryRowTextColor = new KumikoColor(30, 60, 110);
        style.RowDragHandleColor = new KumikoColor(100, 160, 220);
        style.RowDragHandleBackgroundColor = new KumikoColor(230, 242, 255);
        style.RowDragHandleHeaderBackgroundColor = new KumikoColor(20, 80, 160);
        kumiko.GridStyle = style;
    }

    private void OnForestGreenClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var style = new KumikoStyle();
        style.BackgroundColor = new KumikoColor(240, 248, 235);
        style.HeaderBackgroundColor = new KumikoColor(34, 100, 34);
        style.HeaderTextColor = new KumikoColor(255, 255, 255);
        style.HeaderBorderColor = new KumikoColor(25, 80, 25);
        style.CellTextColor = new KumikoColor(30, 50, 30);
        style.SelectionColor = new KumikoColor(80, 160, 80, 80);
        style.SelectionTextColor = new KumikoColor(255, 255, 255);
        style.AccentColor = new KumikoColor(46, 139, 87);
        style.GridLineColor = new KumikoColor(180, 220, 180);
        style.AlternateRowColor = new KumikoColor(225, 245, 220);
        style.CurrentCellBorderColor = new KumikoColor(34, 120, 34);
        style.FocusedRowColor = new KumikoColor(210, 240, 210);
        style.SortIndicatorColor = new KumikoColor(255, 255, 255);
        style.FilterIconColor = new KumikoColor(200, 240, 200);
        style.FilterActiveIconColor = new KumikoColor(255, 220, 100);
        style.FrozenColumnBackgroundColor = new KumikoColor(230, 245, 225);
        style.FrozenColumnDividerColor = new KumikoColor(80, 160, 80);
        style.RightFrozenColumnBackgroundColor = new KumikoColor(230, 245, 225);
        style.RightFrozenColumnDividerColor = new KumikoColor(80, 160, 80);
        style.FrozenRowBackgroundColor = new KumikoColor(230, 245, 225);
        style.FrozenRowDividerColor = new KumikoColor(80, 160, 80);
        style.GroupHeaderBackgroundColor = new KumikoColor(215, 240, 210);
        style.GroupHeaderTextColor = new KumikoColor(25, 70, 25);
        style.GroupHeaderCountColor = new KumikoColor(80, 140, 80);
        style.GroupChevronColor = new KumikoColor(34, 100, 34);
        style.GroupChevronBackgroundColor = new KumikoColor(195, 230, 190);
        style.GroupPanelBackgroundColor = new KumikoColor(225, 245, 220);
        style.GroupPanelTextColor = new KumikoColor(40, 90, 40);
        style.GroupPanelLabelColor = new KumikoColor(80, 140, 80);
        style.GroupPanelChipBackgroundColor = new KumikoColor(210, 240, 205);
        style.GroupPanelChipBorderColor = new KumikoColor(140, 200, 140);
        style.GroupPanelChipAccentColor = new KumikoColor(34, 120, 34);
        style.GroupPanelChipTextColor = new KumikoColor(25, 70, 25);
        style.GroupPanelChipRemoveColor = new KumikoColor(80, 140, 80);
        style.SummaryRowBackgroundColor = new KumikoColor(215, 240, 210);
        style.SummaryRowTextColor = new KumikoColor(25, 60, 25);
        style.SummaryRowLabelColor = new KumikoColor(80, 140, 80);
        style.SummaryRowBorderColor = new KumikoColor(140, 200, 140);
        style.GroupSummaryRowBackgroundColor = new KumikoColor(225, 245, 218);
        style.GroupSummaryRowTextColor = new KumikoColor(30, 70, 30);
        style.RowDragHandleColor = new KumikoColor(80, 160, 80);
        style.RowDragHandleBackgroundColor = new KumikoColor(235, 248, 230);
        style.RowDragHandleHeaderBackgroundColor = new KumikoColor(34, 100, 34);
        kumiko.GridStyle = style;
    }

    private void OnSunsetClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var style = new KumikoStyle();
        style.BackgroundColor = new KumikoColor(255, 248, 240);
        style.HeaderBackgroundColor = new KumikoColor(180, 60, 30);
        style.HeaderTextColor = new KumikoColor(255, 255, 255);
        style.HeaderBorderColor = new KumikoColor(150, 45, 20);
        style.CellTextColor = new KumikoColor(60, 30, 20);
        style.SelectionColor = new KumikoColor(220, 120, 60, 80);
        style.SelectionTextColor = new KumikoColor(255, 255, 255);
        style.AccentColor = new KumikoColor(230, 100, 50);
        style.GridLineColor = new KumikoColor(240, 200, 170);
        style.AlternateRowColor = new KumikoColor(255, 238, 220);
        style.CurrentCellBorderColor = new KumikoColor(200, 80, 40);
        style.FocusedRowColor = new KumikoColor(255, 228, 200);
        style.SortIndicatorColor = new KumikoColor(255, 255, 255);
        style.FilterIconColor = new KumikoColor(255, 210, 180);
        style.FilterActiveIconColor = new KumikoColor(255, 220, 100);
        style.FrozenColumnBackgroundColor = new KumikoColor(255, 240, 228);
        style.FrozenColumnDividerColor = new KumikoColor(200, 120, 80);
        style.RightFrozenColumnBackgroundColor = new KumikoColor(255, 240, 228);
        style.RightFrozenColumnDividerColor = new KumikoColor(200, 120, 80);
        style.FrozenRowBackgroundColor = new KumikoColor(255, 240, 228);
        style.FrozenRowDividerColor = new KumikoColor(200, 120, 80);
        style.GroupHeaderBackgroundColor = new KumikoColor(255, 230, 210);
        style.GroupHeaderTextColor = new KumikoColor(120, 40, 15);
        style.GroupHeaderCountColor = new KumikoColor(180, 100, 60);
        style.GroupChevronColor = new KumikoColor(180, 60, 30);
        style.GroupChevronBackgroundColor = new KumikoColor(255, 215, 185);
        style.GroupPanelBackgroundColor = new KumikoColor(255, 238, 225);
        style.GroupPanelTextColor = new KumikoColor(140, 60, 30);
        style.GroupPanelLabelColor = new KumikoColor(180, 100, 60);
        style.GroupPanelChipBackgroundColor = new KumikoColor(255, 225, 200);
        style.GroupPanelChipBorderColor = new KumikoColor(220, 160, 120);
        style.GroupPanelChipAccentColor = new KumikoColor(200, 80, 40);
        style.GroupPanelChipTextColor = new KumikoColor(120, 40, 15);
        style.GroupPanelChipRemoveColor = new KumikoColor(180, 100, 60);
        style.SummaryRowBackgroundColor = new KumikoColor(255, 230, 210);
        style.SummaryRowTextColor = new KumikoColor(100, 35, 15);
        style.SummaryRowLabelColor = new KumikoColor(180, 100, 60);
        style.SummaryRowBorderColor = new KumikoColor(220, 160, 120);
        style.GroupSummaryRowBackgroundColor = new KumikoColor(255, 238, 222);
        style.GroupSummaryRowTextColor = new KumikoColor(110, 45, 20);
        style.RowDragHandleColor = new KumikoColor(200, 120, 80);
        style.RowDragHandleBackgroundColor = new KumikoColor(255, 245, 235);
        style.RowDragHandleHeaderBackgroundColor = new KumikoColor(180, 60, 30);
        kumiko.GridStyle = style;
    }

    // --- Custom color sliders (header background) ---

    private void OnColorSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // Guard: ValueChanged can fire during XAML load before all sliders exist.
        if (sliderR is null || sliderG is null || sliderB is null || kumiko is null)
            return;

        byte r = (byte)sliderR.Value;
        byte g = (byte)sliderG.Value;
        byte b = (byte)sliderB.Value;

        var style = kumiko.GridStyle;
        style.HeaderBackgroundColor = new KumikoColor(r, g, b);

        // Automatically pick a contrasting text color.
        double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        style.HeaderTextColor = luminance > 0.5
            ? new KumikoColor(0, 0, 0)
            : new KumikoColor(255, 255, 255);

        kumiko.GridStyle = style;
    }
}

// --- View Model ---

public class ThemingViewModel
{
    public ObservableCollection<ThemingProduct> Products { get; }

    public ThemingViewModel()
    {
        Products = new ObservableCollection<ThemingProduct>();

        var categories = new[] { "Electronics", "Clothing", "Home", "Sports", "Books", "Food", "Auto", "Garden" };
        var suppliers = new[] { "Acme Corp", "Global Supply", "Best Goods", "QuickShip", "Premier Parts", "ValueMart" };
        var adjectives = new[] { "Premium", "Standard", "Deluxe", "Basic", "Professional", "Ultra", "Classic", "Compact" };
        var nouns = new[] { "Widget", "Gadget", "Kit", "Set", "Pack", "Bundle", "Module", "Unit" };

        var random = new Random(42);
        for (int i = 1; i <= 50; i++)
        {
            Products.Add(new ThemingProduct
            {
                Sku = $"SKU-{i:D4}",
                ProductName = $"{adjectives[random.Next(adjectives.Length)]} {nouns[random.Next(nouns.Length)]}",
                Category = categories[random.Next(categories.Length)],
                Price = Math.Round(random.NextDouble() * 500 + 5, 2),
                Stock = random.Next(0, 1000),
                InStock = random.Next(100) > 15,
                Rating = Math.Round(random.NextDouble() * 4 + 1, 1),
                Supplier = suppliers[random.Next(suppliers.Length)]
            });
        }
    }
}

// --- Model ---

public class ThemingProduct : INotifyPropertyChanged
{
    private string _sku = string.Empty;
    private string _productName = string.Empty;
    private string _category = string.Empty;
    private double _price;
    private int _stock;
    private bool _inStock;
    private double _rating;
    private string _supplier = string.Empty;

    public string Sku { get => _sku; set { _sku = value; OnPropertyChanged(nameof(Sku)); } }
    public string ProductName { get => _productName; set { _productName = value; OnPropertyChanged(nameof(ProductName)); } }
    public string Category { get => _category; set { _category = value; OnPropertyChanged(nameof(Category)); } }
    public double Price { get => _price; set { _price = value; OnPropertyChanged(nameof(Price)); } }
    public int Stock { get => _stock; set { _stock = value; OnPropertyChanged(nameof(Stock)); } }
    public bool InStock { get => _inStock; set { _inStock = value; OnPropertyChanged(nameof(InStock)); } }
    public double Rating { get => _rating; set { _rating = value; OnPropertyChanged(nameof(Rating)); } }
    public string Supplier { get => _supplier; set { _supplier = value; OnPropertyChanged(nameof(Supplier)); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
