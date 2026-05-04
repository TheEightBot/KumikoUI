---
applyTo: "tests/**"
---

# KumikoUI — Unit Testing

## Rules

- Framework: **xUnit** (`[Fact]` and `[Theory]` + `[InlineData]`). Never NUnit or MSTest.
- One test class per source class — `GridRectTests` for `GridRect`, etc.
- File name: `{ClassName}Tests.cs`.
- No SkiaSharp or MAUI dependencies — `KumikoUI.Core` only.
- Tests target `net9.0`.
- Run: `dotnet test tests/KumikoUI.Core.Tests/`
- Filter to one class: `dotnet test --filter "FullyQualifiedName~{ClassName}Tests"`

---

## `[Fact]` — single scenario

```csharp
[Fact]
public void {MethodName}_{Scenario}_Returns{Expected}()
{
    // Arrange
    var sut = new {ClassName}();

    // Act
    var result = sut.{MethodName}(args);

    // Assert
    Assert.Equal(expected, result);
}
```

## `[Theory]` — data-driven

```csharp
[Theory]
[InlineData(/* input */, /* expected */)]
[InlineData(/* input */, /* expected */)]
public void {MethodName}_{Scenario}({Type} input, {Type} expected)
{
    var sut = new {ClassName}();
    Assert.Equal(expected, sut.{MethodName}(input));
}
```

---

## Example: `DataGridSource` sorting

```csharp
public class DataGridSourceSortingTests
{
    private static DataGridSource MakeSource(IList items, List<DataGridColumn> columns)
    {
        var source = new DataGridSource();
        foreach (var col in columns) source.Columns.Add(col);
        source.ItemsSource = items;
        return source;
    }

    [Fact]
    public void ApplySort_Ascending_OrdersRowsCorrectly()
    {
        var items  = new List<Employee>
        {
            new() { Id = 3, Name = "Charlie" },
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" },
        };
        var col    = new DataGridColumn { PropertyName = "Name", ColumnType = DataGridColumnType.Text };
        var source = MakeSource(items, [col]);

        source.ApplySort(col);

        Assert.Equal("Alice",   source.GetCellDisplayText(0, col));
        Assert.Equal("Bob",     source.GetCellDisplayText(1, col));
        Assert.Equal("Charlie", source.GetCellDisplayText(2, col));
    }
}
```

---

## `FakeDrawingContext` — test double for renderers

When testing `ICellRenderer` or `DrawnComponent`, create a no-op `IDrawingContext`:

```csharp
// tests/KumikoUI.Core.Tests/TestHelpers/FakeDrawingContext.cs
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.TestHelpers;

internal sealed class FakeDrawingContext : IDrawingContext
{
    public void DrawRect(GridRect r, GridPaint p) { }
    public void FillRect(GridRect r, GridPaint p) { }
    public void DrawRoundRect(GridRect r, float cr, GridPaint p) { }
    public void FillRoundRect(GridRect r, float cr, GridPaint p) { }
    public void DrawLine(float x1, float y1, float x2, float y2, GridPaint p) { }
    public void DrawText(string t, float x, float y, GridPaint p) { }
    public void DrawTextInRect(string t, GridRect r, GridPaint p,
        GridTextAlignment h = GridTextAlignment.Left,
        GridVerticalAlignment v = GridVerticalAlignment.Center,
        bool clip = true) { }
    public GridSize MeasureText(string t, GridPaint p) => new GridSize(t.Length * 8, 14);
    public GridFontMetrics GetFontMetrics(GridPaint p) => new GridFontMetrics(14, 0, 2);
    public void ClipRect(GridRect r) { }
    public void Save() { }
    public void Restore() { }
    public void Translate(float dx, float dy) { }
    public void DrawImage(object image, GridRect dest) { }
    public void Dispose() { }
}
```
