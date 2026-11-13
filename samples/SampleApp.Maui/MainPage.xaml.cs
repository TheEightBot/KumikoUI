using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace SampleApp.Maui;

public partial class MainPage : ContentPage
{
	private static readonly string[] Departments = { "Engineering", "Marketing", "Sales", "HR", "Finance", "Design", "Support" };
	private static readonly string[] Cities = { "New York", "San Francisco", "Austin", "Seattle", "Chicago", "Denver", "Boston" };
	private static readonly string[] Levels = { "Junior", "Mid", "Senior", "Staff", "Principal", "Director" };
	private static readonly string[] FirstNames = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Hank", "Iris", "Jack" };
	private static readonly string[] LastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Wilson", "Moore" };

	// Every built-in glyph drawn from the Material Icons font registered in MauiProgram (see RegisterSkiaFonts)
	private static readonly KumikoFontIconProvider MaterialIcons = KumikoFontIconProvider.CreateMaterialIcons("MaterialIcons");

	private readonly MainPageViewModel _viewModel = new();
	private readonly Random _random = new(42);
	private bool _materialIcons;

	public MainPage()
	{
		InitializeComponent();
		BindingContext = _viewModel;
		LoadSampleData();

		// Enable drag handle by default
		var style = kumiko.GridStyle;
		style.ShowRowDragHandle = true;
		kumiko.GridStyle = style;

		// Right click (or a long press on touch) opens a context menu
		kumiko.ContextMenuRequested += OnGridContextMenuRequested;
	}

	// ── Context menu ──
	// MAUI has no API to open a MenuFlyout at a point, so the menu is an action sheet: a sheet on phones and
	// tablets, a dialog on the desktop.

