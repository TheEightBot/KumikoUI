using System.Collections.ObjectModel;
using System.Reflection;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Maui.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// The MAUI grid's rendering (#71): which surface <see cref="KumikoDataGrid.RenderMode"/> resolves to, swapping the
/// surface at run time, and the caret blink redrawing only the editor's area over a cached frame.
/// </summary>
public class KumikoDataGridRenderingTests
{
    // ── Surface selection ─────────────────────────────────────────

    [Theory]
    [InlineData(KumikoRenderMode.Auto, true, true, KumikoRenderMode.Gpu)]
    [InlineData(KumikoRenderMode.Auto, true, false, KumikoRenderMode.Software)]  // iOS, Windows
    [InlineData(KumikoRenderMode.Auto, false, true, KumikoRenderMode.Software)]
    [InlineData(KumikoRenderMode.Gpu, true, false, KumikoRenderMode.Gpu)]        // opt in where Auto does not
    [InlineData(KumikoRenderMode.Gpu, false, false, KumikoRenderMode.Software)]  // no GPU view: falls back
    [InlineData(KumikoRenderMode.Software, true, true, KumikoRenderMode.Software)]
    public void Resolve_PicksTheSurface(KumikoRenderMode requested, bool gpuSupported, bool gpuDefault, KumikoRenderMode expected)
    {
        Assert.Equal(expected, KumikoSurfaceFactory.Resolve(requested, gpuSupported, gpuDefault));
    }

    [Fact]
    public void Create_GpuMode_IsAnSKGLView()
    {
        var surface = KumikoSurfaceFactory.Create(KumikoRenderMode.Gpu);

        Assert.IsType<SKGLView>(surface.View);
        Assert.Equal(KumikoRenderMode.Gpu, surface.Mode);
    }

    [Fact]
    public void Create_SoftwareMode_IsAnSKCanvasView()
    {
        var surface = KumikoSurfaceFactory.Create(KumikoRenderMode.Software);

        Assert.IsType<SKCanvasView>(surface.View);
        Assert.Equal(KumikoRenderMode.Software, surface.Mode);
    }

    [Fact]
    public void NewGrid_RenderModeIsAuto_AndTheActiveModeIsResolved()
    {
        var grid = new KumikoDataGrid();

        Assert.Equal(KumikoRenderMode.Auto, grid.RenderMode);
        Assert.Equal(KumikoSurfaceFactory.Resolve(KumikoRenderMode.Auto), grid.ActiveRenderMode);
        Assert.NotEqual(KumikoRenderMode.Auto, grid.ActiveRenderMode);
    }

    [Theory]
    [InlineData(KumikoRenderMode.Gpu)]
    [InlineData(KumikoRenderMode.Software)]
    [InlineData(KumikoRenderMode.Auto)]
    public void RenderMode_Set_KeepsOneSurfaceBelowTheKeyboardProxy(KumikoRenderMode mode)
    {
        var grid = new KumikoDataGrid();

        grid.RenderMode = mode;

        var root = Assert.IsType<Grid>(grid.Content);
        Assert.Equal(2, root.Children.Count);
        Assert.IsAssignableFrom<View>(root.Children[0]);
        Assert.IsType<Entry>(root.Children[1]);
        Assert.Equal(KumikoSurfaceFactory.Resolve(mode), grid.ActiveRenderMode);
        Assert.Equal(grid.ActiveRenderMode == KumikoRenderMode.Gpu, root.Children[0] is SKGLView);
    }

    [Fact]
    public void RenderMode_SwappedBackAndForth_StillPaints()
    {
        var grid = PaintableGrid();
        grid.RenderMode = KumikoRenderMode.Gpu;
        grid.RenderMode = KumikoRenderMode.Software;

        using var surface = NewSurface();
        Paint(grid, surface);

        // The header (and the background) were drawn on the new surface.
        Assert.NotEqual(default, Pixel(surface, 50, 10));
    }

    // ── Caret blink ───────────────────────────────────────────────

    private sealed class Row
    {
        public string Name { get; set; } = "";
    }

