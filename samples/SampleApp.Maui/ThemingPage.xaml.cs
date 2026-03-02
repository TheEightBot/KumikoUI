using System.Collections.ObjectModel;
using System.ComponentModel;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Maui;

namespace SampleApp.Maui;

public partial class ThemingPage : ContentPage
{
    private readonly ThemingPageViewModel _viewModel;

    public ThemingPage()
    {
        InitializeComponent();
        _viewModel = new ThemingPageViewModel();
        BindingContext = _viewModel;
    }

    // --- Built-in themes ---

    private void OnLightThemeClicked(object sender, EventArgs e)
    {
        kumiko.Theme = DataGridThemeMode.Light;
    }

    private void OnDarkThemeClicked(object sender, EventArgs e)
    {
        kumiko.Theme = DataGridThemeMode.Dark;
    }

    private void OnHighContrastThemeClicked(object sender, EventArgs e)
    {
        kumiko.Theme = DataGridThemeMode.HighContrast;
    }

    // --- Custom themes ---

    private void OnOceanBlueClicked(object sender, EventArgs e)
    {
        var style = new DataGridStyle();
        style.BackgroundColor = new GridColor(235, 245, 255);
        style.HeaderBackgroundColor = new GridColor(20, 80, 160);
        style.HeaderTextColor = new GridColor(255, 255, 255);
        style.HeaderBorderColor = new GridColor(15, 60, 130);
        style.CellTextColor = new GridColor(30, 30, 50);
        style.SelectionColor = new GridColor(100, 160, 230, 80);
        style.SelectionTextColor = new GridColor(255, 255, 255);
        style.AccentColor = new GridColor(0, 120, 215);
        style.GridLineColor = new GridColor(180, 210, 240);
        style.AlternateRowColor = new GridColor(220, 235, 252);
        style.CurrentCellBorderColor = new GridColor(0, 100, 200);
        style.FocusedRowColor = new GridColor(200, 225, 255);
        style.SortIndicatorColor = new GridColor(255, 255, 255);
        style.FilterIconColor = new GridColor(200, 220, 255);
        style.FilterActiveIconColor = new GridColor(255, 220, 100);
        style.FrozenColumnBackgroundColor = new GridColor(225, 238, 255);
        style.FrozenColumnDividerColor = new GridColor(100, 160, 220);
        style.RightFrozenColumnBackgroundColor = new GridColor(225, 238, 255);
        style.RightFrozenColumnDividerColor = new GridColor(100, 160, 220);
        style.FrozenRowBackgroundColor = new GridColor(225, 238, 255);
        style.FrozenRowDividerColor = new GridColor(100, 160, 220);
        style.GroupHeaderBackgroundColor = new GridColor(210, 230, 252);
        style.GroupHeaderTextColor = new GridColor(20, 60, 120);
        style.GroupHeaderCountColor = new GridColor(80, 120, 180);
        style.GroupChevronColor = new GridColor(20, 80, 160);
        style.GroupChevronBackgroundColor = new GridColor(190, 215, 245);
        style.GroupPanelBackgroundColor = new GridColor(215, 235, 255);
        style.GroupPanelTextColor = new GridColor(40, 80, 140);
        style.GroupPanelLabelColor = new GridColor(80, 120, 180);
        style.GroupPanelChipBackgroundColor = new GridColor(200, 225, 252);
        style.GroupPanelChipBorderColor = new GridColor(140, 180, 225);
        style.GroupPanelChipAccentColor = new GridColor(0, 100, 200);
        style.GroupPanelChipTextColor = new GridColor(20, 60, 120);
        style.GroupPanelChipRemoveColor = new GridColor(80, 120, 180);
        style.SummaryRowBackgroundColor = new GridColor(210, 230, 252);
        style.SummaryRowTextColor = new GridColor(20, 50, 100);
        style.SummaryRowLabelColor = new GridColor(80, 120, 180);
        style.SummaryRowBorderColor = new GridColor(140, 180, 225);
        style.GroupSummaryRowBackgroundColor = new GridColor(220, 238, 255);
        style.GroupSummaryRowTextColor = new GridColor(30, 60, 110);
        style.RowDragHandleColor = new GridColor(100, 160, 220);
        style.RowDragHandleBackgroundColor = new GridColor(230, 242, 255);
        style.RowDragHandleHeaderBackgroundColor = new GridColor(20, 80, 160);
        kumiko.GridStyle = style;
    }