	private async void OnGridContextMenuRequested(object? sender, KumikoContextRequestedEventArgs e)
	{
		if (e.IsHeader && e.Column is { } column)
		{
			const string ascending = "Sort ascending", descending = "Sort descending", clear = "Clear sort";
			var choice = await DisplayActionSheetAsync($"Column: {column.Header}", "Cancel", null, ascending, descending, clear);
			if (choice == ascending) kumiko.DataSource.SetSort(column, KumikoSortDirection.Ascending);
			else if (choice == descending) kumiko.DataSource.SetSort(column, KumikoSortDirection.Descending);
			else if (choice == clear) kumiko.DataSource.ClearSort();
		}
		else if (e.DataItem is Employee employee)
		{
			// The grid selected the row first (unless it was already part of the selection)
			var selected = kumiko.Selection.SelectedItems.OfType<Employee>().ToList();
			string delete = selected.Count > 1 ? $"Delete {selected.Count} rows" : "Delete row";
			const string duplicate = "Duplicate", raise = "Give a 5% raise";
			var choice = await DisplayActionSheetAsync(employee.Name, "Cancel", delete, duplicate, raise);
			if (choice == delete)
			{
				foreach (var item in selected.Count > 0 ? selected : new List<Employee> { employee })
					_viewModel.Employees.Remove(item);
			}
			else if (choice == duplicate)
			{
				OnAddRowClicked(this, EventArgs.Empty);
				var copy = _viewModel.Employees[^1];
				copy.Name = $"{employee.Name} (copy)";
				copy.Department = employee.Department;
				copy.Salary = employee.Salary;
			}
			else if (choice == raise)
			{
				employee.Salary = Math.Round(employee.Salary * 1.05m, 0);
			}
		}
		else if (e.Region == HitRegion.EmptyArea)
		{
			const string add = "Add row";
			if (await DisplayActionSheetAsync("Grid", "Cancel", null, add) == add)
				OnAddRowClicked(this, EventArgs.Empty);
		}
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
				Approved = _random.Next(3) switch { 0 => true, 1 => false, _ => null },
				City = Cities[_random.Next(Cities.Length)],
				Level = Levels[_random.Next(Levels.Length)],
				Performance = Math.Round(_random.NextDouble() * 100, 1),
				Rating = _random.Next(1, 6)
			});
		}
	}

	// ── Demo buttons ──

	private void OnAddRowClicked(object? sender, EventArgs e)
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

	private void OnRemoveLastRowClicked(object? sender, EventArgs e)
	{
		if (_viewModel.Employees.Count > 0)
			_viewModel.Employees.RemoveAt(_viewModel.Employees.Count - 1);
	}

	private void OnUpdateRandomSalaryClicked(object? sender, EventArgs e)
	{
		if (_viewModel.Employees.Count > 0)
		{
			var index = _random.Next(_viewModel.Employees.Count);
			_viewModel.Employees[index].Salary = _random.Next(45000, 200000);
		}
	}

	private void OnRandomizeRatingsClicked(object? sender, EventArgs e)
	{
		// Update 10 random employees' ratings and performance to show INPC in action
		for (int i = 0; i < 10 && _viewModel.Employees.Count > 0; i++)
		{
			var index = _random.Next(_viewModel.Employees.Count);
			_viewModel.Employees[index].Rating = _random.Next(1, 6);
			_viewModel.Employees[index].Performance = Math.Round(_random.NextDouble() * 100, 1);
		}
	}

	private void OnToggleRowDragClicked(object? sender, EventArgs e)
	{
		var style = kumiko.GridStyle;

		// Cycle: Handle → Full Row → Off → Handle
		if (style.ShowRowDragHandle)
		{
			// Handle mode → Full row mode
			style.ShowRowDragHandle = false;
			style.AllowRowDragDrop = true;
			rowDragToggle.Text = "Row Drag: Full Row";
		}
		else if (style.AllowRowDragDrop)
		{
			// Full row mode → Off
			style.AllowRowDragDrop = false;
			rowDragToggle.Text = "Row Drag: OFF";
		}
		else
		{
			// Off → Handle mode
			style.ShowRowDragHandle = true;
			rowDragToggle.Text = "Row Drag: Handle";
		}

		kumiko.GridStyle = style;
	}

	private void OnToggleHandlePositionClicked(object? sender, EventArgs e)
	{
		var style = kumiko.GridStyle;
		style.RowDragHandlePosition = style.RowDragHandlePosition == KumikoUI.Core.Models.DragHandlePosition.Left
			? KumikoUI.Core.Models.DragHandlePosition.Right
			: KumikoUI.Core.Models.DragHandlePosition.Left;
		kumiko.GridStyle = style;
		handlePosToggle.Text = style.RowDragHandlePosition == KumikoUI.Core.Models.DragHandlePosition.Left
			? "Handle: Left" : "Handle: Right";
	}

	private void OnToggleDismissKeyboardClicked(object? sender, EventArgs e)
	{
		kumiko.DismissKeyboardOnEnter = !kumiko.DismissKeyboardOnEnter;
		dismissKbToggle.Text = kumiko.DismissKeyboardOnEnter
			? "Enter Dismisses KB: ON"
			: "Enter Dismisses KB: OFF";
	}

	private void OnToggleThemeClicked(object? sender, EventArgs e)
	{
		// Cycle: Light → Dark → HighContrast → Light
		kumiko.Theme = kumiko.Theme switch
		{
			KumikoUI.Core.Models.KumikoThemeMode.Light => KumikoUI.Core.Models.KumikoThemeMode.Dark,
			KumikoUI.Core.Models.KumikoThemeMode.Dark => KumikoUI.Core.Models.KumikoThemeMode.HighContrast,
			_ => KumikoUI.Core.Models.KumikoThemeMode.Light
		};

		themeToggle.Text = $"Theme: {kumiko.Theme}";

		// Re-apply row drag handle settings and the icon set since theme replaces the style instance
		var style = kumiko.GridStyle;
		style.ShowRowDragHandle = true;
		style.IconProvider = _materialIcons ? MaterialIcons : KumikoIconProvider.Default;
		kumiko.GridStyle = style;
		rowDragToggle.Text = "Row Drag: Handle";
	}

	private void OnToggleIconsClicked(object? sender, EventArgs e)
	{
		// Switch every glyph (sort arrows, filter buttons, group chevrons, check marks, editor buttons) between the
		// built-in set and the Material Icons font
		_materialIcons = !_materialIcons;
		var style = kumiko.GridStyle;
		style.IconProvider = _materialIcons ? MaterialIcons : KumikoIconProvider.Default;
		kumiko.GridStyle = style;
		iconsToggle.Text = _materialIcons ? "Icons: Material" : "Icons: Built-in";
	}

	private void OnToggleEditTriggerClicked(object? sender, EventArgs e)
	{
		// Cycle: Double Tap → Single Tap → Long Press → All → Double Tap
		var current = kumiko.EditTriggers;

		if (current == (EditTrigger.DoubleTap | EditTrigger.F2Key | EditTrigger.Typing))
		{
			kumiko.EditTriggers = EditTrigger.SingleTap | EditTrigger.F2Key | EditTrigger.Typing;
			editTriggerToggle.Text = "Edit: Single Tap";
		}
		else if (current == (EditTrigger.SingleTap | EditTrigger.F2Key | EditTrigger.Typing))
		{
			kumiko.EditTriggers = EditTrigger.LongPress | EditTrigger.F2Key | EditTrigger.Typing;
			editTriggerToggle.Text = "Edit: Long Press";
		}
		else if (current == (EditTrigger.LongPress | EditTrigger.F2Key | EditTrigger.Typing))
		{
			kumiko.EditTriggers = EditTrigger.SingleTap | EditTrigger.DoubleTap | EditTrigger.LongPress | EditTrigger.F2Key | EditTrigger.Typing;
			editTriggerToggle.Text = "Edit: All";
		}
		else
		{
			kumiko.EditTriggers = EditTrigger.DoubleTap | EditTrigger.F2Key | EditTrigger.Typing;
			editTriggerToggle.Text = "Edit: Double Tap";
		}
	}

	private void OnToggleSelectionModeClicked(object? sender, EventArgs e)
	{
		kumiko.EditTextSelectionMode = kumiko.EditTextSelectionMode == EditTextSelectionMode.SelectAll
			? EditTextSelectionMode.CursorAtEnd
			: EditTextSelectionMode.SelectAll;

		selectionModeToggle.Text = kumiko.EditTextSelectionMode == EditTextSelectionMode.SelectAll
			? "Select: All"
			: "Select: End";
	}

	// Column order, widths, visibility, sort, filters and grouping, kept across app runs (#98).
	private const string LayoutKey = "MainPage.GridLayout";

	private void OnSaveLayoutClicked(object? sender, EventArgs e)
	{
		Preferences.Set(LayoutKey, kumiko.GetLayoutState().ToJson());
		restoreLayoutButton.Text = "Restore Layout (saved)";
	}

	private void OnRestoreLayoutClicked(object? sender, EventArgs e)
	{
		if (Preferences.Get(LayoutKey, null) is not { } json)
		{
			restoreLayoutButton.Text = "Restore Layout (nothing saved)";
			return;
		}

		kumiko.ApplyLayoutState(KumikoLayoutState.FromJson(json));
		restoreLayoutButton.Text = "Restore Layout";
	}
}

