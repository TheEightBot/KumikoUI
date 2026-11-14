namespace KumikoUI.Core.Rendering;

/// <summary>
/// Platform-independent drawing context abstraction.
/// Implementations map these calls to concrete rendering APIs
/// (SkiaSharp SKCanvas, HTML Canvas 2D, etc.).
/// </summary>
/// <remarks>
/// The method decides how a shape is painted, not the <see cref="KumikoPaint"/>: <c>Fill*</c> methods fill the
/// interior with the paint's color, <c>Draw*</c> methods stroke the outline with its color and
/// <see cref="KumikoPaint.StrokeWidth"/>, and the text methods draw glyphs with its color and font.
/// </remarks>
public interface IDrawingContext
{
    // ── Rectangles ───────────────────────────────────────────────

    /// <summary>Draws the outline of a rectangle using the specified paint (stroke).</summary>
    /// <param name="rect">The rectangle to draw.</param>
    /// <param name="paint">The paint describing color, stroke width, and anti-alias settings.</param>
    void DrawRect(KumikoRect rect, KumikoPaint paint);

    /// <summary>Fills a rectangle with a solid color using the specified paint.</summary>
    /// <param name="rect">The rectangle to fill.</param>
    /// <param name="paint">The paint describing the fill color and anti-alias settings.</param>
    void FillRect(KumikoRect rect, KumikoPaint paint);

