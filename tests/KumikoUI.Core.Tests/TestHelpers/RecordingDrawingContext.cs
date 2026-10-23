using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.TestHelpers;

/// <summary>
/// An <see cref="IDrawingContext"/> that records fill operations in call order so tests can assert
/// what was drawn and in which z-order, without SkiaSharp. Text measures as 8 px per character.
/// </summary>
internal sealed class RecordingDrawingContext : IDrawingContext
{
    /// <summary>A recorded fill, in viewport coordinates after any <see cref="Translate"/> calls.</summary>
    public readonly record struct FillOp(string Kind, GridRect Rect, GridColor Color);

    private readonly Stack<(float X, float Y)> _saved = new();
    private float _tx, _ty;

    public List<FillOp> Fills { get; } = new();

    private GridRect Map(GridRect r) => new(r.X + _tx, r.Y + _ty, r.Width, r.Height);

    public void FillRect(GridRect rect, GridPaint paint) => Fills.Add(new("FillRect", Map(rect), paint.Color));
    public void FillRoundRect(GridRect rect, float cornerRadius, GridPaint paint) => Fills.Add(new("FillRoundRect", Map(rect), paint.Color));
    public void DrawRect(GridRect rect, GridPaint paint) { }
    public void DrawRoundRect(GridRect rect, float cornerRadius, GridPaint paint) { }
    public void DrawLine(float x1, float y1, float x2, float y2, GridPaint paint) { }
    public void DrawText(string text, float x, float y, GridPaint paint) { }
    public void DrawTextInRect(string text, GridRect rect, GridPaint paint,
        GridTextAlignment hAlign = GridTextAlignment.Left,
        GridVerticalAlignment vAlign = GridVerticalAlignment.Center,
        bool clip = true) { }
    public GridSize MeasureText(string text, GridPaint paint) => new(text.Length * 8, 14);
    public GridFontMetrics GetFontMetrics(GridPaint paint) => new(14, 0, 2);
    public void ClipRect(GridRect rect) { }
    public void Save() => _saved.Push((_tx, _ty));
    public void Restore() { if (_saved.Count > 0) (_tx, _ty) = _saved.Pop(); }
    public void Translate(float dx, float dy) { _tx += dx; _ty += dy; }
    public void DrawImage(object image, GridRect destRect) { }
    public void Dispose() { }
}