/// <summary>
/// Simple ViewModel with an ObservableCollection for data binding demo.
/// </summary>
public class MainPageViewModel
{
	public ObservableCollection<Employee> Employees { get; } = new();
}

/// <summary>
/// Employee model implementing INotifyPropertyChanged for live cell updates.
/// Showcases every column type: Text, Numeric, Boolean, Date, ComboBox, Picker, Template.
/// </summary>
[KumikoUI.Core.Data.KumikoGridItem]
public class Employee : INotifyPropertyChanged
{
	private int _id;
	private string _name = string.Empty;
	private string _department = string.Empty;
	private decimal _salary;
	private DateTime _hireDate;
	private bool _isActive;
	private bool? _approved;
	private string _city = string.Empty;
	private string _level = string.Empty;
	private double _performance;
	private int _rating;

	public int Id
	{
		get => _id;
		set { _id = value; OnPropertyChanged(); }
	}

	public string Name
	{
		get => _name;
		set { _name = value; OnPropertyChanged(); }
	}

	/// <summary>ComboBox column — dropdown editor.</summary>
	public string Department
	{
		get => _department;
		set { _department = value; OnPropertyChanged(); }
	}

	public decimal Salary
	{
		get => _salary;
		set { _salary = value; OnPropertyChanged(); }
	}

	public DateTime HireDate
	{
		get => _hireDate;
		set { _hireDate = value; OnPropertyChanged(); }
	}

	/// <summary>Boolean column — checkbox toggle.</summary>
	public bool IsActive
	{
		get => _isActive;
		set { _isActive = value; OnPropertyChanged(); }
	}

	/// <summary>Tri-state Boolean column: unchecked, checked, or indeterminate (null).</summary>
	public bool? Approved
	{
		get => _approved;
		set { _approved = value; OnPropertyChanged(); }
	}

	public string City
	{
		get => _city;
		set { _city = value; OnPropertyChanged(); }
	}

	/// <summary>Picker column — scroll-wheel selector.</summary>
	public string Level
	{
		get => _level;
		set { _level = value; OnPropertyChanged(); }
	}

	/// <summary>ProgressBar column — drawn as a bar (0-100).</summary>
	public double Performance
	{
		get => _performance;
		set { _performance = value; OnPropertyChanged(); }
	}

	/// <summary>Template column — edited with numeric up/down (1-5).</summary>
	public int Rating
	{
		get => _rating;
		set { _rating = value; OnPropertyChanged(); }
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
