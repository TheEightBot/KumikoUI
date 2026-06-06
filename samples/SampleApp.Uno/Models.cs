using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SampleApp.Uno;

/// <summary>
/// Employee model implementing <see cref="INotifyPropertyChanged"/> for live cell updates.
/// Showcases every column type: Text, Numeric, Boolean, Date, ComboBox, Picker, Template.
/// Shared by <see cref="BasicGridPage"/> and <see cref="MvvmActionsPage"/>.
/// </summary>
public class Employee : INotifyPropertyChanged
{
    private int _id;
    private string _name = string.Empty;
    private string _department = string.Empty;
    private decimal _salary;
    private System.DateTime _hireDate;
    private bool _isActive;
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

    public System.DateTime HireDate
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

    /// <summary>Template column — rendered as a progress bar (0-100).</summary>
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

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
