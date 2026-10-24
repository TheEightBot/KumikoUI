using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.TestHelpers;

/// <summary>The kind of drawing call a <see cref="RecordingDrawingContext"/> recorded.</summary>
internal enum DrawOpKind
{
    FillRect,
    DrawRect,
    FillRoundRect,
    DrawRoundRect,
    Line,
    Text,
    TextInRect,
    Image,
}

/// <summary>
/// One recorded drawing call. Geometry is in viewport coordinates (every active
/// <see cref="IDrawingContext.Translate"/> applied). Paint values are copied at call time, so a
/// paint the renderer reuses or mutates later does not change what was recorded.
/// </summary>
/// <param name="Index">Position in the call sequence (z-order: higher draws on top).</param>
/// <param name="Kind">The drawing call.</param>
/// <param name="Rect">The target rectangle; a line's bounding box; a positioned text's measured box.</param>
/// <param name="Color">Paint colour.</param>
/// <param name="Style">Paint style (fill or stroke).</param>
/// <param name="StrokeWidth">Paint stroke width.</param>
/// <param name="Font">Paint font, if any.</param>
/// <param name="Clip">
/// Intersection of every active <see cref="IDrawingContext.ClipRect"/>; null when unclipped. A
/// <see cref="DrawOpKind.TextInRect"/> call's own rectangle clip is not folded in (see <see cref="ClipsToRect"/>).
/// </param>
/// <param name="Text">The text, for text calls.</param>
/// <param name="HAlign">Horizontal alignment, for <see cref="DrawOpKind.TextInRect"/>.</param>
/// <param name="ClipsToRect">Whether a <see cref="DrawOpKind.TextInRect"/> call clips to its own rectangle.</param>
/// <param name="X1">Line start X (viewport).</param>
/// <param name="Y1">Line start Y (viewport).</param>
/// <param name="X2">Line end X (viewport).</param>
/// <param name="Y2">Line end Y (viewport).</param>
internal sealed record DrawOp(
    int Index,
    DrawOpKind Kind,
    GridRect Rect,
    GridColor Color,
    PaintStyle Style,
    float StrokeWidth,
    GridFont? Font,
    GridRect? Clip,
    string? Text = null,
    GridTextAlignment HAlign = GridTextAlignment.Left,
    bool ClipsToRect = false,
    float X1 = 0, float Y1 = 0, float X2 = 0, float Y2 = 0)
{
    /// <summary>
    /// The part of <see cref="Rect"/> that can reach the screen: <see cref="Rect"/> intersected with
    /// <see cref="Clip"/>. Null when the op is clipped away entirely.
    /// </summary>
    public GridRect? Visible => Clip is { } clip ? RectMath.Intersect(Rect, clip) : Rect;

    public bool IsText => Kind is DrawOpKind.Text or DrawOpKind.TextInRect;

    public override string ToString() =>
        $"#{Index} {Kind} {Text} ({Rect.X:0.#},{Rect.Y:0.#} {Rect.Width:0.#}x{Rect.Height:0.#}) {Color}" +
        (Clip is { } c ? $" clip({c.X:0.#},{c.Y:0.#} {c.Width:0.#}x{c.Height:0.#})" : "");
}

/// <summary>Rectangle helpers for clip assertions.</summary>
internal static class RectMath
{
    /// <summary>The overlap of two rectangles, or null when they do not overlap (touching edges do not count).</summary>
    public static GridRect? Intersect(GridRect a, GridRect b)
    {
        float left = Math.Max(a.Left, b.Left);
        float top = Math.Max(a.Top, b.Top);
        float right = Math.Min(a.Right, b.Right);
        float bottom = Math.Min(a.Bottom, b.Bottom);
        // Lines have a zero-width or zero-height bounding box; keep them when they lie inside the clip.
        if (right < left || bottom < top) return null;
        if ((right <= left && a.Width > 0) || (bottom <= top && a.Height > 0)) return null;
        return new GridRect(left, top, right - left, bottom - top);
    }

    /// <summary>The horizontal overlap of a rectangle with the band [left, right).</summary>
    public static float HorizontalOverlap(GridRect r, float left, float right) =>
        Math.Max(0, Math.Min(r.Right, right) - Math.Max(r.Left, left));
}

/// <summary>
/// An <see cref="IDrawingContext"/> that records every drawing call, with the clip and translation
/// in effect, so tests can assert what was drawn, where, in which z-order and what reached the
/// screen — without SkiaSharp. Text measures monospace: <see cref="CharWidth"/> px per character,
/// <see cref="TextHeight"/> px tall.
/// </summary>
internal sealed class RecordingDrawingContext : IDrawingContext
{
    public const float CharWidth = 8f;
    public const float TextHeight = 14f;

    private readonly Stack<(float Tx, float Ty, GridRect? Clip)> _saved = new();
    private float _tx, _ty;
    private GridRect? _clip;

