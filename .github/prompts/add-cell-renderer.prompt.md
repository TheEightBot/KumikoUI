---
mode: agent
description: Scaffold a new custom ICellRenderer for KumikoUI
---

# Add a Custom Cell Renderer

Create a new `ICellRenderer` implementation in `src/KumikoUI.Core/Rendering/` to display cell
values in a custom visual style.

## What to ask the user before generating code

1. What is the **class name** for the renderer? (e.g. `StarRatingCellRenderer`)
2. What **column type** will it be used with, or is it a `Template` column renderer?
3. What **configurable properties** does it expose? (min/max, colors, sizes, etc.)

---

## Files to create / modify

| Action | File |
|---|---|
| **Create** | `src/KumikoUI.Core/Rendering/{ClassName}.cs` |
| **Modify** (optional, if new column type) | `src/KumikoUI.Core/Models/DataGridColumn.cs` — add enum value |
| **Create** | `tests/KumikoUI.Core.Tests/{ClassName}Tests.cs` |

---

## Template

```csharp
// src/KumikoUI.Core/Rendering/{ClassName}.cs
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders {description of what it displays} in a data-grid cell.
/// </summary>
public class {ClassName} : ICellRenderer
{
    // --- Configurable properties --------------------------------------------------

    /// <summary>Minimum value for the range. Default 0.</summary>
    public float Minimum { get; set; } = 0f;

    /// <summary>Maximum value for the range. Default 100.</summary>
    public float Maximum { get; set; } = 100f;

    // --- ICellRenderer ------------------------------------------------------------

    /// <inheritdoc />
    public void Render(
        IDrawingContext ctx,
        GridRect cellRect,
        object? value,
        string displayText,
        DataGridColumn column,
        DataGridStyle style,
        bool isSelected,
        CellStyle? cellStyle = null)
    {
        // ── Background ────────────────────────────────────────────────────────────
        var bg = cellStyle?.BackgroundColor
            ?? (isSelected ? style.SelectionColor : style.CellBackgroundColor);

        ctx.FillRect(cellRect, new GridPaint { Color = bg, Style = PaintStyle.Fill });

        // ── Value parsing ─────────────────────────────────────────────────────────
        float numericValue = value switch
        {
            float f  => f,
            double d => (float)d,
            int i    => i,
            _        => float.TryParse(displayText, out float parsed) ? parsed : 0f
        };

        float fraction = Maximum > Minimum
            ? Math.Clamp((numericValue - Minimum) / (Maximum - Minimum), 0f, 1f)
            : 0f;

        // ── Custom drawing ────────────────────────────────────────────────────────
        // TODO: implement visual representation using ctx.DrawRect / ctx.FillRect /
        // ctx.DrawText / ctx.DrawRoundRect etc.
        //
        // RULES:
        //  • Use only IDrawingContext methods — no SkiaSharp APIs.
        //  • Use GridColor, GridPaint, GridFont from KumikoUI.Core.Rendering.
        //  • Never hardcode colors; fall back to DataGridStyle properties.
        //  • Stay within cellRect bounds.
    }
}
```

---

## Attach to a column

### In XAML (Template column)

```xml
<core:DataGridColumn Header="My Column" PropertyName="Value"
                     ColumnType="Template" Width="140" IsReadOnly="True">
    <core:DataGridColumn.CustomCellRenderer>
        <render:{ClassName} Minimum="0" Maximum="100" />
    </core:DataGridColumn.CustomCellRenderer>
</core:DataGridColumn>
```

### In code

```csharp
var column = new DataGridColumn
{
    Header = "My Column",
    PropertyName = "Value",
    ColumnType = DataGridColumnType.Template,
    Width = 140,
    IsReadOnly = true,
    CustomCellRenderer = new {ClassName} { Minimum = 0, Maximum = 100 }
};
```

---

## Unit test skeleton

```csharp
// tests/KumikoUI.Core.Tests/{ClassName}Tests.cs
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

public class {ClassName}Tests
{
    private static DataGridColumn MakeColumn() =>
        new DataGridColumn { PropertyName = "Value", ColumnType = DataGridColumnType.Template };

    [Fact]
    public void Render_WithNullValue_DoesNotThrow()
    {
        var renderer = new {ClassName}();
        var style    = DataGridTheme.Create(DataGridThemeMode.Light);
        var ctx      = new FakeDrawingContext();
        renderer.Render(ctx, new GridRect(0, 0, 100, 30), null, "", MakeColumn(), style, false);
        // No assertion needed — simply must not throw.
    }

    [Theory]
    [InlineData(0f,   0f)]
    [InlineData(50f,  0.5f)]
    [InlineData(100f, 1f)]
    public void Fraction_CalculatesCorrectly(float value, float expected)
    {
        // Unit test the fraction math independently of drawing.
        float min = 0f, max = 100f;
        float fraction = max > min ? Math.Clamp((value - min) / (max - min), 0f, 1f) : 0f;
        Assert.Equal(expected, fraction, precision: 4);
    }
}
```

> `FakeDrawingContext` is a no-op `IDrawingContext` test double. If it does not yet exist,
> create `tests/KumikoUI.Core.Tests/TestHelpers/FakeDrawingContext.cs` implementing all
> interface members as empty stubs.
