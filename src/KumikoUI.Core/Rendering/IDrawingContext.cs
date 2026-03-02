namespace KumikoUI.Core.Rendering;

/// <summary>
/// Platform-independent drawing context abstraction.
/// Implementations map these calls to concrete rendering APIs
/// (SkiaSharp SKCanvas, HTML Canvas 2D, etc.).
/// </summary>
public interface IDrawingContext
{
    // ── Rectangles ───────────────────────────────────────────────

    /// <summary>Draws the outline of a rectangle using the specified paint (stroke).</summary>
    /// <param name="rect">The rectangle to draw.</param>
    /// <param name="paint">The paint describing color, stroke width, and anti-alias settings.</param>
    void DrawRect(GridRect rect, GridPaint paint);

    /// <summary>Fills a rectangle with a solid color using the specified paint.</summary>
    /// <param name="rect">The rectangle to fill.</param>
    /// <param name="paint">The paint describing the fill color and anti-alias settings.</param>
    void FillRect(GridRect rect, GridPaint paint);

    /// <summary>Draws the outline of a rounded rectangle using the specified paint (stroke).</summary>
    /// <param name="rect">The bounding rectangle.</param>
    /// <param name="cornerRadius">The radius applied to each corner.</param>
    /// <param name="paint">The paint describing color, stroke width, and anti-alias settings.</param>
    void DrawRoundRect(GridRect rect, float cornerRadius, GridPaint paint);

    /// <summary>Fills a rounded rectangle with a solid color using the specified paint.</summary>
    /// <param name="rect">The bounding rectangle.</param>
    /// <param name="cornerRadius">The radius applied to each corner.</param>
    /// <param name="paint">The paint describing the fill color and anti-alias settings.</param>
    void FillRoundRect(GridRect rect, float cornerRadius, GridPaint paint);

    // ── Lines ────────────────────────────────────────────────────

    /// <summary>Draws a line between two points using the specified paint.</summary>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="paint">The paint describing color, stroke width, and anti-alias settings.</param>
    void DrawLine(float x1, float y1, float x2, float y2, GridPaint paint);

    // ── Text ─────────────────────────────────────────────────────

    /// <summary>Draws a text string at the specified position.</summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="x">The x-coordinate of the text origin.</param>
    /// <param name="y">The y-coordinate of the text baseline.</param>
    /// <param name="paint">The paint describing font, color, and anti-alias settings.</param>
    void DrawText(string text, float x, float y, GridPaint paint);

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
    void DrawTextInRect(string text, GridRect rect, GridPaint paint,
        GridTextAlignment hAlign = GridTextAlignment.Left,
        GridVerticalAlignment vAlign = GridVerticalAlignment.Center,
        bool clip = true);

    // ── Text measuring ───────────────────────────────────────────

    /// <summary>Measures the size of the specified text string using the given paint's font settings.</summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="paint">The paint whose font settings determine text size.</param>
    /// <returns>The width and height of the measured text.</returns>
    GridSize MeasureText(string text, GridPaint paint);

    /// <summary>Get font metrics (ascent, descent, leading) for the given paint settings.</summary>
    GridFontMetrics GetFontMetrics(GridPaint paint);

    // ── Clipping & transforms ────────────────────────────────────

    /// <summary>Restricts subsequent drawing operations to the specified rectangle.</summary>
    /// <param name="rect">The clipping rectangle.</param>
    void ClipRect(GridRect rect);

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
    void DrawImage(object image, GridRect destRect);
}
