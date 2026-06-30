using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using Microsoft.UI.Xaml.Controls;

namespace SampleApp.Uno;

/// <summary>
/// MVVM + per-column EditTriggers + action-button column demo (← SampleApp.Maui MvvmActionsPage).
/// The Actions column hosts <see cref="ActionButtonsCellRenderer"/> buttons whose Core
/// <see cref="ActionButtonDefinition.Command"/> is bound to the ViewModel's ICommands. The
/// interactive <c>DrawnActionButtons</c> editor (single-tap) executes the command with the row item.
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

        BuildColumns();
        actionsGrid.ItemsSource = _viewModel.Employees;
    }

    private void BuildColumns()
    {
        // Id — read-only.
        actionsGrid.Columns!.Add(new DataGridColumn
        {
            Header = "Id",
            PropertyName = "Id",
            ColumnType = DataGridColumnType.Numeric,
            Width = 52,
            IsReadOnly = true,
            AllowTabStop = false,
        });

        // Name — single-tap, F2, OR typing starts editing (most accessible).
        actionsGrid.Columns!.Add(new DataGridColumn
        {
            Header = "Name",
            PropertyName = "Name",
            Width = 160,
            EditTriggers = EditTrigger.SingleTap | EditTrigger.F2Key | EditTrigger.Typing,
        });

        // Department — double-tap or F2 only (typing excluded for a ComboBox).
        actionsGrid.Columns!.Add(new DataGridColumn
        {
            Header = "Department",
            PropertyName = "Department",
            ColumnType = DataGridColumnType.ComboBox,
            Width = 130,
            EditorItemsString = "Engineering,Marketing,Finance,HR,Design,Product,Sales",
            EditTriggers = EditTrigger.DoubleTap | EditTrigger.F2Key,
        });

        // Level — double-tap ONLY.
        actionsGrid.Columns!.Add(new DataGridColumn
        {
            Header = "Level",
            PropertyName = "Level",
            ColumnType = DataGridColumnType.Picker,
            Width = 90,
            EditorItemsString = "Junior,Mid,Senior,Lead,Principal",
            EditTriggers = EditTrigger.DoubleTap,
        });

        // Salary — F2 key ONLY (protects financial data from pointer gestures).
        actionsGrid.Columns!.Add(new DataGridColumn
        {
            Header = "Salary",
            PropertyName = "Salary",
            ColumnType = DataGridColumnType.Numeric,
            Format = "C0",
            Width = 110,
            TextAlignment = GridTextAlignment.Right,
            EditTriggers = EditTrigger.F2Key,
        });

        // Active — Boolean; toggles on tap.
        actionsGrid.Columns!.Add(new DataGridColumn
        {
            Header = "Active",
            PropertyName = "IsActive",
            ColumnType = DataGridColumnType.Boolean,
            Width = 60,
            AllowTabStop = false,
        });

        // Actions column — Template with inline action buttons.
        // Column-level EditTriggers=SingleTap ensures buttons activate on a single tap even though
        // the grid default is DoubleTap. PropertyName="" passes the full Employee as the cell value,
        // so DrawnActionButtons.RowItem (→ Command parameter) is the row's Employee.
        actionsGrid.Columns!.Add(new DataGridColumn
        {
            Header = "Actions",
            PropertyName = "",
            ColumnType = DataGridColumnType.Template,
            Width = 200,
            IsReadOnly = false,
            AllowSorting = false,
            AllowFiltering = false,
            AllowTabStop = false,
            SizeMode = ColumnSizeMode.Star,
            StarWeight = 1,
            EditTriggers = EditTrigger.SingleTap,
            CustomCellRenderer = new ActionButtonsCellRenderer
            {
                Buttons =
                {
                    new ActionButtonDefinition
                    {
                        Label = "Details",
                        BackgroundColor = new GridColor(13, 110, 253),
                        Command = _viewModel.ViewDetailsCommand,
                    },
                    new ActionButtonDefinition
                    {
                        Label = "Delete",
                        BackgroundColor = new GridColor(220, 53, 69),
                        Command = _viewModel.DeleteCommand,
                    },
                },
            },
        });
    }

    private async void OnDeleteRequested(object? sender, Employee employee)
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
public class MvvmActionsViewModel
{
    /// <summary>The collection bound to the DataGridView.</summary>
    public ObservableCollection<Employee> Employees { get; } = new();

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
