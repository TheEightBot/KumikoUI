namespace SampleApp.Maui;

/// <summary>
/// The empty view (#326): the message the grid draws while no rows show. "Clear data" shows the "no items" text; "Filter
/// everything out" shows the "no rows match" text, and the header's filter icon or "Clear filter" brings the rows back.
/// The checkboxes turn the view off and replace its text.
/// </summary>
public partial class EmptyViewPage : ContentPage
{
    private static readonly string[] Names = { "Ada", "Grace", "Linus", "Margaret", "Alan", "Barbara" };
    private static readonly string[] Cities = { "Oslo", "Rome", "Lima", "Kyiv" };

    public EmptyViewPage()
    {
        InitializeComponent();
        Load();
    }

    private void Load()
    {
        kumiko.ItemsSource = Enumerable.Range(1, 50).Select(i => new Person
        {
            Name = $"{Names[i % Names.Length]} {i}",
            City = Cities[i % Cities.Length],
            Age = 20 + i % 40,
        }).ToList();
        UpdateStatus();
    }

    private void UpdateStatus() =>
        statusLabel.Text = $"{kumiko.DataSource.RowCount} rows shown of {kumiko.DataSource.ItemCount}";

    private void OnLoadClicked(object? sender, EventArgs e) => Load();

    private void OnClearClicked(object? sender, EventArgs e)
    {
        kumiko.ItemsSource = new List<Person>();
        UpdateStatus();
    }

    private void OnFilterOutClicked(object? sender, EventArgs e)
    {
        kumiko.DataSource.SetFilter(_ => false);
        UpdateStatus();
    }

    private void OnClearFilterClicked(object? sender, EventArgs e)
    {
        kumiko.DataSource.ClearAllFilters();
        UpdateStatus();
    }

    private void OnShowChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.ShowEmptyView = e.Value;
    }

    private void OnCustomTextChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.EmptyViewText = e.Value ? "Nothing to see here" : null;
    }

    private sealed class Person
    {
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
        public int Age { get; set; }
    }
}
