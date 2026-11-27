using System.Globalization;
using System.Text;

namespace KumikoUI.Export.Excel.Internal;

/// <summary>
/// Maps a column's .NET <c>Format</c> to an Excel number format code, per kind of value. A format that cannot be
/// expressed exactly maps to <see langword="null"/>, and the writer then writes the grid's display text instead, so the
/// sheet never shows a different value than the grid. Excel draws separators, month and day names in the reader's
/// locale (or the locale prefix written here), so the text can differ in form, never in value.
/// </summary>
internal static class XlsxNumberFormats
{
    /// <summary>Excel's General format (built-in id 0).</summary>
    public const string General = "General";

    /// <summary>The format of a <see cref="TimeSpan"/>: elapsed hours, minutes and seconds.</summary>
    public const string Duration = "[h]:mm:ss";

    /// <summary>The format code for a number, or <see langword="null"/> when <paramref name="format"/> cannot be mapped.</summary>
    /// <param name="format">The column's format.</param>
    /// <param name="culture">The export culture (decimal digits, currency symbol and patterns).</param>
    /// <param name="integerOnly">Set when the format applies to integers only (<c>D</c>).</param>
    public static string? ForNumber(string? format, CultureInfo culture, out bool integerOnly)
    {
        integerOnly = false;
        if (string.IsNullOrEmpty(format)) return General;
        var nfi = culture.NumberFormat;

        if (TryStandard(format, out char letter, out int? precision))
        {
            switch (letter)
            {
                case 'N':
                    return Signed(Grouped(precision ?? nfi.NumberDecimalDigits), NumberNegative(nfi.NumberNegativePattern), nfi.NegativeSign);
                case 'F':
                    return Signed(Fixed(precision ?? nfi.NumberDecimalDigits), "-n", nfi.NegativeSign);
                case 'D':
                    integerOnly = true;
                    return Signed(new string('0', Math.Max(1, precision ?? 1)), "-n", nfi.NegativeSign);
                case 'C':
                    return Currency(precision ?? nfi.CurrencyDecimalDigits, nfi);
                case 'P':
                    return Percent(precision ?? nfi.PercentDecimalDigits, nfi);
                case 'E':
                    return Signed("0" + Decimals(precision ?? 6) + (char.IsUpper(format[0]) ? "E+000" : "e+000"), "-n", nfi.NegativeSign);
                case 'G':
                case 'R':
                    return precision is null ? General : null;
                default:
                    return null; // X (hexadecimal), B (binary) and unknown letters
            }
        }

        return CustomNumber(format);
    }

