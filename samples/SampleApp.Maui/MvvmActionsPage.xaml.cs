using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Models;
using System.Windows.Input;

namespace SampleApp.Maui;

public partial class MvvmActionsPage : ContentPage
{
    private readonly MvvmActionsViewModel _viewModel;

    public MvvmActionsPage()
    {
        InitializeComponent();

        _viewModel = new MvvmActionsViewModel();
        _viewModel.DeleteRequested += OnDeleteRequested;
        _viewModel.ViewDetailsRequested += OnViewDetailsRequested;

        BindingContext = _viewModel;
        _viewModel.LoadData();
    }

    private async void OnDeleteRequested(object? sender, Employee employee)
    {
        bool confirmed = await DisplayAlertAsync(
            "Delete Employee",
            $"Are you sure you want to delete {employee.Name}?",
            "Delete",
            "Cancel");

        if (confirmed)
            _viewModel.Employees.Remove(employee);
    }

    // A double tap on the Details button raises the command twice; only the first may push the page.
    private bool _navigating;

    private async void OnViewDetailsRequested(object? sender, Employee employee)
    {
        if (_navigating) return;
        _navigating = true;
        try
        {
            await Navigation.PushModalAsync(new EmployeeDetailsPage(employee));
        }
        finally
        {
            _navigating = false;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ViewModel
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// ViewModel for the MVVM actions demo. Exposes employee data and two commands
/// that raise events — the View (page code-behind) handles UI concerns such as
/// showing confirmation dialogs and pushing modal pages.
/// </summary>
public class MvvmActionsViewModel : INotifyPropertyChanged
{
    private Employee? _selectedEmployee;
    private string _lastChange = "No selection change yet";

    /// <summary>Creates the commands once; the grid reads them on every binding refresh.</summary>
    public MvvmActionsViewModel()
    {
        SelectionChangedCommand = new Command<KumikoSelectionChangedEventArgs>(e =>
        {
            LastChange = $"Last change: +{e.AddedItems.Count} / -{e.RemovedItems.Count}";
            OnPropertyChanged(nameof(SelectionSummary));
        });
        SelectFirstCommand = new Command(() => SelectedEmployee = Employees.FirstOrDefault());
        ClearSelectionCommand = new Command(() => SelectedEmployee = null);
        DeleteCommand = new Command<Employee>(e => DeleteRequested?.Invoke(this, e));
        ViewDetailsCommand = new Command<Employee>(e => ViewDetailsRequested?.Invoke(this, e));
    }

    /// <summary>The collection bound to the KumikoDataGrid.</summary>
    public ObservableCollection<Employee> Employees { get; } = new();

    /// <summary>Two-way bound to the grid's <c>SelectedItem</c>.</summary>
    public Employee? SelectedEmployee
    {
        get => _selectedEmployee;
        set
        {
            if (ReferenceEquals(_selectedEmployee, value)) return;
            _selectedEmployee = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectionSummary));
        }
    }

    /// <summary>The grid's <c>SelectedItems</c>: the grid selects what is in it and keeps it up to date.</summary>
    public ObservableCollection<Employee> SelectedEmployees { get; } = new();

    /// <summary>Selected name and count, shown above the grid.</summary>
    public string SelectionSummary =>
        $"Selected: {SelectedEmployee?.Name ?? "none"}  ({SelectedEmployees.Count} selected)";

    /// <summary>What the last <c>SelectionChanged</c> reported.</summary>
    public string LastChange
    {
        get => _lastChange;
        private set { _lastChange = value; OnPropertyChanged(); }
    }

    /// <summary>Bound to the grid's <c>SelectionChangedCommand</c>; receives the added and removed items.</summary>
    public ICommand SelectionChangedCommand { get; }

    /// <summary>Selects the first employee from code; the grid follows, even after a sort or filter.</summary>
    public ICommand SelectFirstCommand { get; }

    /// <summary>Clears the selection from code.</summary>
    public ICommand ClearSelectionCommand { get; }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Raised to request confirmation and removal of an employee.</summary>
    public event EventHandler<Employee>? DeleteRequested;

    /// <summary>Raised to request navigation to the employee details overlay.</summary>
    public event EventHandler<Employee>? ViewDetailsRequested;

    /// <summary>Fires <see cref="DeleteRequested"/> with the target employee.</summary>
    public ICommand DeleteCommand { get; }

    /// <summary>Fires <see cref="ViewDetailsRequested"/> with the target employee.</summary>
    public ICommand ViewDetailsCommand { get; }

    /// <summary>Populates <see cref="Employees"/> with deterministic sample data.</summary>
    public void LoadData()
    {
        var departments = new[] { "Engineering", "Marketing", "Finance", "HR", "Design", "Product", "Sales" };
        var levels = new[] { "Junior", "Mid", "Senior", "Lead", "Principal" };
        var firstNames = new[]
        {
            "Alice", "Bob", "Carol", "David", "Eva", "Frank", "Grace", "Henry",
            "Iris", "Jack", "Karen", "Leo", "Mia", "Ned", "Olivia", "Paul",
            "Quinn", "Rosa", "Sam", "Tina", "Uma", "Victor", "Wendy", "Xander",
            "Yasmin", "Zack", "Amy", "Brian"
        };
        var lastNames = new[]
        {
            "Anderson", "Brown", "Chen", "Davis", "Evans", "Foster", "Green",
            "Harris", "Ito", "Jones", "Kim", "Lee", "Martinez", "Nguyen",
            "O'Brien", "Patel", "Quinn", "Rivera", "Smith", "Taylor"
        };

        var rng = new Random(42);

        for (int i = 1; i <= 28; i++)
        {
            string firstName = firstNames[(i - 1) % firstNames.Length];
            string lastName = lastNames[(i - 1) % lastNames.Length];
            string level = levels[rng.Next(levels.Length)];
            string dept = departments[rng.Next(departments.Length)];

            decimal baseSalary = level switch
            {
                "Junior" => 60_000,
                "Mid" => 90_000,
                "Senior" => 130_000,
                "Lead" => 165_000,
                _ => 200_000
            };

            Employees.Add(new Employee
            {
                Id = i,
                Name = $"{firstName} {lastName}",
                Department = dept,
                Level = level,
                Salary = baseSalary + rng.Next(-10_000, 20_000),
                HireDate = DateTime.Today.AddDays(-rng.Next(180, 3650)),
                IsActive = rng.NextDouble() > 0.15,
                City = "New York",
                Performance = Math.Round(rng.NextDouble() * 100, 1),
                Rating = rng.Next(1, 6)
            });
        }
    }
}
