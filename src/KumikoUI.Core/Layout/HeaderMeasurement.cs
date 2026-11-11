namespace KumikoUI.Core.Layout;

using KumikoUI.Core.Rendering;

/// <summary>
/// One column's measured header text width, with the text and font it was measured for (#182). Layout measures a
/// header again only when its text, its font instance or one of that font's properties changed, so a frame drawn
/// with unchanged headers measures nothing and allocates nothing.
/// </summary>
internal sealed class HeaderMeasurement
{
    private string? _text;
    private KumikoFont? _font;
    private string? _family;
    private float _size;
    private bool _bold;
    private bool _italic;

    /// <summary>Whether a measurement has been stored.</summary>
    public bool HasValue { get; private set; }

    /// <summary>The measured text width, in pixels.</summary>
    public float TextWidth { get; private set; }

    /// <summary>Whether the stored measurement is for <paramref name="text"/> drawn in <paramref name="font"/> as it is now.</summary>
    public bool IsCurrentFor(string text, KumikoFont font) =>
        HasValue
        && string.Equals(_text, text, StringComparison.Ordinal)
        && ReferenceEquals(_font, font)
        && string.Equals(_family, font.Family, StringComparison.Ordinal)
        && _size.Equals(font.Size) // exact: cache change detection
        && _bold == font.IsBold
        && _italic == font.IsItalic;

    /// <summary>Stores the width of <paramref name="text"/> measured in <paramref name="font"/>.</summary>
    public void Store(string text, KumikoFont font, float textWidth)
    {
        _text = text;
        _font = font;
        _family = font.Family;
        _size = font.Size;
        _bold = font.IsBold;
        _italic = font.IsItalic;
        TextWidth = textWidth;
        HasValue = true;
    }
}