    /// <summary>Fills every text cell with <see cref="Color"/>, read at draw time (nothing tells the grid it changed).</summary>
    private sealed class SolidCellRenderer : ICellRenderer
    {
        public KumikoColor Color { get; set; } = new(255, 0, 0);

        public void Render(IDrawingContext ctx, in CellRenderContext cell) =>
            ctx.FillRect(cell.Bounds, new KumikoPaint { Color = Color, Style = PaintStyle.Fill });
    }

    private const int SurfaceWidth = 300, SurfaceHeight = 400;

    private static KumikoDataGrid PaintableGrid(SolidCellRenderer? cells = null)
    {
        var grid = new KumikoDataGrid
        {
            ItemsSource = new ObservableCollection<Row>(Enumerable.Range(0, 20).Select(i => new Row { Name = $"R{i}" })),
            RenderMode = KumikoRenderMode.Software,
        };
        grid.Columns.Add(new KumikoColumn { Header = "Name", PropertyName = nameof(Row.Name), Width = 200 });
        if (cells is not null) grid.SetCellRenderer(KumikoColumnType.Text, cells);
        return grid;
    }

    private static SKSurface NewSurface() =>
        SKSurface.Create(new SKImageInfo(SurfaceWidth, SurfaceHeight, SKColorType.Rgba8888, SKAlphaType.Premul));

