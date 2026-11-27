using System.Text;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Export;

/// <summary>A growable char buffer the CSV rows are formatted into.</summary>
internal sealed class CharBuffer
{
    private char[] _chars = new char[4096];

    public int Length { get; private set; }

    public ReadOnlySpan<char> Span => new(_chars, 0, Length);

    public void Clear() => Length = 0;

    public void Append(char c)
    {
        if (Length == _chars.Length) Array.Resize(ref _chars, _chars.Length * 2);
        _chars[Length++] = c;
    }

    public void Append(ReadOnlySpan<char> text)
    {
        if (Length + text.Length > _chars.Length)
            Array.Resize(ref _chars, Math.Max(_chars.Length * 2, Length + text.Length));
        text.CopyTo(_chars.AsSpan(Length));
        Length += text.Length;
    }

    public override string ToString() => new(_chars, 0, Length);
}

/// <summary>Formats export rows as RFC 4180 records into a <see cref="CharBuffer"/>.</summary>
internal sealed class CsvRowFormatter(KumikoCsvOptions options, char delimiter)
{
    private bool _any;

    /// <summary>Writes the separator before a row (none before the first) and then the row.</summary>
    public void WriteRow(KumikoExportRow row, CharBuffer buffer)
    {
        if (_any) buffer.Append(options.LineEnding);
        _any = true;

        var cells = row.Cells;
        for (int c = 0; c < cells.Length; c++)
        {
            if (c > 0) buffer.Append(delimiter);
            WriteCell(row, cells[c], buffer);
        }
    }

    /// <summary>Ends the last row when the options ask for it.</summary>
    public void Finish(CharBuffer buffer)
    {
        if (_any && options.EndWithLineEnding) buffer.Append(options.LineEnding);
    }

    private void WriteCell(KumikoExportRow row, KumikoExportCell cell, CharBuffer buffer)
    {
        string text = cell.Text;
        bool prefix = false;
        if (cell.Value == null && row.Kind == KumikoExportRowKind.Data && text.Length == 0)
        {
            text = options.NullText;
        }
        else if (options.FormulaInjection == KumikoFormulaInjection.Prefix && row.Kind != KumikoExportRowKind.Header
                 && text.Length > 0 && IsTextValue(cell.Value) && IsFormulaStart(text[0]))
        {
            prefix = true;
        }

        bool quote = options.QuoteAllFields || KumikoDelimitedText.NeedsQuoting(text, delimiter, quoteEdgeSpaces: true);
        if (text.Length == 0 && !options.QuoteAllFields) return;

        if (quote) buffer.Append('"');
        if (prefix) buffer.Append('\'');
        if (!quote)
        {
            buffer.Append(text);
            return;
        }

        var rest = text.AsSpan();
        int q;
        while ((q = rest.IndexOf('"')) >= 0)
        {
            buffer.Append(rest[..(q + 1)]);
            buffer.Append('"'); // an embedded quote is doubled
            rest = rest[(q + 1)..];
        }
        buffer.Append(rest);
        buffer.Append('"');
    }

    // A string, or any other reference value (its text comes from ToString and is as untrusted as a string);
    // numbers, dates, booleans and other value types are never altered.
    private static bool IsTextValue(object? value) => value is string || value is not null && !value.GetType().IsValueType;

    private static bool IsFormulaStart(char c) => c is '=' or '+' or '-' or '@' or '\t' or '\r';
}

/// <summary>
/// Encodes a <see cref="CharBuffer"/> to a stream: the byte order mark first, then the text as it is drained. The
/// destination is never closed. Reading on a pool thread drains with blocking waits on the stream's async write
/// (so a stream that forbids synchronous I/O works); reading on the caller's thread lets the buffer grow until the
/// caller drains it with <see cref="FlushAsync"/> between chunks.
/// </summary>
internal sealed class CsvStreamSink
{
    private const int DrainThreshold = 32 * 1024; // chars, about 64 KB of bytes

    private readonly Stream _stream;
    private readonly Encoding _encoding;
    private readonly Encoder _encoder;
    private readonly byte[] _bom;
    private readonly bool _canBlock;
    private readonly CancellationToken _ct;
    private byte[] _bytes = new byte[DrainThreshold * 2];
    private bool _started;

    public CsvStreamSink(Stream stream, KumikoCsvOptions options, bool canBlock, CancellationToken ct)
    {
        _stream = stream;
        _encoding = options.Encoding;
        _encoder = _encoding.GetEncoder();
        _canBlock = canBlock;
        _ct = ct;
        _bom = !options.EmitByteOrderMark ? Array.Empty<byte>()
            : _encoding is UTF8Encoding ? Encoding.UTF8.GetPreamble() : _encoding.GetPreamble();
        Buffer = new CharBuffer();
    }

    public CharBuffer Buffer { get; }

    /// <summary>Drains the buffer when it is large and the thread may block.</summary>
    public void AfterRow()
    {
        if (_canBlock && Buffer.Length >= DrainThreshold)
            DrainAsync(false, _ct).AsTask().GetAwaiter().GetResult();
    }

    public ValueTask FlushAsync(CancellationToken ct) => DrainAsync(false, ct);

    public ValueTask CompleteAsync(CancellationToken ct) => DrainAsync(true, ct);

    private async ValueTask DrainAsync(bool final, CancellationToken ct)
    {
        if (!_started)
        {
            _started = true;
            if (_bom.Length > 0) await _stream.WriteAsync(_bom, ct).ConfigureAwait(false);
        }

        var chars = Buffer.Span;
        int max = _encoding.GetMaxByteCount(chars.Length);
        if (_bytes.Length < max) _bytes = new byte[max];
        int n = _encoder.GetBytes(chars, _bytes, final);
        Buffer.Clear();
        if (n > 0) await _stream.WriteAsync(_bytes.AsMemory(0, n), ct).ConfigureAwait(false);
        if (final) await _stream.FlushAsync(ct).ConfigureAwait(false);
    }
}
