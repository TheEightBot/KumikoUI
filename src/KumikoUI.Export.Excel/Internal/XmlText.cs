using System.Globalization;
using System.Text;

namespace KumikoUI.Export.Excel.Internal;

/// <summary>
/// A growable character buffer that XML markup is written into: <see cref="Raw(string)"/> for markup the writer builds,
/// <see cref="Text"/> and <see cref="Attribute"/> for values that need escaping. Writing a part through it (instead of an
/// <c>XmlWriter</c>) keeps a cell to a few span copies with no allocation.
/// </summary>
internal sealed class XmlText
{
    /// <summary>The most characters Excel keeps in one cell.</summary>
    public const int MaxCellLength = 32_767;

    private char[] _chars;

    public XmlText(int capacity = 4096) => _chars = new char[capacity];

    public int Length { get; private set; }

    public ReadOnlySpan<char> Span => new(_chars, 0, Length);

    public void Clear() => Length = 0;

    public override string ToString() => new(_chars, 0, Length);

    public XmlText Raw(char c)
    {
        if (Length == _chars.Length) Grow(1);
        _chars[Length++] = c;
        return this;
    }

    public XmlText Raw(string markup) => Raw(markup.AsSpan());

    public XmlText Raw(ReadOnlySpan<char> markup)
    {
        if (Length + markup.Length > _chars.Length) Grow(markup.Length);
        markup.CopyTo(_chars.AsSpan(Length));
        Length += markup.Length;
        return this;
    }

    public XmlText Int(long value)
    {
        if (Length + 20 > _chars.Length) Grow(20);
        value.TryFormat(_chars.AsSpan(Length), out int written, default, CultureInfo.InvariantCulture);
        Length += written;
        return this;
    }

    /// <summary>Appends a number in invariant, round-trippable form (xsd:double).</summary>
    public XmlText Number(double value)
    {
        if (Length + 32 > _chars.Length) Grow(32);
        value.TryFormat(_chars.AsSpan(Length), out int written, "R", CultureInfo.InvariantCulture);
        Length += written;
        return this;
    }

    /// <summary>Appends a single-precision number in invariant, shortest round-trippable form.</summary>
    public XmlText Number(float value)
    {
        if (Length + 32 > _chars.Length) Grow(32);
        value.TryFormat(_chars.AsSpan(Length), out int written, "R", CultureInfo.InvariantCulture);
        Length += written;
        return this;
    }

    /// <summary>Appends a decimal in invariant form.</summary>
    public XmlText Number(decimal value)
    {
        if (Length + 40 > _chars.Length) Grow(40);
        value.TryFormat(_chars.AsSpan(Length), out int written, default, CultureInfo.InvariantCulture);
        Length += written;
        return this;
    }

    /// <summary>Appends element content: escaped, with characters XML 1.0 forbids removed.</summary>
    public XmlText Text(ReadOnlySpan<char> text) => Escape(text, attribute: false, ooxml: false, int.MaxValue);

    /// <summary>Appends an attribute value (without the quotes): escaped, with characters XML 1.0 forbids removed.</summary>
    public XmlText Attribute(ReadOnlySpan<char> text) => Escape(text, attribute: true, ooxml: false, int.MaxValue);

    /// <summary>
    /// Appends the text of a cell: escaped as <see cref="Text"/>, with a literal <c>_xHHHH_</c> protected from Excel's
    /// escape decoding, and cut to <see cref="MaxCellLength"/> characters (never inside a surrogate pair).
    /// </summary>
    public XmlText CellText(ReadOnlySpan<char> text) => Escape(text, attribute: false, ooxml: true, MaxCellLength);

    /// <summary>Whether a cell's text needs <c>xml:space="preserve"</c> to keep its leading or trailing white space.</summary>
    public static bool NeedsPreserve(ReadOnlySpan<char> text) =>
        text.Length > 0 && (IsXmlSpace(text[0]) || IsXmlSpace(text[^1]));

