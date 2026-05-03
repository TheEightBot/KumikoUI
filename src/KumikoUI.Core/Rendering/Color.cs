using System.ComponentModel;
using System.Globalization;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Platform-independent color representation using RGBA values.
/// </summary>
[TypeConverter(typeof(GridColorTypeConverter))]
public readonly struct GridColor : IEquatable<GridColor>
{
    /// <summary>Red component (0–255).</summary>
    public byte R { get; }

    /// <summary>Green component (0–255).</summary>
    public byte G { get; }

    /// <summary>Blue component (0–255).</summary>
    public byte B { get; }

    /// <summary>Alpha component (0 = transparent, 255 = opaque).</summary>
    public byte A { get; }

    public GridColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Returns a copy of this color with the specified alpha value.</summary>
    public GridColor WithAlpha(byte alpha) => new(R, G, B, alpha);

    /// <summary>Creates a color from ARGB byte values.</summary>
    public static GridColor FromArgb(byte a, byte r, byte g, byte b) => new(r, g, b, a);

    /// <summary>Creates a color from RGB byte values (fully opaque).</summary>
    public static GridColor FromRgb(byte r, byte g, byte b) => new(r, g, b);

    // Common colors
    /// <summary>Fully transparent (alpha 0).</summary>
    public static GridColor Transparent => new(0, 0, 0, 0);

    /// <summary>Black (#000000).</summary>
    public static GridColor Black => new(0, 0, 0);

    /// <summary>White (#FFFFFF).</summary>
    public static GridColor White => new(255, 255, 255);

    /// <summary>Light gray (#D3D3D3).</summary>
    public static GridColor LightGray => new(211, 211, 211);

    /// <summary>Gray (#808080).</summary>
    public static GridColor Gray => new(128, 128, 128);

    /// <summary>Dark gray (#404040).</summary>
    public static GridColor DarkGray => new(64, 64, 64);

    /// <summary>Red (#FF0000).</summary>
    public static GridColor Red => new(255, 0, 0);

    /// <summary>Blue (#0000FF).</summary>
    public static GridColor Blue => new(0, 0, 255);

    /// <summary>Green (#008000).</summary>
    public static GridColor Green => new(0, 128, 0);

    /// <summary>Cornflower blue (#6495ED).</summary>
    public static GridColor CornflowerBlue => new(100, 149, 237);

    /// <summary>White smoke (#F5F5F5).</summary>
    public static GridColor WhiteSmoke => new(245, 245, 245);

    /// <summary>Dodger blue (#1E90FF).</summary>
    public static GridColor DodgerBlue => new(30, 144, 255);

    /// <summary>Light blue (#ADD8E6).</summary>
    public static GridColor LightBlue => new(173, 216, 230);

    /// <summary>Dark blue (#00008B).</summary>
    public static GridColor DarkBlue => new(0, 0, 139);

    public bool Equals(GridColor other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is GridColor c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(GridColor left, GridColor right) => left.Equals(right);
    public static bool operator !=(GridColor left, GridColor right) => !left.Equals(right);
    public override string ToString() => $"GridColor({R}, {G}, {B}, {A})";

    /// <summary>Pack ARGB into a single uint for use as a cache key.</summary>
    public uint ToUint32() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;
}

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
        if (parts.Length is 3 or 4)
        {
            if (byte.TryParse(parts[0].Trim(), out byte r) &&
                byte.TryParse(parts[1].Trim(), out byte g) &&
                byte.TryParse(parts[2].Trim(), out byte b))
            {
                if (parts.Length == 4 && byte.TryParse(parts[3].Trim(), out byte a))
                    return new GridColor(r, g, b, a);
                return new GridColor(r, g, b);
            }
        }

        throw new FormatException(
            $"Cannot convert '{s}' to GridColor. " +
            "Use R,G,B  /  R,G,B,A  /  #RRGGBB  /  #AARRGGBB  /  named color.");
    }
}
