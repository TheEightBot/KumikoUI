using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using Microsoft.UI.Xaml.Controls;

namespace SampleApp.Uno;

/// <summary>
/// MVVM + per-column EditTriggers + action-button column demo (← SampleApp.Maui MvvmActionsPage).
/// The columns and the Actions column's <see cref="UnoActionButtonDefinition"/> buttons are declared in
/// the XAML; their <c>Command</c>s are bound to the ViewModel's ICommands. The
/// renderer is an interactive cell: a tap executes the command with the row item, without an editor.
/// Confirmation / details use a WinUI <see cref="ContentDialog"/> (the Uno analogue of MAUI's
/// DisplayAlert / modal page).
/// </summary>
public sealed partial class MvvmActionsPage : Page
{
    private readonly MvvmActionsViewModel _viewModel;

    public MvvmActionsPage()
    {
        this.InitializeComponent();

        _viewModel = new MvvmActionsViewModel();
        _viewModel.DeleteRequested += OnDeleteRequested;
        _viewModel.ViewDetailsRequested += OnViewDetailsRequested;

        DataContext = _viewModel;
        _viewModel.LoadData();

        actionsGrid.ItemsSource = _viewModel.Employees;
    }

    private void OnSelectFirstClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _viewModel.SelectedEmployee = _viewModel.Employees.FirstOrDefault();

    private void OnClearSelectionClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _viewModel.SelectedEmployee = null;

    // WinUI shows one ContentDialog at a time and throws when a second one opens, which a double tap on an
    // action button would cause: the first request wins until its dialog closes.
    private bool _dialogOpen;

    private async void OnDeleteRequested(object? sender, Employee employee)
    {
        if (_dialogOpen) return;
        _dialogOpen = true;
        try
        {
            await ConfirmDeleteAsync(employee);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async System.Threading.Tasks.Task ConfirmDeleteAsync(Employee employee)
    {
        var dialog = new ContentDialog
        {
            Title = "Delete Employee",
            Content = $"Are you sure you want to delete {employee.Name}?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            _viewModel.Employees.Remove(employee);
    }

    private async void OnViewDetailsRequested(object? sender, Employee employee)
    {
        if (_dialogOpen) return;
        _dialogOpen = true;
        try
        {
            await ShowDetailsAsync(employee);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async System.Threading.Tasks.Task ShowDetailsAsync(Employee employee)
    {
        var dialog = new ContentDialog
        {
            Title = $"Employee #{employee.Id}",
            Content =
                $"Name: {employee.Name}\n" +
                $"Department: {employee.Department}\n" +
                $"Level: {employee.Level}\n" +
                $"Salary: {employee.Salary:C0}\n" +
                $"Hire Date: {employee.HireDate:yyyy-MM-dd}\n" +
                $"Active: {(employee.IsActive ? "Yes" : "No")}\n" +
                $"Performance: {employee.Performance:N1}\n" +
                $"Rating: {employee.Rating}",
            CloseButtonText = "Close",
            XamlRoot = this.XamlRoot,
        };

        await dialog.ShowAsync();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ViewModel
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// ViewModel for the MVVM actions demo. Exposes employee data and two commands that raise events —
/// the View (page code-behind) handles UI concerns such as showing confirmation dialogs.
/// </summary>
public class MvvmActionsViewModel : INotifyPropertyChanged
{
    private Employee? _selectedEmployee;
    private string _lastChange = "No selection change yet";

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

    public MvvmActionsViewModel()
    {
        DeleteCommand = new RelayCommand<Employee>(e => DeleteRequested?.Invoke(this, e));
        ViewDetailsCommand = new RelayCommand<Employee>(e => ViewDetailsRequested?.Invoke(this, e));
        SelectionChangedCommand = new RelayCommand<KumikoSelectionChangedEventArgs>(e =>
        {
            LastChange = $"Last change: +{e.AddedItems.Count} / -{e.RemovedItems.Count}";
            OnPropertyChanged(nameof(SelectionSummary));
        });
    }

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

/// <summary>
/// Minimal generic <see cref="ICommand"/> implementation for the MVVM demo (the Uno analogue of
/// MAUI's <c>Command&lt;T&gt;</c>). The action receives the command parameter cast to
/// <typeparamref name="T"/> — here the row's <see cref="Employee"/> supplied by DrawnActionButtons.
/// </summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        _canExecute is null || (parameter is T t && _canExecute(t));

    public void Execute(object? parameter)
    {
        if (parameter is T t)
            _execute(t);
    }

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
