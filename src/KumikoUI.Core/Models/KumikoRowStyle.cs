using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Defines visual properties for an entire row.
/// All properties are nullable; a null value means "inherit from grid-level style".
/// </summary>
public class KumikoRowStyle
{
    /// <summary>Background color of the row.</summary>
    public KumikoColor? BackgroundColor { get; set; }

    /// <summary>Text color for all cells in the row.</summary>
    public KumikoColor? TextColor { get; set; }

    /// <summary>Font for all cells in the row.</summary>
    public KumikoFont? Font { get; set; }

    /// <summary>Row height override in pixels.</summary>
    public float? Height { get; set; }

    /// <summary>
    /// Returns true if any property is set (non-null).
    /// </summary>
    public bool HasOverrides =>
        BackgroundColor.HasValue || TextColor.HasValue || Font != null ||
        Height.HasValue;
}
