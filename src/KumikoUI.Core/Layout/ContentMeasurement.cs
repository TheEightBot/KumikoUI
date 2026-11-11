namespace KumikoUI.Core.Layout;

using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

/// <summary>
/// One Auto column's widest sampled cell text, with the source, the source's content version and the font it was
/// measured for (#24). Layout samples the cells again only when the source's rows or cell values changed, the column
/// moved to another source, or the cell font changed; scrolling alone measures nothing, so an Auto column keeps its
/// width while the grid scrolls.
/// </summary>
internal sealed class ContentMeasurement
{
    private KumikoSource? _source;
    private int _version;
    private KumikoFont? _font;
    private string? _family;
    private float _size;
    private bool _bold;
    private bool _italic;

    /// <summary>Whether a measurement has been stored.</summary>
    public bool HasValue { get; private set; }

    /// <summary>The widest sampled cell text, in pixels; 0 when every sampled cell was empty.</summary>
    public float TextWidth { get; private set; }

    /// <summary>
    /// Whether the stored measurement is for <paramref name="source"/> at its current content version, measured in
    /// <paramref name="font"/> as it is now.
    /// </summary>
    public bool IsCurrentFor(KumikoSource source, KumikoFont font) =>
        HasValue
        && ReferenceEquals(_source, source)
        && _version == source.ContentVersion
        && ReferenceEquals(_font, font)
        && string.Equals(_family, font.Family, StringComparison.Ordinal)
        && _size.Equals(font.Size) // exact: cache change detection
        && _bold == font.IsBold
        && _italic == font.IsItalic;

    /// <summary>Stores the widest sampled cell text of <paramref name="source"/> as it is now, measured in <paramref name="font"/>.</summary>
    public void Store(KumikoSource source, KumikoFont font, float textWidth)
    {
        _source = source;
        _version = source.ContentVersion;
        _font = font;
        _family = font.Family;
        _size = font.Size;
        _bold = font.IsBold;
        _italic = font.IsItalic;
        TextWidth = textWidth;
        HasValue = true;
    }

    /// <summary>Forgets the measurement, so the next layout pass samples the cells again.</summary>
    public void Clear()
    {
        _source = null;
        _font = null;
        HasValue = false;
    }
}
