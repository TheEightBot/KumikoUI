using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Models;

namespace SampleApp.Maui;

public partial class GroupingPage : ContentPage
{
    private static readonly string[] Departments = { "Engineering", "Marketing", "Sales", "HR", "Finance", "Design", "Support" };
    private static readonly string[] Cities = { "New York", "San Francisco", "Austin", "Seattle", "Chicago", "Denver", "Boston" };
    private static readonly string[] Levels = { "Junior", "Mid", "Senior", "Staff", "Principal", "Director" };
    private static readonly string[] FirstNames = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Hank", "Iris", "Jack" };
    private static readonly string[] LastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Wilson", "Moore" };

    private readonly GroupingPageViewModel _viewModel = new();
    private readonly Random _random = new(123);
    private bool _hasGroupSummary;

    public GroupingPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
        LoadSampleData();
    }

    private void LoadSampleData()
    {
        for (int i = 1; i <= 300; i++)
        {
            _viewModel.Employees.Add(new GroupingEmployee
            {
                Id = i,
                Name = $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]}",
                Department = Departments[_random.Next(Departments.Length)],
                Salary = _random.Next(45000, 180000),
                IsActive = _random.NextDouble() > 0.15,
                City = Cities[_random.Next(Cities.Length)],
                Level = Levels[_random.Next(Levels.Length)],
            });
        }
    }

    // ── Grouping controls ──

    private void OnGroupByDepartmentClicked(object? sender, EventArgs e)
    {
        kumiko.DataSource.ClearGroupDescriptions();
        kumiko.DataSource.AddGroupDescription(new GroupDescription("Department", "Department"));
    }

    private void OnGroupByLevelClicked(object? sender, EventArgs e)
    {
        kumiko.DataSource.ClearGroupDescriptions();
        kumiko.DataSource.AddGroupDescription(new GroupDescription("Level", "Level"));
    }

    private void OnGroupByBothClicked(object? sender, EventArgs e)
    {
        kumiko.DataSource.ClearGroupDescriptions();
        kumiko.DataSource.AddGroupDescription(new GroupDescription("Department", "Department"));
        kumiko.DataSource.AddGroupDescription(new GroupDescription("Level", "Level"));
    }

    private void OnClearGroupsClicked(object? sender, EventArgs e)
    {
        kumiko.DataSource.ClearGroupDescriptions();
    }

    private void OnExpandAllClicked(object? sender, EventArgs e)
    {
        kumiko.DataSource.ExpandAllGroups();
    }

    private void OnCollapseAllClicked(object? sender, EventArgs e)
    {
        kumiko.DataSource.CollapseAllGroups();
    }

    private void OnAddGroupSummaryClicked(object? sender, EventArgs e)
    {
        if (_hasGroupSummary)
        {
            kumiko.DataSource.ClearGroupSummaryRows();
            _hasGroupSummary = false;
        }
        else
        {
            var summaryRow = new GroupSummaryRow
            {
                Name = "GroupTotals",
                Title = "Group Summary"
            };
            summaryRow.Columns.Add(new SummaryColumnDescription
            {
                PropertyName = "Salary",
                SummaryType = SummaryType.Sum,
                Format = "C0"
            });
            summaryRow.Columns.Add(new SummaryColumnDescription
            {
                PropertyName = "Id",
                SummaryType = SummaryType.Count,
                Label = "Count: "
            });
            summaryRow.Columns.Add(new SummaryColumnDescription
            {
                PropertyName = "Salary",
                SummaryType = SummaryType.Average,
                Format = "C0"
            });
            kumiko.DataSource.AddGroupSummaryRow(summaryRow);
            _hasGroupSummary = true;
        }
    }

    private void OnToggleFilteringClicked(object? sender, EventArgs e)
    {
        kumiko.AllowFiltering = !kumiko.AllowFiltering;
    }
}

/// <summary>ViewModel for the Grouping page.</summary>
public class GroupingPageViewModel
{
    public ObservableCollection<GroupingEmployee> Employees { get; } = new();
}

/// <summary>Employee model for the Grouping page demo.</summary>
public class GroupingEmployee : INotifyPropertyChanged
{
    private int _id;
    private string _name = string.Empty;
    private string _department = string.Empty;
    private decimal _salary;
    private bool _isActive;
    private string _city = string.Empty;
    private string _level = string.Empty;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string Department { get => _department; set { _department = value; OnPropertyChanged(); } }
    public decimal Salary { get => _salary; set { _salary = value; OnPropertyChanged(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(); } }
    public string City { get => _city; set { _city = value; OnPropertyChanged(); } }
    public string Level { get => _level; set { _level = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
