---
mode: agent
description: Add a unit test for a KumikoUI.Core class
---

# Add a Unit Test

Add xUnit tests to `tests/KumikoUI.Core.Tests/` for a class in `KumikoUI.Core`.

## Key rules (read before generating)

- Test framework: **xUnit** (`[Fact]` and `[Theory]` + `[InlineData]`). Never use NUnit or MSTest.
- One test class per source class (e.g., `KumikoRectTests` for `KumikoRect`).
- Test file name: `{ClassName}Tests.cs` — must match the existing naming convention.
- No SkiaSharp or MAUI dependencies — `KumikoUI.Core` only.
- Test project targets `net10.0`.
- Tests go in `tests/KumikoUI.Core.Tests/`.
- Existing test files for reference: `KumikoRectTests.cs`, `KumikoSelectionModelTests.cs`,
  `KumikoSourceTests.cs`, `InertialScrollerTests.cs`, `KumikoLayoutEngineTests.cs`.

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

## Example: `KumikoSource` sorting test

```csharp
public class DataGridSourceSortingTests
{
    private static KumikoSource MakeSource(IList items, List<KumikoColumn> columns)
    {
        var source = new KumikoSource();
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
        var col     = new KumikoColumn { PropertyName = "Name", ColumnType = KumikoColumnType.Text };
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
    public void DrawRect(KumikoRect r, KumikoPaint p) { }
    public void FillRect(KumikoRect r, KumikoPaint p) { }
    public void DrawRoundRect(KumikoRect r, float cr, KumikoPaint p) { }
    public void FillRoundRect(KumikoRect r, float cr, KumikoPaint p) { }
    public void DrawLine(float x1, float y1, float x2, float y2, KumikoPaint p) { }
    public void DrawText(string t, float x, float y, KumikoPaint p) { }
    public void DrawTextInRect(string t, KumikoRect r, KumikoPaint p,
        KumikoTextAlignment h = KumikoTextAlignment.Left,
        KumikoVerticalAlignment v = KumikoVerticalAlignment.Center,
        bool clip = true) { }
    public KumikoSize MeasureText(string t, KumikoPaint p) => new KumikoSize(t.Length * 8, 14);
    public KumikoFontMetrics GetFontMetrics(KumikoPaint p) => new KumikoFontMetrics(14, 0, 2);
    public void ClipRect(KumikoRect r) { }
    public void Save() { }
    public void Restore() { }
    public void Translate(float dx, float dy) { }
    public void DrawImage(object image, KumikoRect dest) { }
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
