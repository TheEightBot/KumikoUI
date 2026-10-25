using System.ComponentModel;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Platform-independent color representation using RGBA values.
/// </summary>
[TypeConverter(typeof(KumikoColorTypeConverter))]
public readonly struct KumikoColor : IEquatable<KumikoColor>
{
    /// <summary>Red component (0–255).</summary>
    public byte R { get; }

    /// <summary>Green component (0–255).</summary>
    public byte G { get; }

    /// <summary>Blue component (0–255).</summary>
    public byte B { get; }

    /// <summary>Alpha component (0 = transparent, 255 = opaque).</summary>
    public byte A { get; }

    public KumikoColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Returns a copy of this color with the specified alpha value.</summary>
    public KumikoColor WithAlpha(byte alpha) => new(R, G, B, alpha);

    /// <summary>Creates a color from ARGB byte values.</summary>
    public static KumikoColor FromArgb(byte a, byte r, byte g, byte b) => new(r, g, b, a);

    /// <summary>Creates a color from RGB byte values (fully opaque).</summary>
    public static KumikoColor FromRgb(byte r, byte g, byte b) => new(r, g, b);

    // Common colors
    /// <summary>Fully transparent (alpha 0).</summary>
    public static KumikoColor Transparent => new(0, 0, 0, 0);

    /// <summary>Black (#000000).</summary>
    public static KumikoColor Black => new(0, 0, 0);

    /// <summary>White (#FFFFFF).</summary>
    public static KumikoColor White => new(255, 255, 255);

    /// <summary>Light gray (#D3D3D3).</summary>
    public static KumikoColor LightGray => new(211, 211, 211);

    /// <summary>Gray (#808080).</summary>
    public static KumikoColor Gray => new(128, 128, 128);

    /// <summary>Dark gray (#404040).</summary>
    public static KumikoColor DarkGray => new(64, 64, 64);

    /// <summary>Red (#FF0000).</summary>
    public static KumikoColor Red => new(255, 0, 0);

    /// <summary>Blue (#0000FF).</summary>
    public static KumikoColor Blue => new(0, 0, 255);

    /// <summary>Green (#008000).</summary>
    public static KumikoColor Green => new(0, 128, 0);

    /// <summary>Cornflower blue (#6495ED).</summary>
    public static KumikoColor CornflowerBlue => new(100, 149, 237);

    /// <summary>White smoke (#F5F5F5).</summary>
    public static KumikoColor WhiteSmoke => new(245, 245, 245);

    /// <summary>Dodger blue (#1E90FF).</summary>
    public static KumikoColor DodgerBlue => new(30, 144, 255);

    /// <summary>Light blue (#ADD8E6).</summary>
    public static KumikoColor LightBlue => new(173, 216, 230);

    /// <summary>Dark blue (#00008B).</summary>
    public static KumikoColor DarkBlue => new(0, 0, 139);

    public bool Equals(KumikoColor other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is KumikoColor c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(KumikoColor left, KumikoColor right) => left.Equals(right);
    public static bool operator !=(KumikoColor left, KumikoColor right) => !left.Equals(right);
    public override string ToString() => $"KumikoColor({R}, {G}, {B}, {A})";

    /// <summary>Pack ARGB into a single uint for use as a cache key.</summary>
    public uint ToUint32() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;
}
