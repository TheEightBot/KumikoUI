using SkiaSharp;

namespace KumikoUI.SkiaSharp;

/// <summary>
/// How one line of text splits into runs drawn with different typefaces: the requested (primary) typeface where
/// it has the glyphs, and fallback typefaces for the rest. Immutable and cached with the text's measurement in
/// <see cref="SkiaResourceCache"/>, so drawing it again allocates nothing.
/// </summary>
/// <remarks>
/// <para>Every run ends on a text element boundary, so an emoji sequence, a flag or a letter with combining marks is
/// never split between two typefaces. A face is an index into the cache's fallback table, or <see cref="PrimaryFace"/>.</para>
/// <para>The only mutable state is the <see cref="SKTextBlob"/> of each run, made the first time the run is drawn
/// and drawn from then on: building a blob allocates, drawing one does not. The blobs go when the entry that holds
/// the runs leaves the cache (<see cref="Dispose"/>).</para>
/// </remarks>
internal sealed class FontRuns : IDisposable
{
    /// <summary>The face index of the requested typeface.</summary>
    public const int PrimaryFace = -1;

    private readonly int[] _ends;
    private readonly int[] _faces;
    private readonly float[] _widths;

    private readonly SKTextBlob?[] _blobs;
    private readonly bool[] _blank;
    private bool _disposed;

    /// <summary>
    /// The whole text is drawn with the requested typeface (the common case): no runs to walk. Every text gets its
    /// own instance, because the instance caches the text's blob.
    /// </summary>
    public static FontRuns CreatePrimary(long generation) => new([], [], [], generation);

    public FontRuns(int[] ends, int[] faces, float[] widths, long generation)
    {
        _ends = ends;
        _faces = faces;
        _widths = widths;
        Generation = generation;
        // A text drawn in the requested typeface alone has no runs, but still one blob.
        int blobs = Math.Max(1, ends.Length);
        _blobs = new SKTextBlob?[blobs];
        _blank = new bool[blobs];
    }

    /// <summary>The number of runs; zero when <see cref="IsPrimaryOnly"/>.</summary>
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

    /// <summary>
    /// The blob of run <paramref name="index"/> (the whole text for <see cref="IsPrimaryOnly"/> runs, index 0), or
    /// <c>null</c> when it was not made yet or the run has nothing to draw (<paramref name="blank"/>).
    /// </summary>
    public SKTextBlob? GetBlob(int index, out bool blank)
    {
        blank = _blank[index];
        return Volatile.Read(ref _blobs[index]);
    }

    /// <summary>
    /// Keeps <paramref name="blob"/> (<c>null</c>: the run has nothing to draw) as the blob of run
    /// <paramref name="index"/> and returns the blob to draw with: another thread's when it was first, or
    /// <paramref name="blob"/> itself, which is then owned by these runs.
    /// </summary>
    public SKTextBlob? SetBlob(int index, SKTextBlob? blob)
    {
        if (blob is null)
        {
            _blank[index] = true;
            return null;
        }

        var existing = Interlocked.CompareExchange(ref _blobs[index], blob, null);
        if (existing is not null)
        {
            blob.Dispose();
            return existing;
        }
        return blob;
    }

    /// <summary>Disposes the blobs. The runs themselves stay readable.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = 0; i < _blobs.Length; i++)
            Interlocked.Exchange(ref _blobs[i], null)?.Dispose();
    }
}