    /// <summary>Paints the grid's software surface as its handler would, at 1 device pixel per DIP (no layout).</summary>
    private static void Paint(KumikoDataGrid grid, SKSurface surface)
    {
        var view = Assert.IsType<SKCanvasView>(((Grid)grid.Content).Children[0]);
        var info = new SKImageInfo(SurfaceWidth, SurfaceHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        ((ISKCanvasView)view).OnPaintSurface(new SKPaintSurfaceEventArgs(surface, info));
        surface.Flush();
    }

    private static SKColor Pixel(SKSurface surface, int x, int y)
    {
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(x, y);
    }

    private static TestDispatcher.ManualTimer BlinkTimer(KumikoDataGrid grid)
    {
        var field = typeof(KumikoDataGrid).GetField("_caretBlinkTimer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<TestDispatcher.ManualTimer>(field.GetValue(grid));
    }

    // Header 40, rows 36: row 0 (the edited one) spans y 40-76, row 6 spans 256-292.
    private const int EditedRowY = 58, OtherRowY = 274, CellX = 100;

    [Fact]
    public void CaretBlink_RedrawsOnlyTheEditorArea_OverTheLastFullFrame()
    {
        var cells = new SolidCellRenderer { Color = new KumikoColor(255, 0, 0) };
        var grid = PaintableGrid(cells);
        using var surface = NewSurface();
        Paint(grid, surface);

        var column = grid.Columns[0];
        Assert.True(grid.EditSession.BeginEdit(0, 0, column, grid.DataSource, new KumikoRect(0, 40, 200, 36)));
        Paint(grid, surface); // places the editor
        var timer = BlinkTimer(grid);
        Assert.True(timer.IsRunning);
        Assert.Equal(KumikoCaretBlink.PeriodMs + KumikoCaretBlink.SlackMs, timer.Interval.TotalMilliseconds);

        timer.Fire();
        Paint(grid, surface); // first blink: a full frame, kept for the next blinks
        Assert.True(timer.IsRunning); // re-armed for the next transition
        Assert.Equal(new SKColor(255, 0, 0), Pixel(surface, CellX, OtherRowY));

        // Something the grid is not told about changes how every cell draws.
        cells.Color = new KumikoColor(0, 0, 255);
        timer.Fire();
        Paint(grid, surface);

        // The blink frame came from the kept frame outside the editor's area...
        Assert.Equal(new SKColor(255, 0, 0), Pixel(surface, CellX, OtherRowY));
        // ...and was redrawn inside it: the editor (a text box, not the cell renderer) covers the edited cell.
        Assert.NotEqual(new SKColor(255, 0, 0), Pixel(surface, CellX, EditedRowY));

        // Any other redraw paints the whole frame.
        grid.Invalidate();
        Paint(grid, surface);
        Assert.Equal(new SKColor(0, 0, 255), Pixel(surface, CellX, OtherRowY));
    }

    [Fact]
    public void CaretBlink_AfterAnotherRedraw_PaintsTheWholeFrame()
    {
        var cells = new SolidCellRenderer { Color = new KumikoColor(255, 0, 0) };
        var grid = PaintableGrid(cells);
        using var surface = NewSurface();
        Paint(grid, surface);
        grid.EditSession.BeginEdit(0, 0, grid.Columns[0], grid.DataSource, new KumikoRect(0, 40, 200, 36));
        Paint(grid, surface);
        var timer = BlinkTimer(grid);
        timer.Fire();
        Paint(grid, surface); // kept

        cells.Color = new KumikoColor(0, 0, 255);
        grid.Invalidate(); // a real change arrives with the blink
        timer.Fire();
        Paint(grid, surface);

        Assert.Equal(new SKColor(0, 0, 255), Pixel(surface, CellX, OtherRowY));
    }

    [Fact]
    public void CaretBlink_StopsWhenEditingEnds()
    {
        var grid = PaintableGrid();
        grid.EditSession.BeginEdit(0, 0, grid.Columns[0], grid.DataSource, new KumikoRect(0, 40, 200, 36));
        var timer = BlinkTimer(grid);
        Assert.True(timer.IsRunning);

        grid.EditSession.CancelEdit();

        Assert.False(timer.IsRunning);
    }

    // ── Caret frame geometry ──────────────────────────────────────

    [Fact]
    public void ToDeviceClip_ScalesRoundsOutAddsTheMarginAndStaysOnTheSurface()
    {
        var clip = KumikoCaretFrame.ToDeviceClip(new KumikoRect(10.2f, 20.6f, 50f, 30f), 3f, 1000, 1000);

        int m = KumikoCaretFrame.MarginPixels;
        Assert.Equal(new SKRectI(30 - m, 61 - m, 181 + m, 152 + m), clip);
        Assert.Equal(new SKRectI(0, 0, 100, 40), KumikoCaretFrame.ToDeviceClip(new KumikoRect(-5, -5, 200, 30), 1f, 100, 100));
        Assert.True(KumikoCaretFrame.ToDeviceClip(new KumikoRect(500, 500, 10, 10), 1f, 100, 100).IsEmpty);
        Assert.True(KumikoCaretFrame.ToDeviceClip(new KumikoRect(0, 0, 10, 10), 0f, 100, 100).IsEmpty);
    }

    [Fact]
    public void Union_IgnoresEmptyRectangles()
    {
        var a = new KumikoRect(10, 10, 20, 20);
        var b = new KumikoRect(50, 0, 10, 5);

        var u = KumikoCaretFrame.Union(a, b);

        Assert.Equal((10f, 0f, 60f, 30f), (u.X, u.Y, u.Right, u.Bottom));
        Assert.Equal(a, KumikoCaretFrame.Union(a, KumikoRect.Empty));
        Assert.Equal(b, KumikoCaretFrame.Union(KumikoRect.Empty, b));
    }

    [Fact]
    public void CaretFrame_RestoresTheCapturedPixelsOnlyAtTheSameSize()
    {
        using var surface = NewSurface();
        surface.Canvas.Clear(new SKColor(10, 20, 30));
        using var frame = new KumikoCaretFrame();
        frame.Capture(surface);
        Assert.True(frame.HasFrame);

        surface.Canvas.Clear(SKColors.White);
        Assert.False(frame.TryRestore(surface.Canvas, new SKImageInfo(SurfaceWidth + 1, SurfaceHeight)));
        Assert.True(frame.TryRestore(surface.Canvas, new SKImageInfo(SurfaceWidth, SurfaceHeight)));
        surface.Flush();

        Assert.Equal(new SKColor(10, 20, 30), Pixel(surface, 5, 5));
        frame.Clear();
        Assert.False(frame.HasFrame);
    }
}
