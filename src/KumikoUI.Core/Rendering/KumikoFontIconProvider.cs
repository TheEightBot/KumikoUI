namespace KumikoUI.Core.Rendering;

/// <summary>
/// Draws icons as glyphs of an icon font: each <see cref="KumikoIcon"/> maps to a code point of the font, and the
/// glyph is drawn in the icon's color, sized to its slot and centered in it. Icons without a code point are drawn by
/// <see cref="Fallback"/>.
/// </summary>
/// <remarks>
/// <para>
/// The font is looked up by its family name like any other <see cref="KumikoFont"/>, so it must be available to the
/// drawing backend: with SkiaSharp, register it with <c>SkiaFontRegistrar</c> (or <c>KumikoFonts</c> on Uno) before
/// the grid draws. The font is not part of KumikoUI; the app ships it and follows its license.
/// </para>
/// <para>
/// Drawing allocates nothing: the glyph strings are created when a code point is set, and one paint and font are
/// reused for every glyph. Because of that reuse an instance must be used from one thread at a time, which is the
/// case for grids drawn on the same UI thread.
/// </para>
/// </remarks>
public class KumikoFontIconProvider : IKumikoIconProvider
{
    private static readonly int s_iconCount = Enum.GetValues<KumikoIcon>().Length;

    private readonly string?[] _glyphs = new string?[s_iconCount];
    private KumikoFont _font;
    private readonly KumikoPaint _paint;