    private void OnForestGreenClicked(object sender, EventArgs e)
    {
        var style = new DataGridStyle();
        style.BackgroundColor = new GridColor(240, 248, 235);
        style.HeaderBackgroundColor = new GridColor(34, 100, 34);
        style.HeaderTextColor = new GridColor(255, 255, 255);
        style.HeaderBorderColor = new GridColor(25, 80, 25);
        style.CellTextColor = new GridColor(30, 50, 30);
        style.SelectionColor = new GridColor(80, 160, 80, 80);
        style.SelectionTextColor = new GridColor(255, 255, 255);
        style.AccentColor = new GridColor(46, 139, 87);
        style.GridLineColor = new GridColor(180, 220, 180);
        style.AlternateRowColor = new GridColor(225, 245, 220);
        style.CurrentCellBorderColor = new GridColor(34, 120, 34);
        style.FocusedRowColor = new GridColor(210, 240, 210);
        style.SortIndicatorColor = new GridColor(255, 255, 255);
        style.FilterIconColor = new GridColor(200, 240, 200);
        style.FilterActiveIconColor = new GridColor(255, 220, 100);
        style.FrozenColumnBackgroundColor = new GridColor(230, 245, 225);
        style.FrozenColumnDividerColor = new GridColor(80, 160, 80);
        style.RightFrozenColumnBackgroundColor = new GridColor(230, 245, 225);
        style.RightFrozenColumnDividerColor = new GridColor(80, 160, 80);
        style.FrozenRowBackgroundColor = new GridColor(230, 245, 225);
        style.FrozenRowDividerColor = new GridColor(80, 160, 80);
        style.GroupHeaderBackgroundColor = new GridColor(215, 240, 210);
        style.GroupHeaderTextColor = new GridColor(25, 70, 25);
        style.GroupHeaderCountColor = new GridColor(80, 140, 80);
        style.GroupChevronColor = new GridColor(34, 100, 34);
        style.GroupChevronBackgroundColor = new GridColor(195, 230, 190);
        style.GroupPanelBackgroundColor = new GridColor(225, 245, 220);
        style.GroupPanelTextColor = new GridColor(40, 90, 40);
        style.GroupPanelLabelColor = new GridColor(80, 140, 80);
        style.GroupPanelChipBackgroundColor = new GridColor(210, 240, 205);
        style.GroupPanelChipBorderColor = new GridColor(140, 200, 140);
        style.GroupPanelChipAccentColor = new GridColor(34, 120, 34);
        style.GroupPanelChipTextColor = new GridColor(25, 70, 25);
        style.GroupPanelChipRemoveColor = new GridColor(80, 140, 80);
        style.SummaryRowBackgroundColor = new GridColor(215, 240, 210);
        style.SummaryRowTextColor = new GridColor(25, 60, 25);
        style.SummaryRowLabelColor = new GridColor(80, 140, 80);
        style.SummaryRowBorderColor = new GridColor(140, 200, 140);
        style.GroupSummaryRowBackgroundColor = new GridColor(225, 245, 218);
        style.GroupSummaryRowTextColor = new GridColor(30, 70, 30);
        style.RowDragHandleColor = new GridColor(80, 160, 80);
        style.RowDragHandleBackgroundColor = new GridColor(235, 248, 230);
        style.RowDragHandleHeaderBackgroundColor = new GridColor(34, 100, 34);
        kumiko.GridStyle = style;
    }

