---
mode: agent
description: Add a new KumikoStyle visual property and wire it into the theme system
---

# Add a KumikoStyle Theme Property

Adds a new visual property to `KumikoStyle` and wires it through the three built-in
themes (Light, Dark, HighContrast), the paint cache and the renderer that uses it. A property is public API: it needs an
XML doc comment, a CHANGELOG entry, and (when users should set it from XAML) an entry in the README's "KumikoStyle reference".

## What to ask the user before generating code

1. What is the **property name**? (for example a separator color or a group header font size)
2. What is its **type**? (`KumikoColor`, `KumikoFont`, `float`, `bool`, etc.)
3. What is the **Light theme default value**?
4. What is the **Dark theme value**?
5. What is the **HighContrast theme value**?
6. Which part of the renderer will **consume** this property?

---

## Files to modify

| File | Change |
|---|---|
| `src/KumikoUI.Core/Models/KumikoStyle.cs` | Add the property to `KumikoStyle` |
| `src/KumikoUI.Core/Models/KumikoTheme.cs` | Set the value in `CreateDark()` and `CreateHighContrast()` |
| `src/KumikoUI.Core/Rendering/PaintCache.cs` | For a color or font a paint is built from: a paint property, set in `Update` |
| `src/KumikoUI.Core/KumikoRenderer.cs` | Use the paint (or the value) where the feature is drawn |
| `tests/KumikoUI.Core.Tests/KumikoThemeTests.cs` | Tests for the Dark and HighContrast values |
| `tests/KumikoUI.Core.Tests/Rendering/PaintCacheTests.cs` | A test that the paint takes its value from the style field |
| `README.md`, `CHANGELOG.md` | The "KumikoStyle reference" table and an Added entry |

If hosts should expose it as a bindable property, it goes through `KumikoStyleOptions` (nullable: null keeps the theme's
value) and the properties in both hosts; that is a separate, larger change.

---

## Step 1: `KumikoStyle` property

In `src/KumikoUI.Core/Models/KumikoStyle.cs`, add the property with the same setter the others use, so that changing it
raises `KumikoStyle.Version` and `PaintCache.Update` rebuilds:

```csharp
/// <summary>
/// {Description of what this property controls.}
/// Default (Light): {lightValue}.
/// </summary>
public {Type} {PropertyName} { get; set => Set(ref field, value); } = {lightDefault};
```

---

## Step 2: `KumikoTheme` — Dark and HighContrast

In `src/KumikoUI.Core/Models/KumikoTheme.cs`, inside the object initializers:

```csharp
// Inside CreateDark():
{PropertyName} = {darkValue},

// Inside CreateHighContrast():
{PropertyName} = {highContrastValue},
```

The Light theme is the `KumikoStyle` default (`CreateLight()` returns `new()`).

---

## Step 3: `PaintCache` and renderer usage

In `src/KumikoUI.Core/Rendering/PaintCache.cs`, add a paint property (`public KumikoPaint {Name} { get; private set; } = new();`)
and build it in `Update(KumikoStyle style)`, which runs every frame and rebuilds only when the style changed. Then, in
`src/KumikoUI.Core/KumikoRenderer.cs`, use it where this visual feature is drawn, typically inside a `Draw*` method:

```csharp
ctx.DrawLine(x, top, x, bottom, _paintCache.{Name});
```

The render loop allocates nothing: never write `new KumikoPaint` in a draw method. A value that varies per cell or row
(a resolver result) uses a paint the renderer reuses and sets the color of before the draw.

---

## Step 4: Unit tests

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

In `PaintCacheTests`, the render harness gives every `KumikoColor` of its style a distinct value, so a paint wired to the
wrong field fails the test that compares the paint's color with the style field. A renderer test through `RenderHarness`
(`h.Render()` and the recording context) shows that the feature draws with the new color.

---

## Rules

- Property type must be from `KumikoUI.Core.Rendering` or a BCL type.
  Valid types: `KumikoColor`, `KumikoFont`, `float`, `int`, `bool`, `string?`.
- Never use `Microsoft.Maui.Graphics.Color` or `SkiaSharp.SKColor` in Core models.
- Always set a value in all three theme factories (a nullable property may stay null).
- The property initializer is the Light theme value.
- Run `dotnet test tests/KumikoUI.Core.Tests -c Release` after changes.
