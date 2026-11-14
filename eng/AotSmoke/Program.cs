using KumikoUI.Core;
using KumikoUI.Core.Data;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.SkiaSharp;
using SkiaSharp;

// Exercises the grid engine end to end under Native AOT: columns resolved through generated accessors, sorting,
// filtering, grouping, summaries, selection and one frame rendered to an SKSurface. Any failed check exits non-zero.

const int RowCount = 200;
const int Width = 640;
const int Height = 360;

var items = Enumerable.Range(0, RowCount).Select(i => new Employee
{
    Name = $"Employee {i:000}",
    Department = (i % 4) switch { 0 => "Sales", 1 => "Support", 2 => "Engineering", _ => "Finance" },
    Salary = 40_000 + (i * 37 % 60) * 1_000,
    Hired = new DateTime(2020, 1, 1).AddDays(i),
    Active = i % 3 != 0,
}).ToList();

var name = new KumikoColumn { Header = "Name", PropertyName = nameof(Employee.Name), Width = 140 };
var department = new KumikoColumn { Header = "Department", PropertyName = nameof(Employee.Department), Width = 120 };
var salary = new KumikoColumn
{
    Header = "Salary", PropertyName = nameof(Employee.Salary), Width = 100,
    ColumnType = KumikoColumnType.Numeric, Format = "C0",
};
var hired = new KumikoColumn
{
    Header = "Hired", PropertyName = nameof(Employee.Hired), Width = 100,
    ColumnType = KumikoColumnType.Date, Format = "yyyy-MM-dd",
};
var active = new KumikoColumn
{
    Header = "Active", PropertyName = nameof(Employee.Active), Width = 70, ColumnType = KumikoColumnType.Boolean,
};
// A column with an explicit delegate instead of a property name: needs no accessor at all.
var initials = new KumikoColumn
{
    Header = "Initials", Width = 80,
    ValueAccessor = item => ((Employee)item).Name[..1],
};

var source = new KumikoSource();
source.SetColumns(new[] { name, department, salary, hired, active, initials });
source.SetItems(items);

Check(source.RowCount == RowCount, $"expected {RowCount} rows, got {source.RowCount}");
Check(Equals(source.GetCellValue(5, name), "Employee 005"), "generated accessor read the Name property");
Check(Equals(source.GetCellValue(5, initials), "E"), "column delegate read the initials");

// Sort descending by salary: the first row holds the maximum.
source.SetSort(salary, KumikoSortDirection.Descending);
var maxSalary = items.Max(e => e.Salary);
Check(Equals(source.GetCellValue(0, salary), maxSalary), "descending sort puts the highest salary first");

// Filter to one department.
source.SetColumnFilter(department, new FilterDescription(department)
{
    Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Equals, Value = "Engineering" },
});
Check(source.RowCount == items.Count(e => e.Department == "Engineering"), "text filter keeps only Engineering rows");

// Group, summarize and render, with a selection.
source.AddGroupDescription(new GroupDescription(nameof(Employee.Department)));
source.AddTableSummaryRow(new TableSummaryRow
{
    Name = "Total", Title = "Total", Position = SummaryPosition.Bottom,
    Columns = { new SummaryColumnDescription(nameof(Employee.Salary), SummaryType.Sum) },
});
Check(source.IsGroupingActive, "grouping is active");

// Save the layout as JSON (System.Text.Json source generation) and restore it onto a fresh source.
source.SetDisplayIndex(active, 0);
string layoutJson = KumikoLayoutState.Capture(source).ToJson();
var restored = new KumikoSource();
restored.SetColumns(new[]
{
    new KumikoColumn { Header = "Name", PropertyName = nameof(Employee.Name) },
    new KumikoColumn { Header = "Department", PropertyName = nameof(Employee.Department) },
    new KumikoColumn { Header = "Salary", PropertyName = nameof(Employee.Salary), ColumnType = KumikoColumnType.Numeric },
    new KumikoColumn { Header = "Hired", PropertyName = nameof(Employee.Hired), ColumnType = KumikoColumnType.Date },
    new KumikoColumn { Header = "Active", PropertyName = nameof(Employee.Active), ColumnType = KumikoColumnType.Boolean },
});
restored.SetItems(items);
KumikoLayoutState.FromJson(layoutJson).ApplyTo(restored);
Check(restored.Columns[0].PropertyName == nameof(Employee.Active), "the restored layout keeps the column order");
Check(restored.RowCount == source.RowCount, "the restored layout keeps the sort, filter and grouping");
Check(KumikoLayoutState.Capture(restored).ToJson() == layoutJson, "the layout JSON round-trips");

var style = new KumikoStyle();
var scroll = new ScrollState { ViewportWidth = Width, ViewportHeight = Height };
var selection = new KumikoSelectionModel(source);
selection.HandleRowSelection(1, shift: false, control: false);

using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
Check(surface is not null, "SKSurface could be created (SkiaSharp native library loaded)");
surface!.Canvas.Clear(SKColors.White);
using (var ctx = new SkiaDrawingContext(surface.Canvas))
{
    new KumikoRenderer().Render(ctx, source, scroll, selection, style);
}

// The frame must contain more than the white background.
using var snapshot = surface.Snapshot();
using var bitmap = SKBitmap.FromImage(snapshot);
var distinct = new HashSet<uint>();
for (var y = 0; y < Height && distinct.Count < 8; y += 3)
{
    for (var x = 0; x < Width && distinct.Count < 8; x += 3)
    {
        distinct.Add((uint)bitmap.GetPixel(x, y));
    }
}

Check(distinct.Count > 1, "the rendered frame is not blank");

Console.WriteLine($"AOT smoke OK: {source.RowCount} visible rows, {distinct.Count}+ distinct colors in the frame.");
return 0;

static void Check(bool condition, string what)
{
    if (!condition)
    {
        Console.Error.WriteLine($"AOT smoke FAILED: {what}");
        Environment.Exit(1);
    }
}

/// <summary>A row type with generated accessors, as an app on Native AOT would declare it.</summary>
[KumikoGridItem]
internal sealed class Employee
{
    public string Name { get; set; } = "";

    public string Department { get; set; } = "";

    public int Salary { get; set; }

    public DateTime Hired { get; set; }

    public bool Active { get; set; }
}
