using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using KumikoUI.SkiaSharp;
using SkiaSharp;

namespace KumikoUI.Core.Tests.RowHeight;

/// <summary>
/// The steady-state render loop stays allocation-free with rows of varying height (#320, spec #102 §8.2): once the
/// visible rows are measured, a frame reads heights from the row layout and measures nothing, in
/// <see cref="KumikoRowHeightMode.Auto"/> mode, with a resolver, and while scrolling over measured rows.
/// </summary>
[Collection(nameof(RenderAllocationCollection))]
public class RowHeightAllocationTests
{
    private const int WarmUpFrames = 20;
    private const int MeasuredFrames = 100;

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Address { get; set; } = "";
        public int Quantity { get; set; }
    }

    private sealed record Grid(KumikoRenderer Renderer, KumikoSource Source, ScrollState Scroll,
        KumikoSelectionModel Selection, KumikoStyle Style)
    {
        public void Render(IDrawingContext ctx) => Renderer.Render(ctx, Source, Scroll, Selection, Style);
    }

    private static Grid CreateGrid(int rows = 2000)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { Header = "Name", PropertyName = nameof(Row.Name), Width = 110 },
            new KumikoColumn { Header = "Notes", PropertyName = nameof(Row.Notes), Width = 160, TextWrap = KumikoTextWrap.Word },
            new KumikoColumn { Header = "Address", PropertyName = nameof(Row.Address), Width = 160, TextWrap = KumikoTextWrap.Character },
            new KumikoColumn { Header = "Quantity", PropertyName = nameof(Row.Quantity), Width = 90, ColumnType = KumikoColumnType.Numeric },
        });
        source.SetItems(Enumerable.Range(0, rows).Select(i => new Row
        {
            Name = $"Row {i}",
            Notes = string.Join(' ', Enumerable.Repeat("note", 1 + 4 * (i % 9))),
            Address = $"{i} Main Street, Springfield",
            Quantity = i % 97,
        }).ToList());
        var style = new KumikoStyle { RowHeightMode = KumikoRowHeightMode.Auto };
        var scroll = new ScrollState { ViewportWidth = 800, ViewportHeight = 1100 };
        return new Grid(new KumikoRenderer(), source, scroll, new KumikoSelectionModel(source), style);
    }

    [Fact]
    public void SteadyState_AutoWithTwoWrappedColumns_AllocatesNothing()
    {
        var grid = CreateGrid();

        Assert.Equal(0, MeasureSteadyState(grid.Render));
        Assert.True(grid.Source.RowLayout.IsActive);
    }

    [Fact]
    public void SteadyState_AutoWithRealWrappedText_OnSkia_AllocatesNothing()
    {
        // The SkiaSharp context measures and wraps the notes for real: rows of several heights.
        using var bitmap = new SKBitmap(800, 1100);
        using var canvas = new SKCanvas(bitmap);
        using var cache = new SkiaResourceCache();
        using var ctx = new SkiaDrawingContext(canvas, cache);
        var grid = CreateGrid();

        Assert.Equal(0, MeasureSteadyState(_ => grid.Render(ctx)));
        var heights = Enumerable.Range(0, 20).Select(r => KumikoUI.Core.Layout.KumikoScroller.GetRowHeight(grid.Source, grid.Style, r));
        Assert.True(heights.Distinct().Count() > 1, "the wrapped notes make rows of several heights");
    }

    [Fact]
    public void SteadyState_AutoWithAResolver_AllocatesNothing()
    {
        var grid = CreateGrid();
        grid.Style.RowHeightResolver = (in KumikoRowHeightContext c) => ((Row)c.Item).Quantity % 3 == 0 ? 60 : null;

        Assert.Equal(0, MeasureSteadyState(grid.Render));
    }

    [Fact]
    public void SteadyState_FixedWithAResolver_AllocatesNothing()
    {
        var grid = CreateGrid();
        grid.Style.RowHeightMode = KumikoRowHeightMode.Fixed;
        grid.Style.RowHeightResolver = (in KumikoRowHeightContext c) => ((Row)c.Item).Quantity % 3 == 0 ? 60 : null;

        Assert.Equal(0, MeasureSteadyState(grid.Render));
    }

    [Fact]
    public void SteadyState_ScrollingOverMeasuredRows_AllocatesNothing()
    {
        var grid = CreateGrid();
        grid.Style.RowHeightResolver = (in KumikoRowHeightContext c) => ((Row)c.Item).Quantity % 3 == 0 ? 60 : null;
        int frame = 0;

        Assert.Equal(0, MeasureSteadyState(ctx =>
        {
            grid.Scroll.OffsetY = frame++ % 2 == 0 ? 0 : 5 * 36 + 7;
            grid.Render(ctx);
        }));
    }

    [Fact]
    public void AnInvalidatedRow_IsMeasuredOnce_ThenFramesAllocateNothingAgain()
    {
        var grid = CreateGrid();
        MeasureSteadyState(grid.Render);

        grid.Source.InvalidateRowHeight(grid.Source.GetItem(3));
        grid.Render(new NoOpDrawingContext());

        Assert.Equal(0, MeasureSteadyState(grid.Render));
    }

    /// <summary>The least bytes allocated over <see cref="MeasuredFrames"/> frames after warming up, of three tries.</summary>
    private static long MeasureSteadyState(Action<IDrawingContext> render)
    {
        var ctx = new NoOpDrawingContext();
        for (int i = 0; i < WarmUpFrames; i++)
            render(ctx);

        long least = long.MaxValue;
        for (int attempt = 0; attempt < 3 && least > 0; attempt++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < MeasuredFrames; i++)
                render(ctx);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        return least;
    }
}
