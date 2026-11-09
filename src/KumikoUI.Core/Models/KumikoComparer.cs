using System.Collections;
using System.Globalization;

namespace KumikoUI.Core.Models;

/// <summary>
/// Orders any two cell values without throwing, the way the grid sorts, groups, filters and summarizes:
/// <list type="bullet">
/// <item>null first;</item>
/// <item>text by culture, with <see cref="Options"/> (case-insensitive by default);</item>
/// <item>numbers by value across numeric types (an <c>int</c> and a <c>double</c> compare as numbers);</item>
/// <item>dates and times chronologically across <see cref="DateTime"/>, <see cref="DateTimeOffset"/> and
/// <see cref="DateOnly"/>;</item>
/// <item>other values of one type through <see cref="IComparable"/>;</item>
/// <item>anything else by its text, then by type name, so the order is always deterministic.</item>
/// </list>
/// </summary>
public sealed class KumikoComparer : IComparer<object?>, IComparer
{
    /// <summary>Current culture (at the time of each comparison), ignoring case.</summary>
    public static KumikoComparer Default { get; } = new();

    private readonly CultureInfo? _culture;

    /// <summary>Creates a comparer.</summary>
    /// <param name="culture">The culture for text, or null for the current culture at the time of each comparison.</param>
    /// <param name="options">How text compares; <see cref="CompareOptions.IgnoreCase"/> by default.</param>
    public KumikoComparer(CultureInfo? culture = null, CompareOptions options = CompareOptions.IgnoreCase)
    {
        _culture = culture;
        Options = options;
    }

    /// <summary>The culture used for text, formatting and parsing.</summary>
    public CultureInfo Culture => _culture ?? CultureInfo.CurrentCulture;

    /// <summary>How text compares.</summary>
    public CompareOptions Options { get; }

    /// <summary>Equality for text under <see cref="Culture"/> and <see cref="Options"/>, for sets of display text.</summary>
    public StringComparer TextComparer => Culture.CompareInfo.GetStringComparer(Options);

    /// <inheritdoc />
    public int Compare(object? x, object? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        if (x is string sx && y is string sy)
            return CompareText(sx, sy);

        if (TryGetNumber(x, out var nx) && TryGetNumber(y, out var ny))
            return nx.CompareTo(ny);

        if (TryGetInstant(x, out var dx) && TryGetInstant(y, out var dy))
            return dx.CompareTo(dy);

        if (x.GetType() == y.GetType() && x is IComparable comparable)
        {
            try
            {
                return comparable.CompareTo(y);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or InvalidCastException)
            {
                // fall through to the text order
            }
        }

        int byText = CompareText(Format(x), Format(y));
        return byText != 0
            ? byText
            : string.CompareOrdinal(x.GetType().FullName, y.GetType().FullName);
    }

    /// <summary>Compare two strings under <see cref="Culture"/> and <see cref="Options"/>.</summary>
    public int CompareText(string? x, string? y) => Culture.CompareInfo.Compare(x, y, Options);

    /// <summary>Whether two strings are equal under <see cref="Culture"/> and <see cref="Options"/>.</summary>
    public bool TextEquals(string x, string y) => CompareText(x, y) == 0;

    /// <summary>Whether <paramref name="text"/> contains <paramref name="value"/>.</summary>
    public bool TextContains(string text, string value) => Culture.CompareInfo.IndexOf(text, value, Options) >= 0;

    /// <summary>Whether <paramref name="text"/> starts with <paramref name="value"/>.</summary>
    public bool TextStartsWith(string text, string value) => Culture.CompareInfo.IsPrefix(text, value, Options);

    /// <summary>Whether <paramref name="text"/> ends with <paramref name="value"/>.</summary>
    public bool TextEndsWith(string text, string value) => Culture.CompareInfo.IsSuffix(text, value, Options);

    /// <summary>A value as text in <see cref="Culture"/> (empty for null).</summary>
    public string Format(object? value, string? format = null) => value switch
    {
        null => string.Empty,
        IFormattable formattable => formattable.ToString(string.IsNullOrEmpty(format) ? null : format, Culture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>A number as a value that orders across numeric types.</summary>
    internal static bool TryGetNumber(object value, out NumericValue number)
    {
        switch (value)
        {
            case int v: number = new(v); return true;
            case long v: number = new(v); return true;
            case short v: number = new(v); return true;
            case sbyte v: number = new(v); return true;
            case byte v: number = new(v); return true;
            case ushort v: number = new(v); return true;
            case uint v: number = new(v); return true;
            case ulong v: number = new((decimal)v); return true;
            case decimal v: number = new(v); return true;
            case double v: number = new(v); return true;
            case float v: number = new(v); return true;
            case Half v: number = new((double)v); return true;
            default: number = default; return false;
        }
    }

    private static bool TryGetInstant(object value, out DateTimeOffset instant)
    {
        switch (value)
        {
            case DateTimeOffset v: instant = v; return true;
            // A DateTime of unspecified kind is taken as local time, like DateTimeOffset's own conversion.
            case DateTime v: instant = v.Kind == DateTimeKind.Utc ? new DateTimeOffset(v) : new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Local)); return true;
            case DateOnly v: instant = new DateTimeOffset(v.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local)); return true;
            default: instant = default; return false;
        }
    }

    /// <summary>
    /// A number kept exact as <see cref="decimal"/> where it fits, otherwise as <see cref="double"/>, so
    /// mixed numeric types compare by value.
    /// </summary>
    internal readonly struct NumericValue : IComparable<NumericValue>
    {
        private readonly decimal _decimal;
        private readonly double _double;
        private readonly bool _isDecimal;

        public NumericValue(decimal value) { _decimal = value; _double = (double)value; _isDecimal = true; }

        public NumericValue(long value) : this((decimal)value) { }

        public NumericValue(double value) { _decimal = 0; _double = value; _isDecimal = false; }

        public double AsDouble => _double;

        public int CompareTo(NumericValue other) =>
            _isDecimal && other._isDecimal ? _decimal.CompareTo(other._decimal) : _double.CompareTo(other._double);
    }
}
