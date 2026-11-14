using KumikoUI.Core.Rendering;
using SkiaSharp;

namespace KumikoUI.Maui.Rendering;

/// <summary>
/// A copy of the last full frame, so a caret blink redraws only the area around the caret (#71). Neither SkiaSharp
/// view can repaint a sub-rectangle, so a blink frame copies this image over the whole surface and then draws the
/// grid clipped to the editor (or popup) area, where Skia skips every draw call outside the clip.
/// </summary>
/// <remarks>
/// Only CPU-rasterized frames are kept: there the rasterization of the whole surface is what a blink costs, and a
/// snapshot is a plain pixel copy. A GPU frame rasterizes on the GPU and its back buffer may not be readable, so
/// the GPU surfaces redraw the whole frame on a blink. The kept pixels are a copy this type owns (see
/// <see cref="Capture"/>). The copy is taken on the first blink after a change (not on
/// every frame), and any other redraw drops it, so it never shows stale content.
/// </remarks>
internal sealed class KumikoCaretFrame : IDisposable
{
    /// <summary>Device pixels added around the caret area, for antialiased edges and focus outlines.</summary>
    public const int MarginPixels = 4;

    private SKBitmap? _pixels;
    private SKImage? _image; // over _pixels' memory, not a copy
    private bool _hasFrame;
    private SKPaint? _copyPaint;

    /// <summary>Whether a frame is kept.</summary>
    public bool HasFrame => _hasFrame;

    /// <summary>
    /// Keeps a copy of what <paramref name="surface"/> shows now. Does nothing for a GPU surface (see remarks).
    /// </summary>
    /// <remarks>
    /// The pixels are read into a bitmap this frame owns. A <c>Snapshot()</c> of an unchanged surface is the
    /// surface's cached image, and SkiaSharp hands out one wrapper per native image, so anyone else who took a
    /// snapshot and disposed it would free the kept frame under the grid.
    /// </remarks>
    /// <param name="surface">The surface the frame was drawn on.</param>
    /// <param name="info">The surface's size and pixel format.</param>
    public void Capture(SKSurface surface, SKImageInfo info)
    {
        _hasFrame = false;
        _image?.Dispose(); // before the pixels it reads are replaced
        _image = null;
        if (surface.Context is not null || info.Width <= 0 || info.Height <= 0 || info.ColorType == SKColorType.Unknown)
            return;
        if (_pixels is null || _pixels.Info != info)
        {
            _pixels?.Dispose();
            _pixels = new SKBitmap(info);
        }
        if (!surface.ReadPixels(_pixels.Info, _pixels.GetPixels(), _pixels.RowBytes, 0, 0)) return;
        using var pixmap = _pixels.PeekPixels();
        _image = SKImage.FromPixels(pixmap);
        _hasFrame = _image is not null;
    }

    /// <summary>
    /// Copies the kept frame onto <paramref name="canvas"/> (identity transform, device pixels), replacing every
    /// pixel. False, drawing nothing, when no frame of <paramref name="info"/>'s size is kept.
    /// </summary>
    public bool TryRestore(SKCanvas canvas, SKImageInfo info)
    {
        if (!_hasFrame || _image is null || _image.Width != info.Width || _image.Height != info.Height) return false;
        _copyPaint ??= new SKPaint { BlendMode = SKBlendMode.Src };
        canvas.DrawImage(_image, 0, 0, SKSamplingOptions.Default, _copyPaint);
        return true;
    }

    /// <summary>Drops the kept frame and its pixels.</summary>
    public void Clear()
    {
        _hasFrame = false;
        _image?.Dispose(); // before the pixels it reads
        _image = null;
        _pixels?.Dispose();
        _pixels = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Clear();
        _copyPaint?.Dispose();
        _copyPaint = null;
    }

    /// <summary>
    /// The device-pixel rectangle to redraw for <paramref name="area"/> (logical units): scaled by
    /// <paramref name="scale"/>, rounded out to whole pixels, grown by <see cref="MarginPixels"/> and kept inside the
    /// <paramref name="width"/> × <paramref name="height"/> surface. Empty when nothing of it is on the surface.
    /// </summary>
    public static SKRectI ToDeviceClip(KumikoRect area, float scale, int width, int height)
    {
        if (!(scale > 0) || !float.IsFinite(scale) || area.Width <= 0 || area.Height <= 0) return SKRectI.Empty;

        int left = Math.Max(0, (int)MathF.Floor(area.Left * scale) - MarginPixels);
        int top = Math.Max(0, (int)MathF.Floor(area.Top * scale) - MarginPixels);
        int right = Math.Min(width, (int)MathF.Ceiling(area.Right * scale) + MarginPixels);
        int bottom = Math.Min(height, (int)MathF.Ceiling(area.Bottom * scale) + MarginPixels);
        return right > left && bottom > top ? new SKRectI(left, top, right, bottom) : SKRectI.Empty;
    }

    /// <summary>The smallest rectangle holding both <paramref name="a"/> and <paramref name="b"/>; an empty one is ignored.</summary>
    public static KumikoRect Union(KumikoRect a, KumikoRect b)
    {
        if (a.Width <= 0 || a.Height <= 0) return b;
        if (b.Width <= 0 || b.Height <= 0) return a;
        float left = MathF.Min(a.Left, b.Left), top = MathF.Min(a.Top, b.Top);
        return new KumikoRect(left, top, MathF.Max(a.Right, b.Right) - left, MathF.Max(a.Bottom, b.Bottom) - top);
    }
}
