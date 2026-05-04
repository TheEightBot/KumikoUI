---
mode: agent
description: Add a unit test for a KumikoUI.Core class
---

# Add a Unit Test

Add xUnit tests to `tests/KumikoUI.Core.Tests/` for a class in `KumikoUI.Core`.

## Key rules (read before generating)

- Test framework: **xUnit** (`[Fact]` and `[Theory]` + `[InlineData]`). Never use NUnit or MSTest.
- One test class per source class (e.g., `GridRectTests` for `GridRect`).
- Test file name: `{ClassName}Tests.cs` — must match the existing naming convention.
- No SkiaSharp or MAUI dependencies — `KumikoUI.Core` only.
- Test project targets `net9.0`.
- Tests go in `tests/KumikoUI.Core.Tests/`.
- Existing test files for reference: `GridRectTests.cs`, `SelectionModelTests.cs`,
  `DataGridSourceTests.cs`, `InertialScrollerTests.cs`, `GridLayoutEngineTests.cs`.

## What to ask before generating

1. **Which class** should be tested? (Give full class name and namespace.)
2. **Which method(s) or property/ies** need coverage?
3. **Are there any edge cases** the user specifically wants covered?

---

## Template: `[Fact]` test

```csharp
[Fact]
public void {MethodName}_{Scenario}_Returns{ExpectedResult}()
{
    // Arrange
    var sut = new {ClassName}(/* constructor args */);

    // Act
    var result = sut.{MethodName}(/* args */);

    // Assert
    Assert.Equal(expected, result);
}
```

## Template: `[Theory]` data-driven test

```csharp
[Theory]
[InlineData(/* input1 */, /* expected1 */)]
[InlineData(/* input2 */, /* expected2 */)]
public void {MethodName}_{GeneralScenario}(/* paramType input */, /* resultType expected */)
{
    var sut = new {ClassName}();
    Assert.Equal(expected, sut.{MethodName}(input));
}
```

---

## Example: `DataGridSource` sorting test

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
        var items   = new List<Employee>
        {
            new Employee { Id = 3, Name = "Charlie" },
            new Employee { Id = 1, Name = "Alice" },
            new Employee { Id = 2, Name = "Bob" },
        };
        var col     = new DataGridColumn { PropertyName = "Name", ColumnType = DataGridColumnType.Text };
        var source  = MakeSource(items, [col]);

        source.ApplySort(col);   // ascending first

        Assert.Equal("Alice",   source.GetCellDisplayText(0, col));
        Assert.Equal("Bob",     source.GetCellDisplayText(1, col));
        Assert.Equal("Charlie", source.GetCellDisplayText(2, col));
    }
}
```

---

## `FakeDrawingContext` helper (if needed)

When testing renderers, create a no-op `IDrawingContext` test double:

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

---

## Running tests

```shell
dotnet test tests/KumikoUI.Core.Tests/
```

To run a single test class:
```shell
dotnet test tests/KumikoUI.Core.Tests/ --filter "FullyQualifiedName~{ClassName}Tests"
```