    /// <summary>Draws the outline of a rounded rectangle using the specified paint (stroke).</summary>
    /// <param name="rect">The bounding rectangle.</param>
    /// <param name="cornerRadius">The radius applied to each corner.</param>
    /// <param name="paint">The paint describing color, stroke width, and anti-alias settings.</param>
    void DrawRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint);

    /// <summary>Fills a rounded rectangle with a solid color using the specified paint.</summary>
    /// <param name="rect">The bounding rectangle.</param>
    /// <param name="cornerRadius">The radius applied to each corner.</param>
    /// <param name="paint">The paint describing the fill color and anti-alias settings.</param>
    void FillRoundRect(KumikoRect rect, float cornerRadius, KumikoPaint paint);

    // ── Lines ────────────────────────────────────────────────────

    /// <summary>Draws a line between two points using the specified paint.</summary>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="paint">The paint describing color, stroke width, and anti-alias settings.</param>
    void DrawLine(float x1, float y1, float x2, float y2, KumikoPaint paint);

    // ── Polygons & polylines ─────────────────────────────────────

    /// <summary>
    /// Fills a closed polygon (the last point joins back to the first) with the paint's color, anti-aliased when
    /// the paint asks for it. Used for solid glyphs such as expand/collapse and sort triangles. Fewer than three
    /// points draws nothing.
    /// </summary>
    /// <remarks>
    /// This is a default interface member so existing implementers keep compiling: the default draws the polygon's
    /// outline with <see cref="DrawLine"/>, which is visible but not filled. Override it to fill.
    /// </remarks>
    /// <param name="points">The polygon's vertices, in order. Not retained after the call.</param>
    /// <param name="paint">The paint describing the fill color and anti-alias settings.</param>
    void FillPolygon(ReadOnlySpan<KumikoPoint> points, KumikoPaint paint)
    {
        int n = points.Length;
        if (n < 3) return;
        for (int i = 0; i < n; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % n];
            DrawLine(a.X, a.Y, b.X, b.Y, paint);
        }
    }

    /// <summary>
    /// Strokes an open polyline through the points with round joins and round caps, so the corners of an open
    /// chevron meet cleanly. Fewer than two points draws nothing.
    /// </summary>
    /// <remarks>
    /// This is a default interface member so existing implementers keep compiling: the default strokes one
    /// <see cref="DrawLine"/> per segment (butt caps, no joins). Override it for joined strokes.
    /// </remarks>
    /// <param name="points">The vertices, in order. Not retained after the call.</param>
    /// <param name="paint">The paint describing color, stroke width, and anti-alias settings.</param>
    void DrawPolyline(ReadOnlySpan<KumikoPoint> points, KumikoPaint paint)
    {
        for (int i = 0; i + 1 < points.Length; i++)
            DrawLine(points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y, paint);
    }

    // ── Text ─────────────────────────────────────────────────────

    /// <summary>Draws a text string at the specified position.</summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="x">The x-coordinate of the text origin.</param>
    /// <param name="y">The y-coordinate of the text baseline.</param>
    /// <param name="paint">The paint describing font, color, and anti-alias settings.</param>
    void DrawText(string text, float x, float y, KumikoPaint paint);

    /// <summary>
    /// Draws a text string inside the specified rectangle, with alignment and optional clipping.
    /// Text that exceeds the rectangle width may be truncated with an ellipsis.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="rect">The bounding rectangle for the text.</param>
    /// <param name="paint">The paint describing font, color, and anti-alias settings.</param>
    /// <param name="hAlign">Horizontal text alignment within the rectangle.</param>
    /// <param name="vAlign">Vertical text alignment within the rectangle.</param>
    /// <param name="clip">Whether to clip rendering to the rectangle bounds.</param>
    void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
        KumikoTextAlignment hAlign = KumikoTextAlignment.Left,
        KumikoVerticalAlignment vAlign = KumikoVerticalAlignment.Center,
        bool clip = true);

    /// <summary>
    /// Draws a text string inside the specified rectangle on one line or wrapped onto several, with alignment and
    /// optional clipping. See <see cref="KumikoTextWrap"/> for how each mode lays the text out; the block of lines
    /// is aligned vertically as a whole and each line horizontally on its own. Only as many lines as fit the
    /// rectangle's height are drawn (at least one); when the text needs more, the last drawn line ends with an
    /// ellipsis.
    /// </summary>
    /// <remarks>
    /// This is a default interface member so existing implementers keep compiling. With
    /// <see cref="KumikoTextWrap.None"/> the default calls
    /// <see cref="DrawTextInRect(string, KumikoRect, KumikoPaint, KumikoTextAlignment, KumikoVerticalAlignment, bool)"/>;
    /// otherwise it lays the text out with <see cref="KumikoTextLayout"/>, measuring through
    /// <see cref="MeasureText(string, KumikoPaint)"/>, and draws one
    /// <see cref="DrawTextInRect(string, KumikoRect, KumikoPaint, KumikoTextAlignment, KumikoVerticalAlignment, bool)"/>
    /// per line. That allocates on every call; an implementation used in the render loop should override it and
    /// cache the <see cref="KumikoTextLines"/> it lays out.
    /// </remarks>
    /// <param name="text">The text to draw.</param>
    /// <param name="rect">The bounding rectangle for the text.</param>
    /// <param name="paint">The paint describing font, color, and anti-alias settings.</param>
    /// <param name="hAlign">Horizontal alignment of each line within the rectangle.</param>
    /// <param name="vAlign">Vertical alignment of the block of lines within the rectangle.</param>
    /// <param name="clip">Whether to clip rendering to the rectangle bounds.</param>
    /// <param name="wrap">Whether and where long lines are wrapped.</param>
    void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint,
        KumikoTextAlignment hAlign, KumikoVerticalAlignment vAlign, bool clip, KumikoTextWrap wrap)
    {
        if (wrap == KumikoTextWrap.None)
        {
            DrawTextInRect(text, rect, paint, hAlign, vAlign, clip);
            return;
        }
        if (string.IsNullOrEmpty(text)) return;

        var metrics = GetFontMetrics(paint);
        var lines = KumikoTextLayout.LayOut(text, rect.Width, wrap,
            KumikoTextLayout.GetMaxLines(rect.Height, metrics), MeasurerFor(paint));
        if (lines.Count == 0) return;

        float height = lines.GetHeight(metrics);
        float top = vAlign switch
        {
            KumikoVerticalAlignment.Top => rect.Y,
            KumikoVerticalAlignment.Bottom => rect.Bottom - height,
            _ => rect.Y + (rect.Height - height) / 2,
        };

        if (clip)
        {
            Save();
            ClipRect(rect);
        }
        for (int i = 0; i < lines.Count; i++)
        {
            var lineRect = new KumikoRect(rect.X, top + i * metrics.LineHeight, rect.Width, metrics.TextHeight);
            DrawTextInRect(lines.GetLine(i), lineRect, paint, hAlign, KumikoVerticalAlignment.Top, clip: false);
        }
        if (clip)
            Restore();
    }

    // ── Text measuring ───────────────────────────────────────────

    /// <summary>Measures the size of the specified text string using the given paint's font settings.</summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="paint">The paint whose font settings determine text size.</param>
    /// <returns>The width and height of the measured text.</returns>
    KumikoSize MeasureText(string text, KumikoPaint paint);

    /// <summary>
    /// Measures text as
    /// <see cref="DrawTextInRect(string, KumikoRect, KumikoPaint, KumikoTextAlignment, KumikoVerticalAlignment, bool, KumikoTextWrap)"/>
    /// lays it out in a rectangle <paramref name="maxWidth"/> wide and tall enough for every line: the width of
    /// the widest line as drawn (an ellipsized line with its ellipsis) and the height of all the lines.
    /// </summary>
    /// <remarks>
    /// With <see cref="KumikoTextWrap.None"/> and <see cref="float.PositiveInfinity"/> as
    /// <paramref name="maxWidth"/> this is the natural one-line size of the text (with line breaks shown as
    /// spaces). This is a default interface member so existing implementers keep compiling: the default lays the
    /// text out with <see cref="KumikoTextLayout"/>, measuring through <see cref="MeasureText(string, KumikoPaint)"/>,
    /// and allocates on every call.
    /// </remarks>
    /// <param name="text">The text to measure.</param>
    /// <param name="paint">The paint whose font settings determine text size.</param>
    /// <param name="maxWidth">The available width, in pixels; <see cref="float.PositiveInfinity"/> for no limit.</param>
    /// <param name="wrap">Whether and where long lines are wrapped.</param>
    /// <returns>The width and height of the laid-out text; <see cref="KumikoSize.Empty"/> for empty text.</returns>
    KumikoSize MeasureText(string text, KumikoPaint paint, float maxWidth, KumikoTextWrap wrap)
    {
        if (string.IsNullOrEmpty(text)) return KumikoSize.Empty;
        var lines = KumikoTextLayout.LayOut(text, maxWidth, wrap, int.MaxValue, MeasurerFor(paint));
        return new KumikoSize(lines.Width, lines.GetHeight(GetFontMetrics(paint)));
    }

    /// <summary>Measures spans through <see cref="MeasureText(string, KumikoPaint)"/> for the default members.</summary>
    private KumikoTextMeasurer MeasurerFor(KumikoPaint paint) =>
        span => span.IsEmpty ? 0f : MeasureText(span.ToString(), paint).Width;

    /// <summary>Get font metrics (ascent, descent, leading) for the given paint settings.</summary>
    KumikoFontMetrics GetFontMetrics(KumikoPaint paint);

    // ── Clipping & transforms ────────────────────────────────────

    /// <summary>Restricts subsequent drawing operations to the specified rectangle.</summary>
    /// <param name="rect">The clipping rectangle.</param>
    void ClipRect(KumikoRect rect);

    /// <summary>Saves the current drawing state (clip region and transform matrix) onto a stack.</summary>
    void Save();

    /// <summary>Restores the most recently saved drawing state from the stack.</summary>
    void Restore();

    /// <summary>Applies a translation transform, shifting subsequent drawing operations.</summary>
    /// <param name="dx">The horizontal translation offset.</param>
    /// <param name="dy">The vertical translation offset.</param>
    void Translate(float dx, float dy);

    // ── Images (for icons, checkboxes, etc.) ─────────────────────

    /// <summary>Draws an image (icon, checkbox glyph, etc.) scaled to fit the destination rectangle.</summary>
    /// <param name="image">The platform-specific image object to draw.</param>
    /// <param name="destRect">The destination rectangle the image is drawn into.</param>
    void DrawImage(object image, KumikoRect destRect);
}
