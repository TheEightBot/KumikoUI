using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Style settings a host control exposes as bindable properties (grid lines, row drag, alternate row
/// color). Each is nullable: null means "not set, keep what the style or theme says", a value overrides it.
/// <see cref="Apply"/> writes the set values into a <see cref="KumikoStyle"/>; a host calls it whenever one
/// changes and again after the style or theme is replaced, so the explicit settings win over the theme.
/// </summary>
public sealed class KumikoStyleOptions
{
    /// <summary>Whether horizontal grid lines are drawn (<see cref="KumikoStyle.ShowHorizontalGridLines"/>).</summary>
    public bool? ShowHorizontalGridLines { get; set; }

    /// <summary>Whether vertical grid lines are drawn (<see cref="KumikoStyle.ShowVerticalGridLines"/>).</summary>
    public bool? ShowVerticalGridLines { get; set; }

    /// <summary>Whether a row can be dragged from any cell (<see cref="KumikoStyle.AllowRowDragDrop"/>).</summary>
    public bool? AllowRowDragDrop { get; set; }

    /// <summary>Whether each row shows a drag handle (<see cref="KumikoStyle.ShowRowDragHandle"/>).</summary>
    public bool? ShowRowDragHandle { get; set; }

    /// <summary>
    /// Background of alternate rows. A value sets <see cref="KumikoStyle.AlternateRowColor"/> and turns
    /// <see cref="KumikoStyle.AlternateRowBackground"/> on.
    /// </summary>
    public KumikoColor? AlternateRowBackground { get; set; }

    /// <summary>True when at least one setting is set.</summary>
    public bool HasOverrides =>
        ShowHorizontalGridLines.HasValue || ShowVerticalGridLines.HasValue ||
        AllowRowDragDrop.HasValue || ShowRowDragHandle.HasValue || AlternateRowBackground.HasValue;

    /// <summary>Write every setting that is set into <paramref name="style"/>; unset ones leave it untouched.</summary>
    public void Apply(KumikoStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        if (ShowHorizontalGridLines is { } horizontal) style.ShowHorizontalGridLines = horizontal;
        if (ShowVerticalGridLines is { } vertical) style.ShowVerticalGridLines = vertical;
        if (AllowRowDragDrop is { } allowDrag) style.AllowRowDragDrop = allowDrag;
        if (ShowRowDragHandle is { } handle) style.ShowRowDragHandle = handle;
        if (AlternateRowBackground is { } alternate)
        {
            style.AlternateRowColor = alternate;
            style.AlternateRowBackground = true;
        }
    }
}
