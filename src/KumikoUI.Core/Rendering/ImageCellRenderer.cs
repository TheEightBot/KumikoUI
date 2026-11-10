using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders images in cells using <see cref="IDrawingContext.DrawImage"/>.
/// The cell value should be a platform-specific image object
/// (e.g., SKBitmap for SkiaSharp, or a URI string that has been loaded).
/// </summary>
public class ImageCellRenderer : ICellRenderer
{
    /// <summary>
    /// Optional delegate to resolve a cell value to a platform-specific image object.
    /// If not set, the raw cell value is passed to DrawImage directly.
    /// </summary>
    public Func<object?, object?>? ImageResolver { get; set; }

    /// <inheritdoc />
    public void Render(IDrawingContext ctx, in CellRenderContext cell)
    {
        var cellRect = cell.Bounds;
        var image = ImageResolver != null ? ImageResolver(cell.Value) : cell.Value;
        if (image == null) return;

        // Fit the image within the cell, maintaining aspect ratio,
        // centered in the cell rect
        float maxSize = Math.Min(cellRect.Width, cellRect.Height) - 4;
        if (maxSize <= 0) return;

        float imgX = cellRect.X + (cellRect.Width - maxSize) / 2;
        float imgY = cellRect.Y + (cellRect.Height - maxSize) / 2;

        ctx.DrawImage(image, new KumikoRect(imgX, imgY, maxSize, maxSize));
    }
}
