using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Defines visual properties for an individual cell.
/// All properties are nullable; a null value means "inherit from grid-level style".
/// </summary>
public class DataGridCellStyle
{
    /// <summary>Background color of the cell.</summary>
    public GridColor? BackgroundColor { get; set; }

    /// <summary>Text color of the cell.</summary>
    public GridColor? TextColor { get; set; }

    /// <summary>Font for cell text.</summary>
    public GridFont? Font { get; set; }

    /// <summary>Cell content padding in pixels.</summary>
    public float? Padding { get; set; }

    /// <summary>Border color of the cell.</summary>
    public GridColor? BorderColor { get; set; }

    /// <summary>Border width in pixels.</summary>
    public float? BorderWidth { get; set; }

    /// <summary>Text alignment override.</summary>
    public GridTextAlignment? TextAlignment { get; set; }

    /// <summary>
    /// Returns true if any property is set (non-null), meaning this style has overrides.
    /// </summary>
    public bool HasOverrides =>
        BackgroundColor.HasValue || TextColor.HasValue || Font != null ||
        Padding.HasValue || BorderColor.HasValue || BorderWidth.HasValue ||
        TextAlignment.HasValue;

    /// <summary>
    /// Merge two DataGridCellStyle instances. Values from <paramref name="primary"/> take precedence;
    /// any null properties fall back to <paramref name="fallback"/>.
    /// Returns null when both inputs are null.
    /// </summary>
    public static DataGridCellStyle? Merge(DataGridCellStyle? primary, DataGridCellStyle? fallback)
    {
        if (primary == null) return fallback;
        if (fallback == null) return primary;
        return new DataGridCellStyle
        {
            BackgroundColor = primary.BackgroundColor ?? fallback.BackgroundColor,
            TextColor = primary.TextColor ?? fallback.TextColor,
            Font = primary.Font ?? fallback.Font,
            Padding = primary.Padding ?? fallback.Padding,
            BorderColor = primary.BorderColor ?? fallback.BorderColor,
            BorderWidth = primary.BorderWidth ?? fallback.BorderWidth,
            TextAlignment = primary.TextAlignment ?? fallback.TextAlignment,
        };
    }
}
