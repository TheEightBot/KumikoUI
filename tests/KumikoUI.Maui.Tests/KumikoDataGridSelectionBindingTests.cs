using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Models;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// Two-way bindings of the MAUI <see cref="KumikoDataGrid"/> selection properties to a view model (#267).
/// Each scenario runs on a background thread with a timeout, so a feedback loop between the binding and the
/// grid fails the test instead of hanging the test run.
/// </summary>
public class KumikoDataGridSelectionBindingTests
{
    private static readonly TimeSpan LoopTimeout = TimeSpan.FromSeconds(10);

    private sealed record Person(string Name);

    private sealed class Vm : INotifyPropertyChanged
    {
        private Person? _selectedEmployee;
        private int _selectedIndex = -1;

        public ObservableCollection<Person> Employees { get; } = new()
        {
            new Person("Alice"), new Person("Bob"), new Person("Charlie"),
        };

        public ObservableCollection<object> Selection { get; } = new();

        public List<object?> SelectedEmployeeWrites { get; } = new();

        public Person? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (Equals(_selectedEmployee, value)) return;
                _selectedEmployee = value;
                SelectedEmployeeWrites.Add(value);
                OnPropertyChanged();
            }
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private static (Vm Vm, KumikoDataGrid Grid) Bound(KumikoSelectionMode mode = KumikoSelectionMode.Single)
    {
        var vm = new Vm();
        var grid = new KumikoDataGrid { BindingContext = vm, AnnounceChanges = false, GridSelectionMode = mode };
        grid.Columns.Add(new KumikoColumn { Header = "Name", PropertyName = "Name" });
        grid.SetBinding(KumikoDataGrid.ItemsSourceProperty, nameof(Vm.Employees));
        grid.SetBinding(KumikoDataGrid.SelectedItemProperty, new Binding(nameof(Vm.SelectedEmployee), BindingMode.TwoWay));
        return (vm, grid);
    }

    /// <summary>Runs <paramref name="action"/> on a background thread and fails if it does not return in time.</summary>
    private static void CompletesWithoutLooping(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        })
        { IsBackground = true };
        thread.Start();

        Assert.True(thread.Join(LoopTimeout), "The selection binding did not settle (feedback loop).");
        if (error != null) throw new InvalidOperationException("The scenario threw.", error);
    }

    [Fact]
    public void SelectedItem_SetFromTheViewModel_SelectsOnceAndSettles()
    {
        CompletesWithoutLooping(() =>
        {
            var (vm, grid) = Bound();
            var first = vm.Employees[0];

            vm.SelectedEmployee = first;

            Assert.Same(first, vm.SelectedEmployee);
            Assert.Same(first, grid.SelectedItem);
            Assert.Equal(0, grid.SelectedIndex);
            Assert.Equal(0, grid.CurrentCell.Row);
            Assert.Equal(new object[] { first }, grid.Selection.SelectedItems.Cast<object>());
            Assert.Equal(new object?[] { first }, vm.SelectedEmployeeWrites); // no null echoed back
        });
    }

    [Fact]
    public void SelectedItem_SetFromTheViewModel_MovesFromOneItemToAnother()
    {
        CompletesWithoutLooping(() =>
        {
            var (vm, grid) = Bound();
            vm.SelectedEmployee = vm.Employees[0];

            vm.SelectedEmployee = vm.Employees[2];

            Assert.Same(vm.Employees[2], vm.SelectedEmployee);
            Assert.Same(vm.Employees[2], grid.SelectedItem);
            Assert.Equal(2, grid.SelectedIndex);
            Assert.Equal(2, grid.CurrentCell.Row);

            vm.SelectedEmployee = null;

            Assert.Null(grid.SelectedItem);
            Assert.Empty(grid.Selection.SelectedItems);
        });
    }

    [Fact]
    public void SelectedItem_SetFromTheViewModelInNoneMode_SettlesOnNull()
    {
        CompletesWithoutLooping(() =>
        {
            var (vm, grid) = Bound(KumikoSelectionMode.None);

            vm.SelectedEmployee = vm.Employees[1];

            Assert.Null(grid.SelectedItem);
            Assert.Null(vm.SelectedEmployee);
            Assert.Empty(grid.Selection.SelectedItems);
        });
    }

    [Fact]
    public void SelectedItem_SetFromTheViewModelToAnItemNotInTheGrid_SettlesOnNull()
    {
        CompletesWithoutLooping(() =>
        {
            var (vm, grid) = Bound();
            vm.SelectedEmployee = vm.Employees[0];

            vm.SelectedEmployee = new Person("Stranger");

            Assert.Null(grid.SelectedItem);
            Assert.Null(vm.SelectedEmployee);
            Assert.Empty(grid.Selection.SelectedItems);
        });
    }

    [Fact]
    public void SelectedItem_SelectedInTheGrid_ReachesTheViewModel()
    {
        CompletesWithoutLooping(() =>
        {
            var (vm, grid) = Bound();

            grid.Selection.HandleRowSelection(1, false, false);

            Assert.Same(vm.Employees[1], vm.SelectedEmployee);
            Assert.Same(vm.Employees[1], grid.SelectedItem);
        });
    }

    [Fact]
    public void SelectedIndex_SetFromTheViewModel_SelectsOnceAndSettles()
    {
        CompletesWithoutLooping(() =>
        {
            var (vm, grid) = Bound();
            grid.SetBinding(KumikoDataGrid.SelectedIndexProperty, new Binding(nameof(Vm.SelectedIndex), BindingMode.TwoWay));

            vm.SelectedIndex = 2;

            Assert.Equal(2, vm.SelectedIndex);
            Assert.Equal(2, grid.SelectedIndex);
            Assert.Same(vm.Employees[2], grid.SelectedItem);
            Assert.Same(vm.Employees[2], vm.SelectedEmployee);
            Assert.Equal(new object?[] { vm.Employees[2] }, vm.SelectedEmployeeWrites);
        });
    }

    [Fact]
    public void SelectedItems_BoundList_ChangedFromTheViewModel_Settles()
    {
        CompletesWithoutLooping(() =>
        {
            var (vm, grid) = Bound(KumikoSelectionMode.Multiple);
            grid.SetBinding(KumikoDataGrid.SelectedItemsProperty, new Binding(nameof(Vm.Selection), BindingMode.TwoWay));

            vm.Selection.Add(vm.Employees[0]);
            vm.Selection.Add(vm.Employees[2]);

            Assert.Same(vm.Selection, grid.SelectedItems);
            Assert.Equal(new object[] { vm.Employees[0], vm.Employees[2] }, vm.Selection);
            Assert.Equal(2, grid.Selection.SelectedItems.Count);
            Assert.NotNull(vm.SelectedEmployee);

            vm.SelectedEmployee = vm.Employees[1];

            Assert.Equal(new object[] { vm.Employees[1] }, vm.Selection);
            Assert.Same(vm.Employees[1], grid.SelectedItem);
        });
    }
}
