using System.Globalization;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Data;

/// <summary>
/// Converts an edited value (usually text) to the type of the property it is written to, the way the
/// grid commits an edit: parsing in the grid's culture, keeping full precision, and reporting a message
/// instead of throwing or silently rounding.
/// </summary>
/// <remarks>
/// <para>Supported target types, and their nullable forms:</para>
/// <list type="bullet">
/// <item>every integral type (whole numbers only, in range);</item>
/// <item><see cref="float"/>, <see cref="double"/> and <see cref="decimal"/> (parsed directly, so a
/// decimal keeps all its digits);</item>
/// <item><see cref="bool"/>, <see cref="char"/>, <see cref="string"/>, <see cref="Guid"/> and enums (by name,
/// ignoring case, or by a defined number);</item>
/// <item><see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="DateOnly"/>,
/// <see cref="TimeOnly"/> and <see cref="TimeSpan"/>, from text or from each other.</item>
/// </list>
/// <para>
/// Blank text becomes null for a nullable target and is an error for a non-nullable value type; a
/// string target keeps the text as typed.
/// </para>
/// </remarks>
public static class KumikoValueConverter
{
    /// <summary>
    /// Convert <paramref name="value"/> to <paramref name="targetType"/>.
    /// </summary>
    /// <param name="value">The edited value: text from a text editor, or a typed value from another editor.</param>
    /// <param name="targetType">The property type, nullable or not.</param>
    /// <param name="culture">The culture to parse and format numbers and dates in.</param>
    /// <param name="result">The converted value when the conversion succeeds.</param>
    /// <param name="error">A message for the user when it fails.</param>
    /// <returns>Whether the value converts.</returns>
    public static bool TryConvert(object? value, Type targetType, CultureInfo culture, out object? result, out string? error) =>
        TryConvert(value, targetType, culture, KumikoStrings.Default, out result, out error);

    /// <summary>
    /// Convert <paramref name="value"/> to <paramref name="targetType"/>, reporting failures with
    /// <paramref name="strings"/>.
    /// </summary>
    /// <param name="value">The edited value: text from a text editor, or a typed value from another editor.</param>
    /// <param name="targetType">The property type, nullable or not.</param>
    /// <param name="culture">The culture to parse and format numbers and dates in.</param>
    /// <param name="strings">The table the failure messages come from.</param>
    /// <param name="result">The converted value when the conversion succeeds.</param>
    /// <param name="error">A message for the user when it fails.</param>
    /// <returns>Whether the value converts.</returns>
    public static bool TryConvert(object? value, Type targetType, CultureInfo culture, KumikoStrings strings, out object? result, out string? error)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(strings);
        result = null;
        error = null;

        var underlying = Nullable.GetUnderlyingType(targetType);
        bool acceptsNull = underlying != null || !targetType.IsValueType;
        var type = underlying ?? targetType;

