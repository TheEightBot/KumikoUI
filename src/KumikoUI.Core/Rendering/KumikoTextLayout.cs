using System.Buffers;
using System.Globalization;
using System.Text;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Lays text out in a width: one line cut with an ellipsis, or several lines wrapped at words or characters.
/// Every cut falls between two whole text elements (grapheme clusters), so an emoji, a surrogate pair or a letter
/// with combining marks is never split. <see cref="IDrawingContext"/> implementations use it to implement
/// <see cref="IDrawingContext.DrawTextInRect(string, KumikoRect, KumikoPaint, KumikoTextAlignment, KumikoVerticalAlignment, bool, KumikoTextWrap)"/>
/// and <see cref="IDrawingContext.MeasureText(string, KumikoPaint, float, KumikoTextWrap)"/> with their own
/// text measurement.
/// </summary>
/// <remarks>
/// Laying text out measures it several times and allocates the resulting lines, so a drawing context should cache
/// the <see cref="KumikoTextLines"/> it gets (they are immutable) rather than lay the same text out every frame.
/// </remarks>
public static class KumikoTextLayout
{
    /// <summary>The ellipsis that ends text cut to fit: U+2026 HORIZONTAL ELLIPSIS.</summary>
    public const string Ellipsis = "\u2026";

    // Most text fits; its element boundaries go on the stack.
    private const int StackBoundaries = 256;

    // C0 and C1 control characters, DEL, and the Unicode line and paragraph separators.
    private static readonly SearchValues<char> s_controls = SearchValues.Create(BuildControls());

    private static string BuildControls()
    {
        var chars = new StringBuilder();
        for (char c = '\0'; c < ' '; c++) chars.Append(c);
        for (char c = '\u007F'; c <= '\u009F'; c++) chars.Append(c);
        chars.Append('\u2028').Append('\u2029');
        return chars.ToString();
    }

    /// <summary>
    /// Whether <paramref name="text"/> contains a character that cannot be drawn as is on one line: a line break
    /// (CR, LF, NEL, U+2028, U+2029, vertical tab, form feed), a tab or another control character.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns><c>true</c> when <see cref="LayOut"/> replaces or removes characters of the text.</returns>
    public static bool ContainsControlCharacters(ReadOnlySpan<char> text) => text.IndexOfAny(s_controls) >= 0;

    /// <summary>
    /// The number of lines of a font that fit in <paramref name="height"/>, one per
    /// <see cref="KumikoFontMetrics.LineHeight"/> (the last without its leading); at least 1.
    /// </summary>
    /// <param name="height">The available height, in pixels.</param>
    /// <param name="metrics">The font's metrics.</param>
    /// <returns>The number of whole lines that fit, at least 1.</returns>
    public static int GetMaxLines(float height, KumikoFontMetrics metrics)
    {
        float step = metrics.LineHeight;
        if (float.IsNaN(step) || step <= 0f || float.IsNaN(height)) return 1;
        if (float.IsPositiveInfinity(height)) return int.MaxValue;
        // A small tolerance so a rectangle exactly n lines tall holds n lines despite float rounding.
        double lines = Math.Floor((height + metrics.Leading + 0.01f) / step);
        return lines < 1 ? 1 : lines >= int.MaxValue ? int.MaxValue : (int)lines;
    }

