namespace KumikoUI.Core.Rendering;

/// <summary>
/// Platform-independent font definition.
/// </summary>
public class KumikoFont
{
    /// <summary>Font family name.</summary>
    public string Family { get; set; } = "Default";

    /// <summary>Font size in pixels.</summary>
    public float Size { get; set; } = 14f;

    /// <summary>Whether the font is bold.</summary>
    public bool IsBold { get; set; }

    /// <summary>Whether the font is italic.</summary>
    public bool IsItalic { get; set; }

    public KumikoFont() { }

    public KumikoFont(string family, float size, bool bold = false, bool italic = false)
    {
        Family = family;
        Size = size;
        IsBold = bold;
        IsItalic = italic;
    }
}
