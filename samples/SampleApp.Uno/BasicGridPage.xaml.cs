using System;
using System.Collections.ObjectModel;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// Disambiguate Core's SelectionMode from Microsoft.UI.Xaml.Controls.SelectionMode.
using SelectionMode = KumikoUI.Core.Models.SelectionMode;

namespace SampleApp.Uno;

/// <summary>
/// Basic grid demo (← SampleApp.Maui MainPage). Binds ItemsSource + Columns and showcases every
/// column type, table summary rows, runtime theme switching and selection-mode toggling.
/// </summary>
public sealed partial class BasicGridPage : Page
{
    private static readonly string[] Departments = { "Engineering", "Marketing", "Sales", "HR", "Finance", "Design", "Support" };
    private static readonly string[] Cities = { "New York", "San Francisco", "Austin", "Seattle", "Chicago", "Denver", "Boston" };
    private static readonly string[] Levels = { "Junior", "Mid", "Senior", "Staff", "Principal", "Director" };
    private static readonly string[] FirstNames = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Hank", "Iris", "Jack" };
    private static readonly string[] LastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Wilson", "Moore" };

    private readonly BasicGridViewModel _viewModel = new();
    private readonly Random _random = new(42);
    private DataGridThemeMode _themeMode = DataGridThemeMode.Light;

    public BasicGridPage()
    {
        this.InitializeComponent();
        DataContext = _viewModel;
        LoadSampleData();
    }

    private void LoadSampleData()
    {
        for (int i = 1; i <= 200; i++)
        {
            _viewModel.Employees.Add(new Employee
            {
                Id = i,
                Name = $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]}",
                Department = Departments[_random.Next(Departments.Length)],
                Salary = _random.Next(45000, 180000),
                HireDate = DateTime.Today.AddDays(-_random.Next(100, 3000)),
                IsActive = _random.NextDouble() > 0.15,
                City = Cities[_random.Next(Cities.Length)],
                Level = Levels[_random.Next(Levels.Length)],
                Performance = Math.Round(_random.NextDouble() * 100, 1),
                Rating = _random.Next(1, 6)
            });
        }
    }

    // ── Demo buttons ──

    private void OnAddRowClicked(object sender, RoutedEventArgs e)
    {
        var nextId = _viewModel.Employees.Count + 1;
        _viewModel.Employees.Add(new Employee
        {
            Id = nextId,
            Name = $"New Employee {nextId}",
            Department = "Engineering",
            Salary = _random.Next(50000, 150000),
            HireDate = DateTime.Today,
            IsActive = true,
            City = "Boston",
            Level = "Junior",
            Performance = Math.Round(_random.NextDouble() * 100, 1),
            Rating = _random.Next(1, 6)
        });
    }

    private void OnRemoveLastRowClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Employees.Count > 0)
            _viewModel.Employees.RemoveAt(_viewModel.Employees.Count - 1);
    }

    private void OnUpdateRandomSalaryClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Employees.Count > 0)
        {
            var index = _random.Next(_viewModel.Employees.Count);
            _viewModel.Employees[index].Salary = _random.Next(45000, 200000);
        }
    }

    private void OnRandomizeRatingsClicked(object sender, RoutedEventArgs e)
    {
        // Update 10 random employees' ratings and performance to show INPC in action.
        for (int i = 0; i < 10 && _viewModel.Employees.Count > 0; i++)
        {
            var index = _random.Next(_viewModel.Employees.Count);
            _viewModel.Employees[index].Rating = _random.Next(1, 6);
            _viewModel.Employees[index].Performance = Math.Round(_random.NextDouble() * 100, 1);
        }
    }

    private void OnToggleThemeClicked(object sender, RoutedEventArgs e)
    {
        // Cycle: Light → Dark → HighContrast → Light.
        // The Uno control has no Theme DP (unlike MAUI); apply the theme by assigning the
        // DataGridStyle produced by DataGridTheme.Create — same end result.
        _themeMode = _themeMode switch
        {
            DataGridThemeMode.Light => DataGridThemeMode.Dark,
            DataGridThemeMode.Dark => DataGridThemeMode.HighContrast,
            _ => DataGridThemeMode.Light
        };

        kumiko.GridStyle = DataGridTheme.Create(_themeMode);
        themeToggle.Content = $"Theme: {_themeMode}";
    }

    private void OnToggleSelectionModeClicked(object sender, RoutedEventArgs e)
    {
        kumiko.GridSelectionMode = kumiko.GridSelectionMode == SelectionMode.Extended
            ? SelectionMode.Single
            : SelectionMode.Extended;

        selectionModeToggle.Content = kumiko.GridSelectionMode == SelectionMode.Extended
            ? "Select: All"
            : "Select: Single";
    }
}

/// <summary>Simple ViewModel with an ObservableCollection for data binding demo.</summary>
public class BasicGridViewModel
{
    public ObservableCollection<Employee> Employees { get; } = new();
}
