using System.Globalization;

namespace KumikoUI.Core.Components;

/// <summary>
/// Grapheme-cluster boundaries of a string (what a user sees as one character: an emoji sequence, a
/// letter with combining marks, a surrogate pair).
/// </summary>
internal static class TextElements
{
    /// <summary>Every boundary, ascending, from 0 to <c>text.Length</c>.</summary>
    public static int[] GetBoundaries(string text)
    {
        if (text.Length == 0) return [0];
        var boundaries = new List<int>(text.Length + 1) { 0 };
        int i = 0;
        while (i < text.Length)
        {
            i += StringInfo.GetNextTextElementLength(text, i);
            boundaries.Add(i);
        }
        return boundaries.ToArray();
    }

    /// <summary>The boundary at or before <paramref name="index"/>.</summary>
    public static int Floor(int[] boundaries, int index)
    {
        int k = Array.BinarySearch(boundaries, index);
        return k >= 0 ? boundaries[k] : boundaries[Math.Max(0, ~k - 1)];
    }

    /// <summary>The boundary before <paramref name="index"/> (0 at the start).</summary>
    public static int Previous(int[] boundaries, int index)
    {
        int k = Array.BinarySearch(boundaries, index);
        if (k >= 0) return k > 0 ? boundaries[k - 1] : 0;
        return boundaries[Math.Max(0, ~k - 1)];
    }

    /// <summary>The boundary after <paramref name="index"/> (the end at the end).</summary>
    public static int Next(int[] boundaries, int index)
    {
        int k = Array.BinarySearch(boundaries, index);
        if (k >= 0) return k < boundaries.Length - 1 ? boundaries[k + 1] : boundaries[k];
        return boundaries[Math.Min(boundaries.Length - 1, ~k)];
    }
}
