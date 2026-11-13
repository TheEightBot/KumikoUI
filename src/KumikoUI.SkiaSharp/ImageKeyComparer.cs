namespace KumikoUI.SkiaSharp;

/// <summary>
/// Compares <see cref="KumikoImageCache"/> keys without allocating: a <see cref="Uri"/> by its original text, so it
/// equals another <see cref="Uri"/> or a string with the same text, and anything else by its own equality (a byte
/// array or stream by reference).
/// </summary>
internal sealed class ImageKeyComparer : IEqualityComparer<object>
{
    public static ImageKeyComparer Instance { get; } = new();

    public new bool Equals(object? x, object? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        string? xText = x is Uri xUri ? xUri.OriginalString : x as string;
        string? yText = y is Uri yUri ? yUri.OriginalString : y as string;
        if (xText is not null || yText is not null)
            return xText is not null && yText is not null && string.Equals(xText, yText, StringComparison.Ordinal);

        return x.Equals(y);
    }

    public int GetHashCode(object obj) => obj is Uri uri ? uri.OriginalString.GetHashCode() : obj.GetHashCode();
}
