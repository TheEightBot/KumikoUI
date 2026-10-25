---
applyTo: "tests/**"
---

# KumikoUI — Unit Testing

## Rules

- Framework: **xUnit** (`[Fact]` and `[Theory]` + `[InlineData]`). Never NUnit or MSTest.
- One test class per source class — `GridRectTests` for `GridRect`, etc.
- File name: `{ClassName}Tests.cs`.
- No SkiaSharp or MAUI dependencies — `KumikoUI.Core` only.
- Tests target `net10.0`.
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
    private static DataGridSource MakeSource(IList items, List<GridColumn> columns)
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
        var col    = new GridColumn { PropertyName = "Name", ColumnType = GridColumnType.Text };
        var source = MakeSource(items, [col]);

        source.ApplySort(col);

        Assert.Equal("Alice",   source.GetCellDisplayText(0, col));
        Assert.Equal("Bob",     source.GetCellDisplayText(1, col));
        Assert.Equal("Charlie", source.GetCellDisplayText(2, col));
    }
}
```

---

## `RecordingDrawingContext` — test double for rendering

Rendering tests run without SkiaSharp against `tests/KumikoUI.Core.Tests/TestHelpers/RecordingDrawingContext.cs`.
It records every `IDrawingContext` call as a `DrawOp` in call order (so index = z-order), in viewport
coordinates with the active clip, and measures text monospace (8 px per character, 14 px tall).
`DrawOp.Visible` is the part of an op its clip lets reach the screen.

For `DataGridRenderer`, use `RenderHarness` (same folder): it builds a four-column grid of people, gives
every `GridColor` on the style a distinct value (so a recorded colour identifies its style role) and
renders into a fresh recording context:

```csharp
var h = RenderHarness.Create(width: 300);
h.Column("Name").FreezeMode = ColumnFreezeMode.Left;
h.Scroll.OffsetX = 50;

var ctx = h.Render();

var cell = ctx.SingleText("P00");
Assert.Equal(new GridRect(0, 0, 150, 400), cell.Clip);
```

Known renderer defects are pinned as `[Fact(Skip = "<finding>, fixed by #<issue>: …")]` tests in
`tests/KumikoUI.Core.Tests/Rendering/`; the fix removes the `Skip`.