    private static bool IsXmlSpace(char c) => c is ' ' or '\t' or '\n' or '\r';

    private XmlText Escape(ReadOnlySpan<char> text, bool attribute, bool ooxml, int maxChars)
    {
        int kept = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    if (kept + 2 > maxChars) break;
                    Raw(c).Raw(text[++i]);
                    kept += 2;
                }
                continue; // an unpaired surrogate is not a character
            }

            if (char.IsLowSurrogate(c) || c is '￾' or '￿' || (c < ' ' && c is not ('\t' or '\n' or '\r')))
                continue; // not allowed in XML 1.0

            if (kept + 1 > maxChars) break;
            kept++;
            switch (c)
            {
                case '&': Raw("&amp;"); break;
                case '<': Raw("&lt;"); break;
                case '>': Raw("&gt;"); break;
                case '"' when attribute: Raw("&quot;"); break;
                case '\r': Raw("&#xD;"); break; // a parser would turn a raw CR (or CRLF) into LF
                case '\n' when attribute: Raw("&#xA;"); break;
                case '\t' when attribute: Raw("&#x9;"); break;
                case '_' when ooxml && IsOoxmlEscape(text, i): Raw("_x005F_"); break; // Excel would decode _xHHHH_
                default: Raw(c); break;
            }
        }

        return this;
    }

    // "_x" + four hex digits + "_" is how OOXML escapes a character in a string; a literal one needs its "_" escaped.
    private static bool IsOoxmlEscape(ReadOnlySpan<char> text, int i)
    {
        if (i + 6 >= text.Length || text[i + 1] != 'x' || text[i + 6] != '_') return false;
        for (int k = i + 2; k < i + 6; k++)
            if (!char.IsAsciiHexDigit(text[k])) return false;
        return true;
    }

    private void Grow(int needed) =>
        Array.Resize(ref _chars, Math.Max(_chars.Length * 2, Length + needed));
}

/// <summary>
/// Encodes an <see cref="XmlText"/> buffer as UTF-8 into a stream. On a thread that may block, the buffer is drained
/// whenever it passes about 64 KB; on the caller's thread it is only drained, asynchronously, between chunks.
/// </summary>
internal sealed class XmlStreamSink
{
    private const int DrainThreshold = 32 * 1024; // chars

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Stream _stream;
    private readonly Encoder _encoder = Utf8.GetEncoder();
    private readonly bool _canBlock;
    private byte[] _bytes = new byte[DrainThreshold * 3 + 16];

    public XmlStreamSink(Stream stream, bool canBlock)
    {
        _stream = stream;
        _canBlock = canBlock;
    }

    public XmlText Buffer { get; } = new(DrainThreshold + 4096);

    public void AfterRow()
    {
        if (_canBlock && Buffer.Length >= DrainThreshold) Drain(final: false);
    }

    public async ValueTask FlushAsync(CancellationToken ct)
    {
        int n = Encode(final: false);
        if (n > 0) await _stream.WriteAsync(_bytes.AsMemory(0, n), ct).ConfigureAwait(false);
    }

    public async ValueTask CompleteAsync(CancellationToken ct)
    {
        int n = Encode(final: true);
        if (n > 0) await _stream.WriteAsync(_bytes.AsMemory(0, n), ct).ConfigureAwait(false);
    }

    private void Drain(bool final)
    {
        int n = Encode(final);
        if (n > 0) _stream.Write(_bytes, 0, n);
    }

    private int Encode(bool final)
    {
        var chars = Buffer.Span;
        int max = Utf8.GetMaxByteCount(chars.Length);
        if (_bytes.Length < max) _bytes = new byte[max];
        int n = _encoder.GetBytes(chars, _bytes, final);
        Buffer.Clear();
        return n;
    }

    /// <summary>The UTF-8 bytes of a small part held in a buffer.</summary>
    public static byte[] ToBytes(XmlText text) => Utf8.GetBytes(text.ToString());
}
