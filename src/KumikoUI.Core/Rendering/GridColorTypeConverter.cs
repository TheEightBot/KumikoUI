using System.ComponentModel;
using System.Globalization;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Converts strings to <see cref="GridColor"/> values for XAML and design-time support.
/// </summary>
/// <remarks>
/// Accepted formats:
/// <list type="bullet">
///   <item><description><c>R,G,B</c> — RGB, fully opaque. Example: <c>220,53,69</c></description></item>
///   <item><description><c>R,G,B,A</c> — RGBA. Example: <c>220,53,69,128</c></description></item>
///   <item><description><c>#RRGGBB</c> — hex RGB. Example: <c>#DC3545</c></description></item>
///   <item><description><c>#AARRGGBB</c> — hex ARGB. Example: <c>#80DC3545</c></description></item>
///   <item><description>Named colors: <c>White</c>, <c>Black</c>, <c>Red</c>, <c>Blue</c>,
///     <c>Green</c>, <c>Gray</c>, <c>Transparent</c></description></item>
/// </list>
/// </remarks>
public class GridColorTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string s)
            return base.ConvertFrom(context, culture, value);

        s = s.Trim();

        // Named colors
        switch (s.ToLowerInvariant())
        {
            case "white":       return GridColor.White;
            case "black":       return GridColor.Black;
            case "red":         return GridColor.Red;
            case "blue":        return GridColor.Blue;
            case "green":       return GridColor.Green;
            case "gray":        return GridColor.Gray;
            case "transparent": return GridColor.Transparent;
        }

        // Hex: #RRGGBB or #AARRGGBB
        if (s.StartsWith('#'))
        {
            var hex = s[1..];
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex[0..2], 16);
                byte g = Convert.ToByte(hex[2..4], 16);
                byte b = Convert.ToByte(hex[4..6], 16);
                return new GridColor(r, g, b);
            }
            if (hex.Length == 8)
            {
                byte a = Convert.ToByte(hex[0..2], 16);
                byte r = Convert.ToByte(hex[2..4], 16);
                byte g = Convert.ToByte(hex[4..6], 16);
                byte b = Convert.ToByte(hex[6..8], 16);
                return new GridColor(r, g, b, a);
            }
        }

        // CSV: R,G,B  or  R,G,B,A
        var parts = s.Split(',');
        if (parts.Length is 3 or 4 &&
            byte.TryParse(parts[0].Trim(), out byte red) &&
            byte.TryParse(parts[1].Trim(), out byte green) &&
            byte.TryParse(parts[2].Trim(), out byte blue))
        {
            if (parts.Length == 4 && byte.TryParse(parts[3].Trim(), out byte alpha))
                return new GridColor(red, green, blue, alpha);
            return new GridColor(red, green, blue);
        }

        throw new FormatException(
            $"Cannot convert '{s}' to GridColor. " +
            "Use R,G,B  /  R,G,B,A  /  #RRGGBB  /  #AARRGGBB  /  named color.");
    }
}