    /// <summary>Creates a provider for the icon font <paramref name="fontFamily"/> with no glyphs mapped yet.</summary>
    /// <param name="fontFamily">The family name the font is registered under.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fontFamily"/> is null, empty or white space.</exception>
    public KumikoFontIconProvider(string fontFamily)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFamily);
        FontFamily = fontFamily;
        _font = new KumikoFont(fontFamily, 12f);
        _paint = new KumikoPaint { Font = _font, IsAntiAlias = true };
    }

    /// <summary>The family name of the icon font.</summary>
    public string FontFamily { get; }

    /// <summary>
    /// The font size of a glyph relative to the shorter side of its slot. Icon fonts usually leave a margin around
    /// each glyph inside its em square, so a value above 1 draws them about as large as the built-in glyphs.
    /// Default: 1.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a positive finite number.</exception>
    public float Scale
    {
        get;
        set
        {
            if (!float.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The scale must be a positive finite number.");
            field = value;
        }
    } = 1f;

    /// <summary>
    /// The provider that draws the icons this one has no code point for, or <c>null</c> to leave them out.
    /// Default: <see cref="KumikoIconProvider.Default"/>, so a partial map keeps the built-in glyphs for the rest.
    /// </summary>
    public IKumikoIconProvider? Fallback { get; set; } = KumikoIconProvider.Default;

    /// <summary>Maps <paramref name="icon"/> to the glyph at <paramref name="codePoint"/> of the font.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="codePoint">The Unicode code point of the glyph, for example <c>0xE5CF</c>.</param>
    /// <returns>This provider, so calls can be chained.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="icon"/> is not a <see cref="KumikoIcon"/> member, or <paramref name="codePoint"/>
    /// is not a valid code point (negative, above U+10FFFF or a surrogate).
    /// </exception>
    public KumikoFontIconProvider SetGlyph(KumikoIcon icon, int codePoint)
    {
        int index = IndexOf(icon);
        _glyphs[index] = char.ConvertFromUtf32(codePoint);
        return this;
    }

    /// <summary>Removes the code point of <paramref name="icon"/>, so <see cref="Fallback"/> draws it.</summary>
    /// <param name="icon">The icon.</param>
    /// <returns><c>true</c> when the icon had a code point.</returns>
    public bool RemoveGlyph(KumikoIcon icon)
    {
        if ((uint)icon >= (uint)_glyphs.Length || _glyphs[(int)icon] is null) return false;
        _glyphs[(int)icon] = null;
        return true;
    }

    /// <summary>Gets the code point <paramref name="icon"/> is mapped to.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="codePoint">The code point, or 0 when the icon has none.</param>
    /// <returns><c>true</c> when the icon has a code point.</returns>
    public bool TryGetGlyph(KumikoIcon icon, out int codePoint)
    {
        string? glyph = (uint)icon < (uint)_glyphs.Length ? _glyphs[(int)icon] : null;
        codePoint = glyph is null ? 0 : char.ConvertToUtf32(glyph, 0);
        return glyph is not null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The glyph is drawn with the font at <see cref="Scale"/> times the shorter side of <paramref name="bounds"/>,
    /// centered horizontally on its advance width and vertically on the font's ascent and descent, in the color of
    /// <paramref name="paint"/>.
    /// </remarks>
    public virtual void DrawIcon(IDrawingContext ctx, KumikoIcon icon, KumikoRect bounds, KumikoPaint paint)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(paint);

        string? glyph = (uint)icon < (uint)_glyphs.Length ? _glyphs[(int)icon] : null;
        if (glyph is null)
        {
            Fallback?.DrawIcon(ctx, icon, bounds, paint);
            return;
        }

        float size = Math.Min(bounds.Width, bounds.Height) * Scale;
        if (float.IsNaN(size) || size <= 0) return;

        _font = _font with { Size = size };
        _paint.Font = _font;
        _paint.Color = paint.Color;
        _paint.IsAntiAlias = paint.IsAntiAlias;

        float width = ctx.MeasureText(glyph, _paint).Width;
        var metrics = ctx.GetFontMetrics(_paint);
        float x = bounds.X + (bounds.Width - width) / 2;
        float baseline = bounds.Y + bounds.Height / 2 - (metrics.Ascent + metrics.Descent) / 2;
        ctx.DrawText(glyph, x, baseline, _paint);
    }

    /// <summary>
    /// Creates a provider for <see href="https://fonts.google.com/icons">Material Icons</see> (the Regular style,
    /// <c>MaterialIcons-Regular.ttf</c>) with a glyph for every <see cref="KumikoIcon"/>.
    /// </summary>
    /// <remarks>
    /// The code points come from Google's <c>MaterialIcons-Regular.codepoints</c> file: <c>expand_more</c> and
    /// <c>chevron_right</c> for the group headers, <c>arrow_upward</c> and <c>arrow_downward</c> for sorting,
    /// <c>filter_list</c> and <c>filter_alt</c> for the filter button, <c>close</c>, <c>drag_indicator</c>,
    /// <c>check</c>, <c>remove</c>, <c>arrow_drop_down</c> and <c>arrow_drop_up</c>, <c>add</c>,
    /// <c>calendar_today</c>, <c>chevron_left</c>, <c>expand_less</c>. The font is licensed under the Apache License
    /// 2.0; ship it with the app and register it under <paramref name="fontFamily"/>. <see cref="Scale"/> is set
    /// to 1.25 to make up for the font's margin around each glyph.
    /// </remarks>
    /// <param name="fontFamily">The family name the font is registered under. Default: <c>"MaterialIcons"</c>.</param>
    /// <returns>A new provider; change its glyphs or scale freely.</returns>
    public static KumikoFontIconProvider CreateMaterialIcons(string fontFamily = "MaterialIcons") =>
        new KumikoFontIconProvider(fontFamily) { Scale = 1.25f }
            .SetGlyph(KumikoIcon.GroupExpanded, 0xE5CF)      // expand_more
            .SetGlyph(KumikoIcon.GroupCollapsed, 0xE5CC)     // chevron_right
            .SetGlyph(KumikoIcon.SortAscending, 0xE5D8)      // arrow_upward
            .SetGlyph(KumikoIcon.SortDescending, 0xE5DB)     // arrow_downward
            .SetGlyph(KumikoIcon.Filter, 0xE152)             // filter_list
            .SetGlyph(KumikoIcon.FilterActive, 0xEF4F)       // filter_alt
            .SetGlyph(KumikoIcon.Remove, 0xE5CD)             // close
            .SetGlyph(KumikoIcon.GroupSeparator, 0xE5CC)     // chevron_right
            .SetGlyph(KumikoIcon.DragHandle, 0xE945)         // drag_indicator
            .SetGlyph(KumikoIcon.Check, 0xE5CA)              // check
            .SetGlyph(KumikoIcon.Indeterminate, 0xE15B)      // remove
            .SetGlyph(KumikoIcon.DropDown, 0xE5C5)           // arrow_drop_down
            .SetGlyph(KumikoIcon.DropDownOpen, 0xE5C7)       // arrow_drop_up
            .SetGlyph(KumikoIcon.Increment, 0xE145)          // add
            .SetGlyph(KumikoIcon.Decrement, 0xE15B)          // remove
            .SetGlyph(KumikoIcon.Calendar, 0xE935)           // calendar_today
            .SetGlyph(KumikoIcon.CalendarPrevious, 0xE5CB)   // chevron_left
            .SetGlyph(KumikoIcon.CalendarNext, 0xE5CC)       // chevron_right
            .SetGlyph(KumikoIcon.SpinUp, 0xE5CE)             // expand_less
            .SetGlyph(KumikoIcon.SpinDown, 0xE5CF);          // expand_more

    private int IndexOf(KumikoIcon icon)
    {
        if ((uint)icon >= (uint)_glyphs.Length)
            throw new ArgumentOutOfRangeException(nameof(icon), icon, "Not a KumikoIcon member.");
        return (int)icon;
    }
}
