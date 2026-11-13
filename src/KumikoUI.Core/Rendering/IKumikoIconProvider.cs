namespace KumikoUI.Core.Rendering;

/// <summary>
/// Draws the grid's built-in glyphs (<see cref="KumikoIcon"/>). Set one on
/// <see cref="Models.KumikoStyle.IconProvider"/> to replace the icon set of a grid and of the editors and popups it
/// opens; a drawn component used on its own takes one through <see cref="Components.DrawnComponent.IconProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Built-in implementations: <see cref="KumikoIconProvider"/> (vector glyphs, the default) and
/// <see cref="KumikoFontIconProvider"/> (glyphs of an icon font). Implement this interface, or derive from
/// <see cref="KumikoIconProvider"/> and override <see cref="KumikoIconProvider.DrawIcon"/>, to draw some or all
/// icons yourself.
/// </para>
/// <para>
/// <see cref="DrawIcon"/> is called on the render thread for every visible glyph in every frame, so it should not
/// allocate: keep paints, fonts and glyph strings in fields and reuse them. Hit regions do not depend on the
/// provider: a glyph is drawn into the slot the grid reserves for it and taps on the slot work whatever is drawn.
/// </para>
/// </remarks>
public interface IKumikoIconProvider
{
    /// <summary>Draws <paramref name="icon"/> centered in <paramref name="bounds"/>.</summary>
    /// <param name="ctx">The drawing context of the frame.</param>
    /// <param name="icon">The glyph to draw. Draw nothing for a value the provider does not know.</param>
    /// <param name="bounds">
    /// The slot the glyph belongs in. The glyph should be centered in it and, apart from anti-aliasing, stay inside
    /// it; the slot's size scales with the part of the grid it belongs to, so the glyph should scale with it too.
    /// </param>
    /// <param name="paint">
    /// The slot's paint: <see cref="KumikoPaint.Color"/> is the glyph color (from the style or theme) and
    /// <see cref="KumikoPaint.StrokeWidth"/> the line weight the slot asks for, which a glyph drawn with lines can
    /// use. Read it; do not change it or keep it after the call.
    /// </param>
    void DrawIcon(IDrawingContext ctx, KumikoIcon icon, KumikoRect bounds, KumikoPaint paint);
}
