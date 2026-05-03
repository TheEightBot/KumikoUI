using System.Collections.ObjectModel;
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
        bool confirmed = await DisplayAlert(
            "Delete Employee",
            $"Are you sure you want to delete {employee.Name}?",
            "Delete",
            "Cancel");

        if (confirmed)
            _viewModel.Employees.Remove(employee);
    }

    private async void OnViewDetailsRequested(object? sender, Employee employee)
    {
        await Navigation.PushModalAsync(new EmployeeDetailsPage(employee));
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
public class MvvmActionsViewModel
{
    /// <summary>The collection bound to the DataGridView.</summary>
    public ObservableCollection<Employee> Employees { get; } = new();

    /// <summary>Raised to request confirmation and removal of an employee.</summary>
    public event EventHandler<Employee>? DeleteRequested;

    /// <summary>Raised to request navigation to the employee details overlay.</summary>
    public event EventHandler<Employee>? ViewDetailsRequested;

    /// <summary>Fires <see cref="DeleteRequested"/> with the target employee.</summary>
    public ICommand DeleteCommand => new Command<Employee>(e =>
        DeleteRequested?.Invoke(this, e));

    /// <summary>Fires <see cref="ViewDetailsRequested"/> with the target employee.</summary>
    public ICommand ViewDetailsCommand => new Command<Employee>(e =>
        ViewDetailsRequested?.Invoke(this, e));

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
