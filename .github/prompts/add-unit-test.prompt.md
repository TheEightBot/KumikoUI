---
mode: agent
description: Add a unit test for a KumikoUI.Core class
---

# Add a Unit Test

Add xUnit tests to `tests/KumikoUI.Core.Tests/` for a class in `KumikoUI.Core`. Read
`.github/instructions/testing.instructions.md` first: it lists the test projects and the helpers.

## Key rules (read before generating)

- Test framework: **xUnit** (`[Fact]` and `[Theory]` + `[InlineData]`). Never use NUnit or MSTest. `Xunit` is a global using.
- One test class per source class or behavior (for example `KumikoRectTests` for `KumikoRect`).
- Test file name: `{ClassName}Tests.cs`, in the folder that mirrors the source (`Input/`, `Editing/`, `Rendering/`,
  `Components/`, `Data/`; Models and Layout tests are at the project root).
- Core tests see Core's `internal` members; do not make a member public to test it.
- Test project targets `net10.0`. Tests for the SkiaSharp backend go in `tests/KumikoUI.SkiaSharp.Tests`, for the hosts'
  platform-independent logic in `tests/KumikoUI.Maui.Tests` and `tests/KumikoUI.Uno.Tests`.
- Existing test files for reference: `KumikoRectTests.cs`, `KumikoSelectionModelTests.cs`,
  `KumikoSourceTests.cs`, `InertialScrollerTests.cs`, `KumikoLayoutEngineTests.cs`.
- Timing tests carry `[Trait("Category", "Performance")]`; a test that needs a real SkiaSharp surface belongs in the SkiaSharp tests.

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

The sorting API is `SetSort`, `ToggleSort` and `ClearSort`; columns and items are set with `SetColumns` and `SetItems`.

```csharp
public class KumikoSourceSortingExampleTests
{
    private sealed class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    [Fact]
    public void SetSort_Ascending_OrdersRowsCorrectly()
    {
        var col = new KumikoColumn { PropertyName = "Name", ColumnType = KumikoColumnType.Text };
        var source = new KumikoSource();
        source.SetColumns(new[] { col });
        source.SetItems(new List<Employee>
        {
            new() { Id = 3, Name = "Charlie" },
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" },
        });

        source.SetSort(col, KumikoSortDirection.Ascending);

        Assert.Equal("Alice",   source.GetCellDisplayText(0, col));
        Assert.Equal("Bob",     source.GetCellDisplayText(1, col));
        Assert.Equal("Charlie", source.GetCellDisplayText(2, col));
    }
}
```

---

## Test doubles for rendering (when needed)

Do not write a new `IDrawingContext` stub: use the helpers in `tests/KumikoUI.Core.Tests/TestHelpers/`.

| Helper | Use |
|---|---|
| `RecordingDrawingContext` | Records every draw call as a `DrawOp` (rect, color, text, clip) in call order; `ctx.Texts`, `ctx.Fills`, `ctx.SingleText("P00")` |
| `NoOpDrawingContext` | Draws and records nothing; for allocation tests |
| `RenderHarness` | A grid of people wired to a `KumikoRenderer`; `RenderHarness.Create(rows: 20)` then `h.Render()` |
| `CellRendererExtensions` | Calls an `ICellRenderer` with a context built like the grid's |

```csharp
var h = RenderHarness.Create(width: 300);
h.Column("Name").FreezeMode = ColumnFreezeMode.Left;

var ctx = h.Render();

Assert.Equal(new KumikoRect(0, 0, 150, 400), ctx.SingleText("P00").Clip);
```

Pointer and keyboard behavior is tested with `InputGrid` (`Input/`), drawn components with `ComponentInput` (`Components/`).

---

## Running tests

```shell
dotnet test tests/KumikoUI.Core.Tests -c Release
```

To run a single test class:
```shell
dotnet test tests/KumikoUI.Core.Tests -c Release --filter "FullyQualifiedName~{ClassName}Tests"
```
