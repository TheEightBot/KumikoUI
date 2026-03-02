using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SampleApp.Maui;

public partial class LargeDataPage : ContentPage
{
    private static readonly string[] Categories = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta" };
    private static readonly string[] Regions = { "North", "South", "East", "West", "Central", "Northeast", "Southwest", "Pacific" };
    private static readonly string[] FirstNames = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Hank", "Iris", "Jack", "Kate", "Leo" };
    private static readonly string[] LastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Wilson", "Moore", "Taylor", "Anderson" };

    private readonly LargeDataPageViewModel _viewModel = new();
    private readonly Random _random = new(99);

    public LargeDataPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    private async void LoadData(int count)
    {
        statusLabel.Text = $"Generating {count:N0} rows...";

        // Disable buttons during load to prevent double-taps
        SetButtonsEnabled(false);

        var totalSw = Stopwatch.StartNew();

        // Phase 1: Generate data objects (CPU-bound, off UI thread)
        var genSw = Stopwatch.StartNew();
        var items = await Task.Run(() => GenerateItems(count));
        genSw.Stop();
        var genMs = genSw.ElapsedMilliseconds;

        statusLabel.Text = $"Generated {count:N0} in {genMs:N0}ms — assigning to grid...";

        // Phase 2: Wrap in ObservableCollection and assign to ViewModel.
        // This triggers a SINGLE ItemsSource property change → single SetItems → single RebuildView.
        // IMPORTANT: Do NOT add items one-by-one to an existing ObservableCollection —
        //   each Add fires CollectionChanged, which triggers a full RebuildView (O(n)) when
        //   summaries/sort/filter/grouping are active. That makes it O(n²) total.
        var assignSw = Stopwatch.StartNew();
        _viewModel.Items = new ObservableCollection<LargeDataItem>(items);
        assignSw.Stop();
        var assignMs = assignSw.ElapsedMilliseconds;

        totalSw.Stop();

        statusLabel.Text = $"{count:N0} rows | Generate: {genMs:N0}ms | Assign+Rebuild: {assignMs:N0}ms | Total: {totalSw.ElapsedMilliseconds:N0}ms";
        Debug.WriteLine($"[LargeDataPage] LoadData({count:N0}): generate={genMs}ms, assign={assignMs}ms, total={totalSw.ElapsedMilliseconds}ms");

        SetButtonsEnabled(true);
    }

    private List<LargeDataItem> GenerateItems(int count)
    {
        var rng = new Random(99); // deterministic seed for reproducibility
        var items = new List<LargeDataItem>(count);

        for (int i = 1; i <= count; i++)
        {
            items.Add(new LargeDataItem
            {
                RowNumber = i,
                FullName = $"{FirstNames[rng.Next(FirstNames.Length)]} {LastNames[rng.Next(LastNames.Length)]}",
                Category = Categories[rng.Next(Categories.Length)],
                Value = Math.Round(rng.NextDouble() * 1000, 2),
                Amount = rng.Next(100, 500000),
                CreatedDate = DateTime.Today.AddDays(-rng.Next(1, 3650)),
                IsEnabled = rng.NextDouble() > 0.2,
                Region = Regions[rng.Next(Regions.Length)],
                Score = Math.Round(rng.NextDouble() * 100, 1),
                Notes = $"Record {i}"
            });
        }

        return items;
    }

    private void SetButtonsEnabled(bool enabled)
    {
        btn10K.IsEnabled = enabled;
        btn50K.IsEnabled = enabled;
        btn100K.IsEnabled = enabled;
        btnClear.IsEnabled = enabled;
        btnUpdateRandom.IsEnabled = enabled;
    }

    private void OnLoad10KClicked(object? sender, EventArgs e) => LoadData(10_000);
    private void OnLoad50KClicked(object? sender, EventArgs e) => LoadData(50_000);
    private void OnLoad100KClicked(object? sender, EventArgs e) => LoadData(100_000);

    private void OnClearClicked(object? sender, EventArgs e)
    {
        var sw = Stopwatch.StartNew();
        _viewModel.Items = new ObservableCollection<LargeDataItem>();
        sw.Stop();
        statusLabel.Text = $"Cleared in {sw.ElapsedMilliseconds:N0}ms";
        Debug.WriteLine($"[LargeDataPage] Clear: {sw.ElapsedMilliseconds}ms");
    }

    private void OnUpdateRandomClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Items.Count == 0) return;

        // Update 100 random rows to demonstrate INPC with large data
        var sw = Stopwatch.StartNew();
        int updateCount = Math.Min(100, _viewModel.Items.Count);
        for (int i = 0; i < updateCount; i++)
        {
            var index = _random.Next(_viewModel.Items.Count);
            _viewModel.Items[index].Value = Math.Round(_random.NextDouble() * 1000, 2);
            _viewModel.Items[index].Score = Math.Round(_random.NextDouble() * 100, 1);
            _viewModel.Items[index].Amount = _random.Next(100, 500000);
        }
        sw.Stop();
        statusLabel.Text = $"Updated {updateCount} rows in {sw.ElapsedMilliseconds:N0}ms";
        Debug.WriteLine($"[LargeDataPage] UpdateRandom({updateCount}): {sw.ElapsedMilliseconds}ms");
    }
}

/// <summary>ViewModel for the Large Data page.</summary>
public class LargeDataPageViewModel : INotifyPropertyChanged
{
    private ObservableCollection<LargeDataItem> _items = new();

    /// <summary>The collection bound to the grid's ItemsSource.</summary>
    public ObservableCollection<LargeDataItem> Items
    {
        get => _items;
        set
        {
            _items = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Data item for large dataset stress testing.</summary>
public class LargeDataItem : INotifyPropertyChanged
{
    private int _rowNumber;
    private string _fullName = string.Empty;
    private string _category = string.Empty;
    private double _value;
    private decimal _amount;
    private DateTime _createdDate;
    private bool _isEnabled;
    private string _region = string.Empty;
    private double _score;
    private string _notes = string.Empty;

    public int RowNumber { get => _rowNumber; set { _rowNumber = value; OnPropertyChanged(); } }
    public string FullName { get => _fullName; set { _fullName = value; OnPropertyChanged(); } }
    public string Category { get => _category; set { _category = value; OnPropertyChanged(); } }
    public double Value { get => _value; set { _value = value; OnPropertyChanged(); } }
    public decimal Amount { get => _amount; set { _amount = value; OnPropertyChanged(); } }
    public DateTime CreatedDate { get => _createdDate; set { _createdDate = value; OnPropertyChanged(); } }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; OnPropertyChanged(); } }
    public string Region { get => _region; set { _region = value; OnPropertyChanged(); } }
    public double Score { get => _score; set { _score = value; OnPropertyChanged(); } }
    public string Notes { get => _notes; set { _notes = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
