using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders images in cells using <see cref="IDrawingContext.DrawImage"/>, fitted into a square centered in the cell.
/// </summary>
/// <remarks>
/// The cell value (or what <see cref="ImageResolver"/> maps it to) is resolved through the grid's
/// <see cref="CellRenderContext.ImageResolver"/>. On both hosts that is the grid's image cache, which accepts
/// file names, URLs, bytes, streams and the host's image sources as well as ready <c>SKImage</c> and
/// <c>SKBitmap</c> objects; an image that is still loading draws nothing until it is ready. Without a resolver
/// (a host of your own) the value is passed to <see cref="IDrawingContext.DrawImage"/> unchanged.
/// </remarks>
public class ImageCellRenderer : ICellRenderer
{
    /// <summary>
    /// Optional delegate that maps a cell value to the image source to draw (for example an item's id to its
    /// photo's URL). Its result goes through the grid's image resolver like a cell value. If not set, the raw
    /// cell value is used.
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

        if (cell.ImageResolver is { } resolver
            && (resolver.Resolve(image, out image) != KumikoImageStatus.Ready || image == null))
            return;

        float imgX = cellRect.X + (cellRect.Width - maxSize) / 2;
        float imgY = cellRect.Y + (cellRect.Height - maxSize) / 2;

        ctx.DrawImage(image, new KumikoRect(imgX, imgY, maxSize, maxSize));
    }
}