        if (value is string text && type != typeof(string) && type != typeof(object))
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                if (acceptsNull) return true;
                error = strings.ValueRequired;
                return false;
            }
            return TryParse(text.Trim(), type, culture, strings, out result, out error);
        }

        if (value is null)
        {
            if (acceptsNull) return true;
            error = strings.ValueRequired;
            return false;
        }

        if (type.IsInstanceOfType(value))
        {
            result = value;
            return true;
        }

        if (type == typeof(string))
        {
            result = value is IFormattable formattable ? formattable.ToString(null, culture) : value.ToString();
            return true;
        }

        return TryConvertValue(value, type, culture, strings, out result, out error);
    }

    // ── Text ─────────────────────────────────────────────────────

    private static bool TryParse(string text, Type type, CultureInfo culture, KumikoStrings strings, out object? result, out string? error)
    {
        result = null;
        error = null;

        if (type.IsEnum)
            return TryParseEnum(text, type, culture, strings, out result, out error);

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean:
                if (bool.TryParse(text, out var b)) { result = b; return true; }
                error = strings.EnterBoolean;
                return false;

            case TypeCode.Char:
                if (text.Length == 1) { result = text[0]; return true; }
                error = strings.EnterCharacter;
                return false;

            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
                if (!decimal.TryParse(text, NumberStyles.Number, culture, out var whole))
                {
                    error = strings.EnterNumber;
                    return false;
                }
                return TryToIntegral(whole, type, culture, strings, out result, out error);

            case TypeCode.Single:
                if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var single) &&
                    double.IsFinite(single))
                {
                    if (Math.Abs(single) > float.MaxValue) { error = strings.NumberOutOfRange; return false; }
                    result = (float)single;
                    return true;
                }
                error = strings.EnterNumber;
                return false;

            case TypeCode.Double:
                if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var d) &&
                    double.IsFinite(d))
                {
                    result = d;
                    return true;
                }
                error = strings.EnterNumber;
                return false;

            case TypeCode.Decimal:
                if (decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowExponent, culture, out var m))
                {
                    result = m;
                    return true;
                }
                error = strings.EnterNumber;
                return false;

            case TypeCode.DateTime:
                if (DateTime.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out var dateTime))
                {
                    result = dateTime;
                    return true;
                }
                error = strings.EnterDate;
                return false;
        }

        if (type == typeof(Guid))
        {
            if (Guid.TryParse(text, out var guid)) { result = guid; return true; }
            error = strings.EnterIdentifier;
            return false;
        }

        if (type == typeof(DateOnly))
        {
            if (DateOnly.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out var date))
            {
                result = date;
                return true;
            }
            if (DateTime.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out var withTime))
            {
                result = DateOnly.FromDateTime(withTime); // text with a time of day: keep the date
                return true;
            }
            error = strings.EnterDate;
            return false;
        }

        if (type == typeof(TimeOnly))
        {
            if (TimeOnly.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out var time))
            {
                result = time;
                return true;
            }
            if (TimeSpan.TryParse(text, culture, out var span) && span >= TimeSpan.Zero && span < TimeSpan.FromDays(1))
            {
                result = TimeOnly.FromTimeSpan(span);
                return true;
            }
            error = strings.EnterTime;
            return false;
        }

        if (type == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out var offset))
            {
                result = offset;
                return true;
            }
            error = strings.EnterDate;
            return false;
        }

        if (type == typeof(TimeSpan))
        {
            if (TimeSpan.TryParse(text, culture, out var duration))
            {
                result = duration;
                return true;
            }
            error = strings.EnterDuration;
            return false;
        }

        error = strings.EnterValidValue;
        return false;
    }

    private static bool TryParseEnum(string text, Type type, CultureInfo culture, KumikoStrings strings, out object? result, out string? error)
    {
        if (Enum.TryParse(type, text, ignoreCase: true, out var parsed) && IsAcceptedEnumValue(type, parsed!))
        {
            result = parsed;
            error = null;
            return true;
        }
        result = null;
        error = string.Format(culture, strings.EnterOneOf, string.Join(", ", Enum.GetNames(type)));
        return false;
    }

    private static bool IsAcceptedEnumValue(Type type, object value) =>
        Enum.IsDefined(type, value) || type.IsDefined(typeof(FlagsAttribute), inherit: false);

    // ── Typed values ─────────────────────────────────────────────

    private static bool TryConvertValue(object value, Type type, CultureInfo culture, KumikoStrings strings, out object? result, out string? error)
    {
        result = null;
        error = null;

        if (type.IsEnum)
        {
            if (value is string name) return TryParseEnum(name, type, culture, strings, out result, out error);
            if (Type.GetTypeCode(value.GetType()) is >= TypeCode.SByte and <= TypeCode.UInt64)
            {
                var candidate = Enum.ToObject(type, value);
                if (IsAcceptedEnumValue(type, candidate)) { result = candidate; return true; }
            }
            error = string.Format(culture, strings.EnterOneOf, string.Join(", ", Enum.GetNames(type)));
            return false;
        }

        // Dates and times convert between their representations
        switch (value)
        {
            case DateTime dt when type == typeof(DateOnly): result = DateOnly.FromDateTime(dt); return true;
            case DateTime dt when type == typeof(DateTimeOffset): result = new DateTimeOffset(dt); return true;
            case DateTime dt when type == typeof(TimeOnly): result = TimeOnly.FromDateTime(dt); return true;
            case DateTimeOffset dto when type == typeof(DateTime): result = dto.DateTime; return true;
            case DateTimeOffset dto when type == typeof(DateOnly): result = DateOnly.FromDateTime(dto.DateTime); return true;
            case DateOnly date when type == typeof(DateTime): result = date.ToDateTime(TimeOnly.MinValue); return true;
            case DateOnly date when type == typeof(DateTimeOffset): result = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue)); return true;
            case TimeSpan span when type == typeof(TimeOnly):
                if (span >= TimeSpan.Zero && span < TimeSpan.FromDays(1)) { result = TimeOnly.FromTimeSpan(span); return true; }
                error = strings.EnterTime;
                return false;
            case TimeOnly time when type == typeof(TimeSpan): result = time.ToTimeSpan(); return true;
        }

        var targetCode = Type.GetTypeCode(type);
        if (KumikoUI.Core.Models.KumikoComparer.TryGetNumber(value, out _) && targetCode is >= TypeCode.SByte and <= TypeCode.UInt64)
        {
            decimal number;
            try
            {
                number = Convert.ToDecimal(value, culture);
            }
            catch (OverflowException)
            {
                error = strings.NumberOutOfRange;
                return false;
            }
            return TryToIntegral(number, type, culture, strings, out result, out error);
        }

        if (value is IConvertible && targetCode is not (TypeCode.Object or TypeCode.Empty or TypeCode.DBNull))
        {
            try
            {
                result = Convert.ChangeType(value, type, culture);
                if (result is float f && float.IsInfinity(f) || result is double d && double.IsInfinity(d))
                {
                    result = null;
                    error = strings.NumberOutOfRange;
                    return false;
                }
                return true;
            }
            catch (OverflowException)
            {
                error = strings.NumberOutOfRange;
                return false;
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException)
            {
                error = strings.EnterValidValue;
                return false;
            }
        }

        error = strings.EnterValidValue;
        return false;
    }

    private static bool TryToIntegral(decimal number, Type type, CultureInfo culture, KumikoStrings strings, out object? result, out string? error)
    {
        result = null;
        if (number != decimal.Truncate(number))
        {
            error = strings.EnterWholeNumber;
            return false;
        }

        try
        {
            result = Convert.ChangeType(number, type, culture);
            error = null;
            return true;
        }
        catch (OverflowException)
        {
            error = strings.NumberOutOfRange;
            return false;
        }
    }
}