    /// <summary>The format code for a date and time, or <see langword="null"/> when it cannot be mapped.</summary>
    /// <param name="format">The column's format; <see langword="null"/> or empty uses <paramref name="defaultFormat"/>.</param>
    /// <param name="defaultFormat">What .NET uses without a format: <c>G</c> for a date and time, <c>d</c> for a date, <c>t</c> for a time.</param>
    /// <param name="culture">The export culture.</param>
    public static string? ForDate(string? format, string defaultFormat, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(format)) format = defaultFormat;
        var dtf = culture.DateTimeFormat;
        string? pattern = format.Length != 1 ? format : format[0] switch
        {
            'd' => dtf.ShortDatePattern,
            'D' => dtf.LongDatePattern,
            'f' => dtf.LongDatePattern + " " + dtf.ShortTimePattern,
            'F' => dtf.FullDateTimePattern,
            'g' => dtf.ShortDatePattern + " " + dtf.ShortTimePattern,
            'G' => dtf.ShortDatePattern + " " + dtf.LongTimePattern,
            'm' or 'M' => dtf.MonthDayPattern,
            's' => dtf.SortableDateTimePattern,
            't' => dtf.ShortTimePattern,
            'T' => dtf.LongTimePattern,
            'y' or 'Y' => dtf.YearMonthPattern,
            // o and O need seven fraction digits and the offset; r, R, u and U convert to UTC first.
            _ => null,
        };
        if (pattern == null) return null;
        // A single specifier is only a custom format when it is written "%x".
        if (format.Length == 2 && format[0] == '%') pattern = format[1..];
        return CustomDate(pattern, culture);
    }

    /// <summary>The format code for a <see cref="TimeSpan"/>, or <see langword="null"/> when it cannot be mapped.</summary>
    public static string? ForDuration(string? format) =>
        string.IsNullOrEmpty(format) || format is "c" or "t" or "T" or "g" or "G" ? Duration : null;

    // ── standard numeric formats ────────────────────────────────────────────────────────────────────────────────────

    private static bool TryStandard(string format, out char letter, out int? precision)
    {
        letter = char.ToUpperInvariant(format[0]);
        precision = null;
        if (!char.IsAsciiLetter(format[0]) || format.Length > 3) return false;
        if (format.Length == 1) return true;
        if (!int.TryParse(format.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int p)) return false;
        precision = p;
        return p <= 30; // Excel shows at most 30 decimals
    }

    private static string Decimals(int digits) => digits <= 0 ? string.Empty : "." + new string('0', digits);

    private static string Grouped(int digits) => "#,##0" + Decimals(digits);

    private static string Fixed(int digits) => "0" + Decimals(digits);

    private static string NumberNegative(int pattern) => pattern switch
    {
        0 => "(n)",
        2 => "- n",
        3 => "n-",
        4 => "n -",
        _ => "-n",
    };

    /// <summary>A number code with a negative section when the culture's negative form is not Excel's own "-n".</summary>
    private static string Signed(string number, string negativePattern, string negativeSign)
    {
        if (negativePattern == "-n" && negativeSign == "-") return number;
        return number + ";" + Expand(negativePattern, number, negativeSign, symbol: null);
    }

    private static string Currency(int digits, NumberFormatInfo nfi)
    {
        string number = Grouped(digits);
        string positive = nfi.CurrencyPositivePattern switch
        {
            0 => "$n",
            1 => "n$",
            2 => "$ n",
            _ => "n $",
        };
        string negative = nfi.CurrencyNegativePattern switch
        {
            0 => "($n)",
            1 => "-$n",
            2 => "$-n",
            3 => "$n-",
            4 => "(n$)",
            5 => "-n$",
            6 => "n-$",
            7 => "n$-",
            8 => "-n $",
            9 => "-$ n",
            10 => "n $-",
            11 => "$ n-",
            12 => "$ -n",
            13 => "n- $",
            14 => "($ n)",
            15 => "(n $)",
            _ => "$- n",
        };
        string symbol = nfi.CurrencySymbol;
        return Expand(positive, number, nfi.NegativeSign, symbol) + ";" + Expand(negative, number, nfi.NegativeSign, symbol);
    }

    private static string? Percent(int digits, NumberFormatInfo nfi)
    {
        if (nfi.PercentSymbol != "%") return null; // Excel's % is the only symbol that scales by 100
        string number = Grouped(digits);
        string positive = nfi.PercentPositivePattern switch
        {
            0 => "n %",
            1 => "n%",
            2 => "%n",
            _ => "% n",
        };
        string negative = nfi.PercentNegativePattern switch
        {
            0 => "-n %",
            1 => "-n%",
            2 => "-%n",
            3 => "%-n",
            4 => "%n-",
            5 => "n-%",
            6 => "n%-",
            7 => "-% n",
            8 => "n %-",
            9 => "% n-",
            10 => "% -n",
            _ => "n- %",
        };
        return Expand(positive, number, nfi.NegativeSign, symbol: null) + ";" + Expand(negative, number, nfi.NegativeSign, symbol: null);
    }

    /// <summary>
    /// Expands a .NET pattern ("n" the number, "$" the currency symbol, "-" the negative sign, "%" the percent sign) into
    /// an Excel section with every literal quoted.
    /// </summary>
    private static string Expand(string pattern, string number, string negativeSign, string? symbol)
    {
        var sb = new StringBuilder();
        var literal = new StringBuilder();
        void FlushLiteral()
        {
            if (literal.Length == 0) return;
            AppendQuoted(sb, literal.ToString());
            literal.Clear();
        }

        foreach (char c in pattern)
        {
            switch (c)
            {
                case 'n': FlushLiteral(); sb.Append(number); break;
                case '%': FlushLiteral(); sb.Append('%'); break;
                case '$': literal.Append(symbol); break;
                case '-': literal.Append(negativeSign); break;
                default: literal.Append(c); break;
            }
        }

        FlushLiteral();
        return sb.ToString();
    }

    private static void AppendQuoted(StringBuilder sb, string literal)
    {
        // A double quote cannot appear inside an Excel quoted string; escape it on its own.
        var parts = literal.Split('"');
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) sb.Append("\\\"");
            if (parts[i].Length > 0) sb.Append('"').Append(parts[i]).Append('"');
        }
    }

    // ── custom numeric formats ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A custom numeric format whose syntax means the same in .NET and Excel: digit placeholders <c>0 #</c>, the decimal
    /// point, grouping and scaling commas, <c>%</c>, exponents <c>E+0</c>/<c>E-0</c>, up to three non-empty sections,
    /// quoted and escaped literals and the literal characters <c>$ - + ( )</c> and space.
    /// </summary>
    private static string? CustomNumber(string format)
    {
        var sb = new StringBuilder(format.Length + 8);
        int sections = 1, inSection = 0;
        for (int i = 0; i < format.Length; i++)
        {
            char c = format[i];
            switch (c)
            {
                case '0' or '#' or '.' or ',' or '%':
                    sb.Append(c);
                    inSection++;
                    break;
                case '$' or '-' or '+' or '(' or ')' or ' ':
                    sb.Append(c);
                    inSection++;
                    break;
                case ';':
                    if (inSection == 0 || ++sections > 3) return null; // .NET and Excel read empty sections differently
                    sb.Append(';');
                    inSection = 0;
                    break;
                case 'E' or 'e':
                    if (i + 2 >= format.Length || format[i + 1] is not ('+' or '-') || format[i + 2] != '0') return null;
                    sb.Append(c).Append(format[i + 1]);
                    i++;
                    inSection++;
                    break;
                case '\\':
                    if (i + 1 >= format.Length || format[i + 1] == '"') return null;
                    sb.Append('\\').Append(format[++i]);
                    inSection++;
                    break;
                case '\'' or '"':
                    int end = format.IndexOf(c, i + 1);
                    if (end < 0) return null;
                    string literal = format.Substring(i + 1, end - i - 1);
                    if (literal.Contains('"') || literal.Contains('\\')) return null;
                    sb.Append('"').Append(literal).Append('"');
                    i = end;
                    inSection++;
                    break;
                default:
                    return null; // per mille, digits 1-9 and letters mean different things (or nothing) in Excel
            }
        }

        return inSection == 0 ? null : sb.ToString();
    }

    // ── custom date and time formats ────────────────────────────────────────────────────────────────────────────────

    private enum Token { Literal, Year, Month, Day, Hour12, Hour24, Minute, Second, Fraction, AmPm }

    /// <summary>
    /// Converts a .NET custom date and time pattern to an Excel code. Literals are quoted, <c>/</c> and <c>:</c> become
    /// the culture's separators, and month and day names get the culture's locale prefix. Patterns Excel cannot match
    /// exactly (a one-digit year, a 12-hour clock without AM/PM or with designators other than AM/PM, eras, offsets,
    /// optional or more than three fraction digits, or a minute Excel would read as a month) map to <see langword="null"/>.
    /// </summary>
    internal static string? CustomDate(string pattern, CultureInfo culture)
    {
        var dtf = culture.DateTimeFormat;
        var tokens = new List<(Token Kind, string Text)>();
        var literal = new StringBuilder();
        void Literal(string text) => literal.Append(text);
        void Add(Token kind, string text)
        {
            if (literal.Length > 0) { tokens.Add((Token.Literal, literal.ToString())); literal.Clear(); }
            tokens.Add((kind, text));
        }

        bool names = false, hasAmPm = false, has12 = false, has24 = false;
        for (int i = 0; i < pattern.Length;)
        {
            char c = pattern[i];
            int run = 1;
            while (i + run < pattern.Length && pattern[i + run] == c) run++;
            switch (c)
            {
                case 'y':
                    if (run == 1 || run > 4) return null;
                    Add(Token.Year, run == 2 ? "yy" : "yyyy");
                    break;
                case 'M':
                    if (run >= 3) names = true;
                    Add(Token.Month, run switch { 1 => "m", 2 => "mm", 3 => "mmm", _ => "mmmm" });
                    break;
                case 'd':
                    if (run >= 3) names = true;
                    Add(Token.Day, run switch { 1 => "d", 2 => "dd", 3 => "ddd", _ => "dddd" });
                    break;
                case 'h':
                    has12 = true;
                    Add(Token.Hour12, run == 1 ? "h" : "hh");
                    break;
                case 'H':
                    has24 = true;
                    Add(Token.Hour24, run == 1 ? "h" : "hh");
                    break;
                case 'm':
                    Add(Token.Minute, run == 1 ? "m" : "mm");
                    break;
                case 's':
                    Add(Token.Second, run == 1 ? "s" : "ss");
                    break;
                case 'f':
                    if (run > 3) return null;
                    Add(Token.Fraction, new string('0', run));
                    break;
                case 't':
                    if (dtf.AMDesignator != "AM" || dtf.PMDesignator != "PM") return null;
                    hasAmPm = true;
                    Add(Token.AmPm, run == 1 ? "A/P" : "AM/PM");
                    break;
                case 'F' or 'g' or 'z' or 'K':
                    return null;
                case '/':
                    Literal(dtf.DateSeparator);
                    break;
                case ':':
                    Literal(dtf.TimeSeparator);
                    break;
                case '%':
                    run = 1;
                    break;
                case '\\':
                    if (i + 1 >= pattern.Length) return null;
                    Literal(pattern[i + 1].ToString());
                    run = 2;
                    break;
                case '\'' or '"':
                    int end = pattern.IndexOf(c, i + 1);
                    if (end < 0) return null;
                    Literal(pattern.Substring(i + 1, end - i - 1));
                    run = end - i + 1;
                    break;
                default:
                    Literal(new string(c, run));
                    break;
            }

            i += run;
        }

        if (literal.Length > 0) tokens.Add((Token.Literal, literal.ToString()));

        // A pattern with no date or time part (a number format such as "C2" applied to a date) is not a date format:
        // as a format code it would show only its text, so the cell gets the display text instead.
        if (tokens.TrueForAll(static t => t.Kind == Token.Literal)) return null;

        // Excel prints AM/PM with a 12-hour clock and a 24-hour clock without it; .NET decides per specifier.
        if (has12 && (has24 || !hasAmPm)) return null;
        if (has24 && hasAmPm) return null;

        var sb = new StringBuilder();
        if (names && culture.LCID is > 0 and not 127 and not 4096 and < 0x10000)
            sb.Append("[$-").Append(culture.LCID.ToString("X", CultureInfo.InvariantCulture)).Append(']');

        for (int k = 0; k < tokens.Count; k++)
        {
            var (kind, text) = tokens[k];
            switch (kind)
            {
                case Token.Literal:
                    // A fraction needs Excel's own decimal point just before it.
                    if (k + 1 < tokens.Count && tokens[k + 1].Kind == Token.Fraction)
                    {
                        if (!text.EndsWith('.')) return null;
                        if (text.Length > 1) AppendQuoted(sb, text[..^1]);
                        sb.Append('.');
                    }
                    else
                    {
                        AppendQuoted(sb, text);
                    }
                    break;
                case Token.Fraction:
                    if (k == 0 || tokens[k - 1].Kind != Token.Literal || Previous(tokens, k - 1) != Token.Second) return null;
                    sb.Append(text);
                    break;
                case Token.Minute:
                    // Excel reads m as minutes only right after an hour or right before seconds.
                    if (Previous(tokens, k) is not (Token.Hour12 or Token.Hour24) && Next(tokens, k) != Token.Second) return null;
                    sb.Append(text);
                    break;
                case Token.Month:
                    if (text.Length <= 2 && (Previous(tokens, k) is Token.Hour12 or Token.Hour24 || Next(tokens, k) == Token.Second))
                        return null; // Excel would read it as minutes
                    sb.Append(text);
                    break;
                default:
                    sb.Append(text);
                    break;
            }
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    private static Token? Previous(List<(Token Kind, string Text)> tokens, int index)
    {
        for (int k = index - 1; k >= 0; k--)
            if (tokens[k].Kind != Token.Literal) return tokens[k].Kind;
        return null;
    }

    private static Token? Next(List<(Token Kind, string Text)> tokens, int index)
    {
        for (int k = index + 1; k < tokens.Count; k++)
            if (tokens[k].Kind != Token.Literal) return tokens[k].Kind;
        return null;
    }
}
