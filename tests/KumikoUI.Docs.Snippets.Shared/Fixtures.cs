// Shared by both documentation snippet projects (linked in, not a project of its own).
//
// The README's C# snippets say "assume these `using` directives and a grid named `kumiko`" and refer to a few
// everyday objects (`employees`, `vm`, ...). This file supplies them so the extracted snippets compile as the
// README describes. Nothing here ever runs: the snippet projects are compiled, never executed.
//
// Add a member only when a documented snippet needs one the README states as an assumption.

#if DOCS_MAUI
using HostGrid = KumikoUI.Maui.KumikoDataGrid;
#else
using HostGrid = KumikoUI.Uno.KumikoDataGrid;
#endif
using System.Collections.ObjectModel;
using System.Windows.Input;
using KumikoUI.Core.Models;

namespace DocsSnippets;

/// <summary>The row type the README uses in its examples.</summary>
public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LastName { get; set; } = "";
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public string Department { get; set; } = "";
    public decimal Salary { get; set; }
    public string Level { get; set; } = "";
    public string StatusIcon { get; set; } = "";
}

/// <summary>A view model with the commands the README binds to.</summary>
public class EmployeesViewModel
{
    public ICommand EditCommand { get; } = null!;
    public ICommand DeleteCommand { get; } = null!;
}

#if DOCS_MAUI
/// <summary>The application class <c>MauiProgram</c> in the README registers.</summary>
public class App : Microsoft.Maui.Controls.Application;
#endif

/// <summary>The objects the README's statement snippets assume are in scope.</summary>
public static class Fixtures
{
    public static HostGrid kumiko = null!;
    public static ObservableCollection<Employee> employees = null!;
    public static List<Employee> initialList = null!;
    public static Employee employee = null!;
    public static EmployeesViewModel vm = null!;
    public static KumikoColumn lastNameColumn = null!;
    public static KumikoColumn departmentColumn = null!;
}