    private void OnSunsetClicked(object sender, EventArgs e)
    {
        var style = new DataGridStyle();
        style.BackgroundColor = new GridColor(255, 248, 240);
        style.HeaderBackgroundColor = new GridColor(180, 60, 30);
        style.HeaderTextColor = new GridColor(255, 255, 255);
        style.HeaderBorderColor = new GridColor(150, 45, 20);
        style.CellTextColor = new GridColor(60, 30, 20);
        style.SelectionColor = new GridColor(220, 120, 60, 80);
        style.SelectionTextColor = new GridColor(255, 255, 255);
        style.AccentColor = new GridColor(230, 100, 50);
        style.GridLineColor = new GridColor(240, 200, 170);
        style.AlternateRowColor = new GridColor(255, 238, 220);
        style.CurrentCellBorderColor = new GridColor(200, 80, 40);
        style.FocusedRowColor = new GridColor(255, 228, 200);
        style.SortIndicatorColor = new GridColor(255, 255, 255);
        style.FilterIconColor = new GridColor(255, 210, 180);
        style.FilterActiveIconColor = new GridColor(255, 220, 100);
        style.FrozenColumnBackgroundColor = new GridColor(255, 240, 228);
        style.FrozenColumnDividerColor = new GridColor(200, 120, 80);
        style.RightFrozenColumnBackgroundColor = new GridColor(255, 240, 228);
        style.RightFrozenColumnDividerColor = new GridColor(200, 120, 80);
        style.FrozenRowBackgroundColor = new GridColor(255, 240, 228);
        style.FrozenRowDividerColor = new GridColor(200, 120, 80);
        style.GroupHeaderBackgroundColor = new GridColor(255, 230, 210);
        style.GroupHeaderTextColor = new GridColor(120, 40, 15);
        style.GroupHeaderCountColor = new GridColor(180, 100, 60);
        style.GroupChevronColor = new GridColor(180, 60, 30);
        style.GroupChevronBackgroundColor = new GridColor(255, 215, 185);
        style.GroupPanelBackgroundColor = new GridColor(255, 238, 225);
        style.GroupPanelTextColor = new GridColor(140, 60, 30);
        style.GroupPanelLabelColor = new GridColor(180, 100, 60);
        style.GroupPanelChipBackgroundColor = new GridColor(255, 225, 200);
        style.GroupPanelChipBorderColor = new GridColor(220, 160, 120);
        style.GroupPanelChipAccentColor = new GridColor(200, 80, 40);
        style.GroupPanelChipTextColor = new GridColor(120, 40, 15);
        style.GroupPanelChipRemoveColor = new GridColor(180, 100, 60);
        style.SummaryRowBackgroundColor = new GridColor(255, 230, 210);
        style.SummaryRowTextColor = new GridColor(100, 35, 15);
        style.SummaryRowLabelColor = new GridColor(180, 100, 60);
        style.SummaryRowBorderColor = new GridColor(220, 160, 120);
        style.GroupSummaryRowBackgroundColor = new GridColor(255, 238, 222);
        style.GroupSummaryRowTextColor = new GridColor(110, 45, 20);
        style.RowDragHandleColor = new GridColor(200, 120, 80);
        style.RowDragHandleBackgroundColor = new GridColor(255, 245, 235);
        style.RowDragHandleHeaderBackgroundColor = new GridColor(180, 60, 30);
        kumiko.GridStyle = style;
    }

    // --- Custom color sliders (header background) ---

    private void OnColorSliderChanged(object sender, ValueChangedEventArgs e)
    {
        byte r = (byte)sliderR.Value;
        byte g = (byte)sliderG.Value;
        byte b = (byte)sliderB.Value;

        var style = kumiko.GridStyle;
        style.HeaderBackgroundColor = new GridColor(r, g, b);

        // Automatically pick a contrasting text color
        double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        style.HeaderTextColor = luminance > 0.5
            ? new GridColor(0, 0, 0)
            : new GridColor(255, 255, 255);

        kumiko.GridStyle = style;
    }
}

// --- View Model ---

public class ThemingPageViewModel
{
    public ObservableCollection<ThemingProduct> Products { get; }

    public ThemingPageViewModel()
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

    public string Sku
    {
        get => _sku;
        set { _sku = value; OnPropertyChanged(nameof(Sku)); }
    }

    public string ProductName
    {
        get => _productName;
        set { _productName = value; OnPropertyChanged(nameof(ProductName)); }
    }

    public string Category
    {
        get => _category;
        set { _category = value; OnPropertyChanged(nameof(Category)); }
    }

    public double Price
    {
        get => _price;
        set { _price = value; OnPropertyChanged(nameof(Price)); }
    }

    public int Stock
    {
        get => _stock;
        set { _stock = value; OnPropertyChanged(nameof(Stock)); }
    }

    public bool InStock
    {
        get => _inStock;
        set { _inStock = value; OnPropertyChanged(nameof(InStock)); }
    }

    public double Rating
    {
        get => _rating;
        set { _rating = value; OnPropertyChanged(nameof(Rating)); }
    }

    public string Supplier
    {
        get => _supplier;
        set { _supplier = value; OnPropertyChanged(nameof(Supplier)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
