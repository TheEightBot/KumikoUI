namespace KumikoUI.Core.Rendering;

/// <summary>
/// Platform-independent font definition.
/// </summary>
/// <remarks>
/// A value: two fonts with the same family, size, weight and slant are equal, and a style or paint holds its own
/// copy, so changing one place never changes another. Build a changed font with a <c>with</c> expression
/// (<c>style.CellFont = style.CellFont with { Size = 16f }</c>). Use <c>new KumikoFont()</c> (14 px, default family)
/// rather than <c>default</c>, which has size 0.
/// </remarks>
public readonly record struct KumikoFont
{
    /// <summary>Font family name. <c>"Default"</c> is the platform's default family.</summary>
    public string Family { get => field ?? "Default"; init; }

    /// <summary>Font size in pixels.</summary>
    public float Size { get; init; }

    /// <summary>Whether the font is bold.</summary>
    public bool IsBold { get; init; }

    /// <summary>Whether the font is italic.</summary>
    public bool IsItalic { get; init; }

    /// <summary>Creates the default font (default family, 14 px, regular).</summary>
    public KumikoFont()
    {
        Family = "Default";
        Size = 14f;
    }

    /// <summary>Creates a font.</summary>
    /// <param name="family">Font family name.</param>
    /// <param name="size">Font size in pixels.</param>
    /// <param name="bold">Whether the font is bold.</param>
    /// <param name="italic">Whether the font is italic.</param>
    public KumikoFont(string family, float size, bool bold = false, bool italic = false)
    {
        Family = family ?? "Default";
        Size = size;
        IsBold = bold;
        IsItalic = italic;
    }
}
