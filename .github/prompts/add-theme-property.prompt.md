---
mode: agent
description: Add a new KumikoStyle visual property and wire it into the theme system
---

# Add a KumikoStyle Theme Property

Adds a new visual property to `KumikoStyle` and wires it through all three built-in
themes (Light, Dark, HighContrast) and the renderer that uses it.

## What to ask the user before generating code

1. What is the **property name**? (e.g. `FrozenColumnSeparatorColor`, `GroupHeaderFontSize`)
2. What is its **type**? (`KumikoColor`, `KumikoFont`, `float`, `bool`, etc.)
3. What is the **Light theme default value**?
4. What is the **Dark theme value**?
5. What is the **HighContrast theme value**?
6. Which part of the renderer will **consume** this property?

---

## Files to modify

| File | Change |
|---|---|
| `src/KumikoUI.Core/Models/KumikoStyle.cs` | Add property to `KumikoStyle` |
| `src/KumikoUI.Core/Models/KumikoTheme.cs` | Set value in Dark and HighContrast themes |
| `src/KumikoUI.Core/KumikoRenderer.cs` | Use the property in the rendering path |
| `tests/KumikoUI.Core.Tests/KumikoThemeTests.cs` | Add two `[Fact]` tests |

---

## Step 1 — `KumikoStyle` property

In `src/KumikoUI.Core/Models/KumikoStyle.cs`, add the property inside `KumikoStyle`:

```csharp
/// <summary>
/// {Description of what this property controls.}
/// Default (Light): {lightValue}.
/// </summary>
public {Type} {PropertyName} { get; set; } = {lightDefault};
```

---

## Step 2 — `KumikoTheme` — Dark and HighContrast

In `src/KumikoUI.Core/Models/KumikoTheme.cs`:

```csharp
// Inside CreateDark():
{PropertyName} = {darkValue},

// Inside CreateHighContrast():
{PropertyName} = {highContrastValue},
```

---

## Step 3 — Renderer usage

In `src/KumikoUI.Core/KumikoRenderer.cs`, use `style.{PropertyName}` where
this visual feature is drawn. Typically inside a `Draw*` helper method or inline
in the render loop.

Example (for a color property):
```csharp
var separatorPaint = new KumikoPaint
{
    Color = style.{PropertyName},
    Style = PaintStyle.Stroke,
    StrokeWidth = 1f
};
ctx.DrawLine(x, top, x, bottom, separatorPaint);
```

---

## Step 4 — Unit tests

In `tests/KumikoUI.Core.Tests/KumikoThemeTests.cs`:

```csharp
[Fact]
public void Dark_{PropertyName}_HasExpectedValue()
{
    var theme = KumikoTheme.Create(KumikoThemeMode.Dark);
    Assert.Equal({darkValue}, theme.{PropertyName});
}

[Fact]
public void HighContrast_{PropertyName}_HasExpectedValue()
{
    var theme = KumikoTheme.Create(KumikoThemeMode.HighContrast);
    Assert.Equal({highContrastValue}, theme.{PropertyName});
}
```

---

## Rules

- Property type must be from `KumikoUI.Core.Rendering` or a BCL type.
  Valid types: `KumikoColor`, `KumikoFont`, `float`, `int`, `bool`, `string?`.
- Never use `Microsoft.Maui.Graphics.Color` or `SkiaSharp.SKColor` in Core models.
- Always set a non-null/non-default value in all three theme factories.
- Default (property initializer) reflects the Light theme value.
- Run `dotnet test tests/KumikoUI.Core.Tests/` after changes.
