using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Defines visual properties for an individual cell.
/// All properties are nullable; a null value means "inherit from grid-level style".
/// </summary>
public sealed class KumikoCellStyle
{
    /// <summary>Background color of the cell.</summary>
    public KumikoColor? BackgroundColor { get; set; }

    /// <summary>Text color of the cell.</summary>
    public KumikoColor? TextColor { get; set; }

    /// <summary>Font for cell text.</summary>
    public KumikoFont? Font { get; set; }

    /// <summary>Cell content padding in pixels.</summary>
    public float? Padding { get; set; }

    /// <summary>Text alignment override.</summary>
    public KumikoTextAlignment? TextAlignment { get; set; }

    /// <summary>
    /// Returns true if any property is set (non-null), meaning this style has overrides.
    /// </summary>
    public bool HasOverrides =>
        BackgroundColor.HasValue || TextColor.HasValue || Font.HasValue ||
        Padding.HasValue || TextAlignment.HasValue;

    /// <summary>
    /// Merge two KumikoCellStyle instances. Values from <paramref name="primary"/> take precedence;
    /// any null properties fall back to <paramref name="fallback"/>.
    /// Returns null when both inputs are null, and the other input itself (not a copy) when one is null.
    /// </summary>
    public static KumikoCellStyle? Merge(KumikoCellStyle? primary, KumikoCellStyle? fallback)
    {
        if (primary == null) return fallback;
        if (fallback == null) return primary;
        return new KumikoCellStyle
        {
            BackgroundColor = primary.BackgroundColor ?? fallback.BackgroundColor,
            TextColor = primary.TextColor ?? fallback.TextColor,
            Font = primary.Font ?? fallback.Font,
            Padding = primary.Padding ?? fallback.Padding,
            TextAlignment = primary.TextAlignment ?? fallback.TextAlignment,
        };
    }
}
