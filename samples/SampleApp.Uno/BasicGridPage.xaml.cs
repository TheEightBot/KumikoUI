using System;
using System.Collections.ObjectModel;
using System.Linq;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

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

    // Every built-in glyph drawn from the Material Icons font registered in App.OnLaunched (KumikoFonts)
    private static readonly KumikoFontIconProvider MaterialIcons = KumikoFontIconProvider.CreateMaterialIcons("MaterialIcons");

    private readonly BasicGridViewModel _viewModel = new();
    private readonly Random _random = new(42);
    private KumikoThemeMode _themeMode = KumikoThemeMode.Light;
    private bool _themeChosen;
    private bool _materialIcons;

    public BasicGridPage()
    {
        StartupTrace.Mark("BasicGridPage ctor start");
        this.InitializeComponent();
        StartupTrace.Mark("BasicGridPage InitializeComponent done");
        DataContext = _viewModel;
        LoadSampleData();
        StartupTrace.Mark("BasicGridPage data loaded");

        // Rows can be dragged by their handle, as on the MAUI sample's main page (the Row Drag button cycles the modes)
        kumiko.ShowRowDragHandle = true;

        // A right click or a long press on a header, its menu button, or the Menu key (Shift+F10) opens the grid's own
        // column menu: sort, filter, group by, hide and auto-fit, drawn at the pointer like the MAUI sample's
        kumiko.IsColumnMenuEnabled = true;
        kumiko.ShowColumnMenuButton = true;
        kumiko.ColumnMenuOpening += OnColumnMenuOpening;

        // Right click (or a long press on touch) on a row opens the grid's own row menu: copy, copy with headers, edit and
        // delete, plus the app's items below. The empty area has no built-in menu, so the app answers it with a MenuFlyout.
        kumiko.AllowDeleting = true;
        kumiko.IsRowMenuEnabled = true;
        kumiko.RowMenuOpening += OnRowMenuOpening;
        kumiko.ContextMenuRequested += OnGridContextMenuRequested;

        // The grid follows the app theme until a theme is picked: the theme button starts from the one it shows
        Loaded += (_, _) =>
        {
            if (_themeChosen) return;
            _themeMode = kumiko.ActualTheme == ElementTheme.Dark ? KumikoThemeMode.Dark : KumikoThemeMode.Light;
            themeToggle.Content = $"Theme: {_themeMode}";
        };
    }

    // ── Column menu ──

    // The built-in items can be removed, reordered or joined by the app's own
    private void OnColumnMenuOpening(object? sender, KumikoColumnMenuOpeningEventArgs e)
    {
        e.Items.Add(DrawnMenuItem.Separator());
        e.Items.Add(new DrawnMenuItem("ClearAllSorts", "Clear All Sorts", () => kumiko.DataSource.ClearSort())
        {
            IsEnabled = kumiko.Columns.Any(c => c.SortDirection != KumikoSortDirection.None),
        });
    }

    // ── Row menu ──

    // The app's own items follow the grid's, for a data row only (a group header row has no employee)
    private void OnRowMenuOpening(object? sender, KumikoRowMenuOpeningEventArgs e)
    {
        if (e.DataItem is not Employee employee) return;

        e.Items.Add(DrawnMenuItem.Separator());
        e.Items.Add(new DrawnMenuItem("Raise", "Give a 5% raise", () => employee.Salary = Math.Round(employee.Salary * 1.05m, 0)));
        e.Items.Add(new DrawnMenuItem("Duplicate", "Duplicate", () =>
        {
            OnAddRowClicked(this, new RoutedEventArgs());
            var copy = _viewModel.Employees[^1];
            copy.Name = $"{employee.Name} (copy)";
            copy.Department = employee.Department;
            copy.Salary = employee.Salary;
        }));
    }

    // ── Context menu ──
    // Column headers and rows use the grid's drawn menus (see above); the empty area gets a MenuFlyout.

    private void OnGridContextMenuRequested(object? sender, KumikoContextRequestedEventArgs e)
    {
        if (e.Region != HitRegion.EmptyArea) return;

        var menu = new MenuFlyout();
        AddItem(menu, "Add row", () => OnAddRowClicked(this, new RoutedEventArgs()));
        menu.ShowAt(kumiko, new FlyoutShowOptions { Position = new Point(e.X, e.Y) });
    }

    private static void AddItem(MenuFlyout menu, string text, Action action)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
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
                ShiftStart = new TimeOnly(_random.Next(6, 12), _random.Next(0, 4) * 15),
                LastLogin = DateTime.Now.AddMinutes(-_random.Next(10, 20000)),
                IsActive = _random.NextDouble() > 0.15,
                Approved = _random.Next(3) switch { 0 => true, 1 => false, _ => null },
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
        // Cycle: Light → Dark → HighContrast → Light. The style comes from KumikoTheme.Create (what the Theme
        // property applies) so the chosen icon set can go on it; assigning it stops following the app theme.
        _themeChosen = true;
        _themeMode = _themeMode switch
        {
            KumikoThemeMode.Light => KumikoThemeMode.Dark,
            KumikoThemeMode.Dark => KumikoThemeMode.HighContrast,
            _ => KumikoThemeMode.Light
        };

        // A new style instance: keep the chosen icon set
        var style = KumikoTheme.Create(_themeMode);
        style.IconProvider = _materialIcons ? MaterialIcons : KumikoIconProvider.Default;
        kumiko.GridStyle = style;
        themeToggle.Content = $"Theme: {_themeMode}";
    }

    private void OnToggleIconsClicked(object sender, RoutedEventArgs e)
    {
        // Switch every glyph (sort arrows, filter buttons, group chevrons, check marks, editor buttons) between the
        // built-in set and the Material Icons font
        _materialIcons = !_materialIcons;
        var style = kumiko.GridStyle;
        style.IconProvider = _materialIcons ? MaterialIcons : KumikoIconProvider.Default;
        kumiko.GridStyle = style;
        iconsToggle.Content = _materialIcons ? "Icons: Material" : "Icons: Built-in";
    }

    private void OnToggleRowDragClicked(object sender, RoutedEventArgs e)
    {
        // Cycle: Handle → Full Row → Off → Handle. Both are explicit settings, so they survive a theme change.
        if (kumiko.ShowRowDragHandle == true)
        {
            kumiko.ShowRowDragHandle = false;
            kumiko.AllowRowDragDrop = true;
            rowDragToggle.Content = "Row Drag: Full Row";
        }
        else if (kumiko.AllowRowDragDrop == true)
        {
            kumiko.AllowRowDragDrop = false;
            rowDragToggle.Content = "Row Drag: OFF";
        }
        else
        {
            kumiko.ShowRowDragHandle = true;
            rowDragToggle.Content = "Row Drag: Handle";
        }
    }

    private void OnToggleSelectionModeClicked(object sender, RoutedEventArgs e)
    {
        kumiko.GridSelectionMode = kumiko.GridSelectionMode == KumikoSelectionMode.Extended
            ? KumikoSelectionMode.Single
            : KumikoSelectionMode.Extended;

        selectionModeToggle.Content = kumiko.GridSelectionMode == KumikoSelectionMode.Extended
            ? "Select: All"
            : "Select: Single";
    }

    // Column order, widths, visibility, sort, filters and grouping, kept across app runs (#98).
    private const string LayoutKey = "BasicGridPage.GridLayout";

    private void OnSaveLayoutClicked(object sender, RoutedEventArgs e)
    {
        Windows.Storage.ApplicationData.Current.LocalSettings.Values[LayoutKey] = kumiko.GetLayoutState().ToJson();
        restoreLayoutButton.Content = "Restore Layout (saved)";
    }

    private void OnRestoreLayoutClicked(object sender, RoutedEventArgs e)
    {
        if (Windows.Storage.ApplicationData.Current.LocalSettings.Values[LayoutKey] is not string json)
        {
            restoreLayoutButton.Content = "Restore Layout (nothing saved)";
            return;
        }

        kumiko.ApplyLayoutState(KumikoLayoutState.FromJson(json));
        restoreLayoutButton.Content = "Restore Layout";
    }
}

/// <summary>Simple ViewModel with an ObservableCollection for data binding demo.</summary>
public class BasicGridViewModel
{
    public ObservableCollection<Employee> Employees { get; } = new();
}
