---
mode: agent
description: Scaffold a new custom ICellRenderer for KumikoUI
---

# Add a Custom Cell Renderer

Create a new `ICellRenderer` implementation in `src/KumikoUI.Core/Rendering/` to display cell
values in a custom visual style. Read `.github/instructions/rendering.instructions.md` first: a renderer runs inside the
zero-allocation render loop.

## What to ask the user before generating code

1. What is the **class name** for the renderer? (for example a star rating renderer)
2. What **column type** will it be used with, or is it a `Template` column renderer?
3. What **configurable properties** does it expose? (min/max, colors, sizes, etc.)
4. Is it **interactive** (reacts to the pointer or animates)? Then it also implements `IInteractiveCellRenderer`.

---

## Files to create / modify

| Action | File |
|---|---|
| **Create** | `src/KumikoUI.Core/Rendering/{ClassName}.cs` |
| **Modify** (only to make it a built-in for a column type) | the `KumikoRenderer` constructor, which registers a renderer per `KumikoColumnType` |
| **Create** | `tests/KumikoUI.Core.Tests/Rendering/{ClassName}Tests.cs` |

A renderer an app writes for its own column needs only the class and `KumikoColumn.CustomCellRenderer`.

---

## What the grid has already done

`KumikoRenderer` fills the row and cell backgrounds (selection, alternate rows, per-cell `BackgroundColor`) and draws the
grid lines and the current cell border **before** it calls the renderer, so a renderer draws only the cell's content. It
passes everything through one `CellRenderContext`: `Bounds` (the padded cell area, stay inside it), `Value`,
`DisplayText` (already formatted and cached), `Column`, `Style`, `CellStyle`, `IsSelected`, `Paints` (the frame's
`PaintCache`), `TextPaint` (the resolved text paint for this cell), `TextAlignment` and `ImageResolver`.

---

## Template

```csharp
// src/KumikoUI.Core/Rendering/{ClassName}.cs
namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders {description of what it displays} in a data-grid cell.
/// </summary>
public sealed class {ClassName} : ICellRenderer
{
    // --- Configurable properties --------------------------------------------------

    /// <summary>Minimum value for the range. Default 0.</summary>
    public double Minimum { get; set; }

    /// <summary>Maximum value for the range. Default 100.</summary>
    public double Maximum { get; set; } = 100;

    // Reused for every cell: only the color varies, so drawing allocates nothing.
    private readonly KumikoPaint _paint = new();

    // --- ICellRenderer ------------------------------------------------------------

    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in CellRenderContext cell)
    {
        double number = cell.Value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal m => (double)m,
            _ => 0,
        };
        float fraction = Maximum > Minimum
            ? (float)Math.Clamp((number - Minimum) / (Maximum - Minimum), 0, 1)
            : 0f;

        // ── Custom drawing ────────────────────────────────────────────────────────
        // TODO: draw with ctx.FillRect / DrawRect / FillRoundRect / DrawText / FillPolygon ...
        //
        // RULES:
        //  • Use only IDrawingContext members: no SkiaSharp APIs.
        //  • Use KumikoColor, KumikoPaint and KumikoFont from KumikoUI.Core.Rendering.
        //  • Take colors from cell.Style or cell.Paints; never hardcode a color the theme should decide.
        //  • No allocation per call: no new KumikoPaint, no LINQ, no string building (use cell.DisplayText).
        //  • Stay within cell.Bounds; wrap a ClipRect in Save() / Restore().
        _paint.Color = cell.Style.AccentColor;
        ctx.FillRect(new KumikoRect(cell.Bounds.X, cell.Bounds.Y, cell.Bounds.Width * fraction, cell.Bounds.Height), _paint);
    }
}
```

Make a public renderer `sealed` and document every public member (doc warnings are errors). Look at `ProgressBarCellRenderer`
for a finished example, and at `IInteractiveCellRenderer` (`RedrawRequested`) for pointer-reactive ones.

---

## Attach to a column

### In XAML (Template column)

```xml
<core:KumikoColumn Header="My Column" PropertyName="Value"
                     ColumnType="Template" Width="140" IsReadOnly="True">
    <core:KumikoColumn.CustomCellRenderer>
        <render:{ClassName} Minimum="0" Maximum="100" />
    </core:KumikoColumn.CustomCellRenderer>
</core:KumikoColumn>
```

### In code

```csharp
var column = new KumikoColumn
{
    Header = "My Column",
    PropertyName = "Value",
    ColumnType = KumikoColumnType.Template,
    Width = 140,
    IsReadOnly = true,
    CustomCellRenderer = new {ClassName} { Minimum = 0, Maximum = 100 }
};
```

---

## Unit test skeleton

The test helpers are in `tests/KumikoUI.Core.Tests/TestHelpers/`: `RecordingDrawingContext` records every draw call, and the
`Render` extension on `ICellRenderer` (`CellRendererExtensions`) builds the `CellRenderContext` the grid would.

```csharp
// tests/KumikoUI.Core.Tests/Rendering/{ClassName}Tests.cs
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

public class {ClassName}Tests
{
    private static readonly KumikoRect Cell = new(10, 20, 100, 30);

    private static RecordingDrawingContext Render({ClassName} renderer, object? value)
    {
        var ctx = new RecordingDrawingContext();
        renderer.Render(ctx, Cell, value, value?.ToString() ?? "", new KumikoColumn(), new KumikoStyle(), isSelected: false);
        return ctx;
    }

    [Fact]
    public void Render_WithNullValue_DoesNotThrow()
    {
        var exception = Record.Exception(() => Render(new {ClassName}(), null));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    public void Render_FillsTheShareOfTheCellThatTheValueIs(double value, float expectedWidth)
    {
        var ctx = Render(new {ClassName}(), value);

        Assert.Equal(expectedWidth, ctx.Fills.Last().Rect.Width, precision: 3);
    }
}
```

`RecordingDrawingContext` records each `DrawOp` with its `Rect`, `Color`, `Text` and clip, in call order. A renderer's
allocation behavior is covered by `RenderAllocationTests`: if the renderer is part of the default grid, extend it.