    /// <summary>Every recorded call, in call order.</summary>
    public List<DrawOp> Ops { get; } = new();

    /// <summary>Highest <see cref="Save"/> nesting reached.</summary>
    public int MaxSaveDepth { get; private set; }

    /// <summary><see cref="Restore"/> calls without a matching <see cref="Save"/>.</summary>
    public int UnbalancedRestores { get; private set; }

    /// <summary>Current <see cref="Save"/> nesting; zero after a balanced frame.</summary>
    public int SaveDepth => _saved.Count;

    // ── Queries ───────────────────────────────────────────────────

    public IEnumerable<DrawOp> Fills => Ops.Where(o => o.Kind is DrawOpKind.FillRect or DrawOpKind.FillRoundRect);
    public IEnumerable<DrawOp> Lines => Ops.Where(o => o.Kind == DrawOpKind.Line);
    public IEnumerable<DrawOp> Texts => Ops.Where(o => o.IsText);

    /// <summary>Text ops whose text equals <paramref name="text"/>.</summary>
    public IEnumerable<DrawOp> TextsEqual(string text) => Texts.Where(o => o.Text == text);

    /// <summary>The single text op with this exact text; fails when there is none or several.</summary>
    public DrawOp SingleText(string text)
    {
        var matches = TextsEqual(text).ToList();
        Assert.True(matches.Count == 1,
            $"expected one '{text}' text op, found {matches.Count}:{Environment.NewLine}{string.Join(Environment.NewLine, matches)}");
        return matches[0];
    }

    // ── IDrawingContext ───────────────────────────────────────────

    private GridRect Map(GridRect r) => new(r.X + _tx, r.Y + _ty, r.Width, r.Height);

    private void Record(DrawOpKind kind, GridRect rect, GridPaint paint, string? text = null,
        GridTextAlignment hAlign = GridTextAlignment.Left, bool clipsToRect = false,
        float x1 = 0, float y1 = 0, float x2 = 0, float y2 = 0)
    {
        Ops.Add(new DrawOp(Ops.Count, kind, rect, paint.Color, paint.Style, paint.StrokeWidth, paint.Font, _clip,
            text, hAlign, clipsToRect, x1, y1, x2, y2));
    }

    public void FillRect(GridRect rect, GridPaint paint) => Record(DrawOpKind.FillRect, Map(rect), paint);
    public void DrawRect(GridRect rect, GridPaint paint) => Record(DrawOpKind.DrawRect, Map(rect), paint);
    public void FillRoundRect(GridRect rect, float cornerRadius, GridPaint paint) => Record(DrawOpKind.FillRoundRect, Map(rect), paint);
    public void DrawRoundRect(GridRect rect, float cornerRadius, GridPaint paint) => Record(DrawOpKind.DrawRoundRect, Map(rect), paint);

    public void DrawLine(float x1, float y1, float x2, float y2, GridPaint paint)
    {
        x1 += _tx; x2 += _tx; y1 += _ty; y2 += _ty;
        var box = new GridRect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        Record(DrawOpKind.Line, box, paint, x1: x1, y1: y1, x2: x2, y2: y2);
    }

    public void DrawText(string text, float x, float y, GridPaint paint)
    {
        var size = MeasureText(text, paint);
        // y is the baseline; the measured box sits above it.
        Record(DrawOpKind.Text, Map(new GridRect(x, y - size.Height, size.Width, size.Height)), paint, text);
    }

    public void DrawTextInRect(string text, GridRect rect, GridPaint paint,
        GridTextAlignment hAlign = GridTextAlignment.Left,
        GridVerticalAlignment vAlign = GridVerticalAlignment.Center,
        bool clip = true) =>
        Record(DrawOpKind.TextInRect, Map(rect), paint, text, hAlign, clip);

    public GridSize MeasureText(string text, GridPaint paint) => new(text.Length * CharWidth, TextHeight);

    public GridFontMetrics GetFontMetrics(GridPaint paint) => new(-11, 3, 2);

    public void ClipRect(GridRect rect)
    {
        var mapped = Map(rect);
        _clip = _clip is { } c ? RectMath.Intersect(c, mapped) ?? GridRect.Empty : mapped;
    }

    public void Save()
    {
        _saved.Push((_tx, _ty, _clip));
        MaxSaveDepth = Math.Max(MaxSaveDepth, _saved.Count);
    }

    public void Restore()
    {
        if (_saved.Count == 0) { UnbalancedRestores++; return; }
        (_tx, _ty, _clip) = _saved.Pop();
    }

    public void Translate(float dx, float dy) { _tx += dx; _ty += dy; }

    public void DrawImage(object image, GridRect destRect) =>
        Record(DrawOpKind.Image, Map(destRect), new GridPaint());
}
