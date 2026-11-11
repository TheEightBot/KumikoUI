namespace KumikoUI.SkiaSharp;

/// <summary>
/// How one line of text splits into runs drawn with different typefaces: the requested (primary) typeface where
/// it has the glyphs, and fallback typefaces for the rest. Immutable and cached with the text's measurement in
/// <see cref="SkiaResourceCache"/>, so drawing it again allocates nothing.
/// </summary>
/// <remarks>
/// Every run ends on a text element boundary, so an emoji sequence, a flag or a letter with combining marks is
/// never split between two typefaces. A face is an index into the cache's fallback table, or <see cref="PrimaryFace"/>.
/// </remarks>
internal sealed class FontRuns
{
    /// <summary>The face index of the requested typeface.</summary>
    public const int PrimaryFace = -1;

    private readonly int[] _ends;
    private readonly int[] _faces;
    private readonly float[] _widths;

    /// <summary>The whole text is drawn with the requested typeface (the common case): no runs to walk.</summary>
    public static FontRuns Primary { get; } = new([], [], [], 0);

    public FontRuns(int[] ends, int[] faces, float[] widths, long generation)
    {
        _ends = ends;
        _faces = faces;
        _widths = widths;
        Generation = generation;
    }

    /// <summary>The number of runs; zero for <see cref="Primary"/>.</summary>
    public int Count => _ends.Length;

    /// <summary>Whether the whole text is drawn with the requested typeface.</summary>
    public bool IsPrimaryOnly => _ends.Length == 0;

    /// <summary>The cache's text generation the face indices belong to; stale indices draw with the primary font.</summary>
    public long Generation { get; }

    /// <summary>The end (exclusive, in UTF-16 code units) of run <paramref name="index"/>.</summary>
    public int GetEnd(int index) => _ends[index];

    /// <summary>The face run <paramref name="index"/> is drawn with.</summary>
    public int GetFace(int index) => _faces[index];

    /// <summary>The advance width of run <paramref name="index"/>.</summary>
    public float GetWidth(int index) => _widths[index];
}