    /// <summary>
    /// The length, in UTF-16 code units, of the longest start of <paramref name="text"/> made of whole text
    /// elements whose width is at most <paramref name="maxWidth"/>. Zero when not even the first element fits.
    /// </summary>
    /// <param name="text">The text, on one line.</param>
    /// <param name="maxWidth">The available width, in pixels.</param>
    /// <param name="measure">Measures text in the font it is drawn in.</param>
    /// <returns>A text element boundary: 0, <c>text.Length</c>, or a position between two text elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measure"/> is <c>null</c>.</exception>
    public static int FitTextElements(ReadOnlySpan<char> text, float maxWidth, KumikoTextMeasurer measure)
    {
        ArgumentNullException.ThrowIfNull(measure);
        if (text.IsEmpty || float.IsNaN(maxWidth) || maxWidth < 0f) return 0;

        int[]? rented = null;
        Span<int> ends = text.Length <= StackBoundaries
            ? stackalloc int[StackBoundaries]
            : (rented = ArrayPool<int>.Shared.Rent(text.Length));
        try
        {
            // ends[i] = the end of text element i.
            int count = 0;
            for (int pos = 0; pos < text.Length;)
            {
                int length = StringInfo.GetNextTextElementLength(text[pos..]);
                pos += Math.Max(1, length);
                ends[count++] = pos;
            }

            // The most whole elements that fit; prefix widths grow with the prefix.
            int lo = 0, hi = count;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (measure(text[..ends[mid - 1]]) <= maxWidth)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return lo == 0 ? 0 : ends[lo - 1];
        }
        finally
        {
            if (rented is not null)
                ArrayPool<int>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Lays <paramref name="text"/> out in <paramref name="maxWidth"/>.
    /// </summary>
    /// <remarks>
    /// <para>With <see cref="KumikoTextWrap.None"/> the result is one line: line breaks and tabs become spaces,
    /// other control characters are removed, and text wider than <paramref name="maxWidth"/> is cut at a text
    /// element boundary and ends with <see cref="Ellipsis"/> (just the ellipsis when no character fits beside it;
    /// nothing when <paramref name="maxWidth"/> is zero).</para>
    /// <para>With <see cref="KumikoTextWrap.Word"/> or <see cref="KumikoTextWrap.Character"/> each line break in
    /// the text starts a new line and longer lines are wrapped; spaces at a wrap are dropped. At most
    /// <paramref name="maxLines"/> lines are produced: when the text needs more, the last line ends with an
    /// ellipsis.</para>
    /// </remarks>
    /// <param name="text">The text to lay out.</param>
    /// <param name="maxWidth">The available width, in pixels; <see cref="float.PositiveInfinity"/> for no limit.</param>
    /// <param name="wrap">Whether and where lines are wrapped.</param>
    /// <param name="maxLines">The most lines to produce (at least 1 is); ignored for <see cref="KumikoTextWrap.None"/>.</param>
    /// <param name="measure">Measures text in the font it is drawn in.</param>
    /// <returns>The lines to draw, top to bottom; <see cref="KumikoTextLines.Empty"/> for empty text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measure"/> is <c>null</c>.</exception>
    public static KumikoTextLines LayOut(string? text, float maxWidth, KumikoTextWrap wrap, int maxLines,
        KumikoTextMeasurer measure)
    {
        ArgumentNullException.ThrowIfNull(measure);
        if (string.IsNullOrEmpty(text)) return KumikoTextLines.Empty;
        if (float.IsNaN(maxWidth)) maxWidth = float.PositiveInfinity;
        if (maxWidth < 0f) maxWidth = 0f;

        if (wrap == KumikoTextWrap.None)
        {
            string line = Normalize(text, singleLine: true);
            string shown = Ellipsize(line, maxWidth, forceEllipsis: false, measure, out float width);
            return shown.Length == 0 ? KumikoTextLines.Empty : new KumikoTextLines([shown], [width]);
        }

        var lines = new List<string>();
        var widths = new List<float>();
        Wrap(Normalize(text, singleLine: false), maxWidth, wrap, Math.Max(1, maxLines), measure, lines, widths);
        return new KumikoTextLines(lines.ToArray(), widths.ToArray());
    }

    // ── Single line ──────────────────────────────────────────────

    /// <summary>
    /// <paramref name="line"/> when it fits <paramref name="maxWidth"/> (and no ellipsis is forced), else its
    /// longest whole-element start that fits beside an ellipsis, with the ellipsis.
    /// </summary>
    internal static string Ellipsize(string line, float maxWidth, bool forceEllipsis, KumikoTextMeasurer measure,
        out float width) =>
        Ellipsize(line.AsSpan(), line, maxWidth, forceEllipsis, measure, out width);

    private static string Ellipsize(ReadOnlySpan<char> line, string? lineString, float maxWidth, bool forceEllipsis,
        KumikoTextMeasurer measure, out float width)
    {
        if (!forceEllipsis)
        {
            width = measure(line);
            if (width <= maxWidth)
                return lineString ?? line.ToString();
        }

        if (float.IsNaN(maxWidth) || maxWidth <= 0f)
        {
            width = 0f;
            return string.Empty;
        }

        float ellipsisWidth = measure(Ellipsis);
        int fit = ellipsisWidth >= maxWidth ? 0 : FitTextElements(line, maxWidth - ellipsisWidth, measure);
        var kept = line[..fit].TrimEnd();
        if (kept.IsEmpty)
        {
            width = ellipsisWidth;
            return Ellipsis;
        }

        string shown = string.Concat(kept, Ellipsis);
        width = measure(shown);
        return shown;
    }

    /// <summary>
    /// <paramref name="text"/> with its line breaks (CRLF counted once) made spaces when
    /// <paramref name="singleLine"/>, or '\n' otherwise, tabs made spaces and other control characters removed.
    /// Returns <paramref name="text"/> itself when there is nothing to change.
    /// </summary>
    internal static string Normalize(string text, bool singleLine)
    {
        int first = text.AsSpan().IndexOfAny(s_controls);
        if (first < 0) return text;

        var result = new StringBuilder(text.Length);
        result.Append(text, 0, first);
        char lineBreak = singleLine ? ' ' : '\n';
        for (int i = first; i < text.Length; i++)
        {
            char c = text[i];
            switch (c)
            {
                case '\r':
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    result.Append(lineBreak);
                    break;
                case '\n' or '\v' or '\f' or '\u0085' or '\u2028' or '\u2029':
                    result.Append(lineBreak);
                    break;
                case '\t':
                    result.Append(' ');
                    break;
                default:
                    if (!s_controls.Contains(c))
                        result.Append(c);
                    break;
            }
        }
        return result.ToString();
    }

    // ── Wrapping ─────────────────────────────────────────────────

    private static void Wrap(string text, float maxWidth, KumikoTextWrap wrap, int maxLines,
        KumikoTextMeasurer measure, List<string> lines, List<float> widths)
    {
        // Line breaks at the very end add no visible line.
        int end = text.AsSpan().TrimEnd('\n').Length;
        if (end == 0) return;
        int paragraphStart = 0;
        while (lines.Count < maxLines)
        {
            int newline = end > paragraphStart ? text.AsSpan(paragraphStart, end - paragraphStart).IndexOf('\n') : -1;
            int paragraphEnd = newline < 0 ? end : paragraphStart + newline;
            bool moreParagraphs = newline >= 0;

            int lineStart = paragraphStart;
            while (true)
            {
                var rest = text.AsSpan(lineStart, paragraphEnd - lineStart);
                bool lastSlot = lines.Count == maxLines - 1;
                float restWidth = measure(rest);

                if (restWidth <= maxWidth)
                {
                    if (lastSlot && moreParagraphs)
                    {
                        // More text follows, but not on this cell's lines.
                        lines.Add(Ellipsize(rest, null, maxWidth, forceEllipsis: true, measure, out float w));
                        widths.Add(w);
                    }
                    else
                    {
                        lines.Add(rest.ToString());
                        widths.Add(restWidth);
                    }
                    break;
                }

                if (lastSlot)
                {
                    lines.Add(Ellipsize(rest, null, maxWidth, forceEllipsis: false, measure, out float w));
                    widths.Add(w);
                    break;
                }

                int fit = FitTextElements(rest, maxWidth, measure);
                int breakAt = fit;
                if (wrap == KumikoTextWrap.Word)
                {
                    int wordBreak = FindWordBreak(rest, fit);
                    if (wordBreak > 0) breakAt = wordBreak;
                }
                if (breakAt == 0)
                {
                    // Not even one element fits: take it anyway so every line shows something and the loop ends.
                    breakAt = Math.Max(1, StringInfo.GetNextTextElementLength(rest));
                }

                var line = rest[..breakAt].TrimEnd();
                lines.Add(line.ToString());
                widths.Add(measure(line));

                lineStart += breakAt;
                while (lineStart < paragraphEnd && char.IsWhiteSpace(text[lineStart]))
                    lineStart++;
                if (lineStart >= paragraphEnd)
                    break;
            }

            if (!moreParagraphs) return;
            paragraphStart = paragraphEnd + 1;
        }
    }

    /// <summary>
    /// The last place at or before <paramref name="fit"/> where a line may end between words: before a space, or
    /// after a hyphen that follows other text. Zero when there is none.
    /// </summary>
    private static int FindWordBreak(ReadOnlySpan<char> text, int fit)
    {
        for (int i = fit; i > 0; i--)
        {
            if (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                if (!text[..i].IsWhiteSpace()) return i;
                continue;
            }
            char before = text[i - 1];
            if (i >= 2 && i < text.Length && (before == '-' || before == '\u2010')
                && !char.IsWhiteSpace(text[i - 2]) && !IsExtending(text[i]))
                return i;
        }
        return 0;
    }

    /// <summary>Whether <paramref name="c"/> attaches to the character before it (a combining mark or a joiner).</summary>
    private static bool IsExtending(char c) => CharUnicodeInfo.GetUnicodeCategory(c) is
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
        or UnicodeCategory.Format;
}
