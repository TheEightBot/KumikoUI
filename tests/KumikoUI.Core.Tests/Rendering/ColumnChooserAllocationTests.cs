using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Input;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// An open column chooser (#335) adds no allocation to the frame: the entries and their labels are built when the
/// columns or the search change, and drawing them reuses cached paints and the columns' strings, while it scrolls, is
/// hovered or drags an entry.
/// </summary>
[Collection(nameof(RenderAllocationCollection))]
public class ColumnChooserAllocationTests
{
    private const int WarmUpFrames = 20;
    private const int MeasuredFrames = 100;

    [Theory]
    [InlineData(KumikoThemeMode.Light)]
    [InlineData(KumikoThemeMode.Dark)]
    [InlineData(KumikoThemeMode.HighContrast)]
    public void SteadyState_TheGridWithAnOpenChooser_AllocatesNothing(KumikoThemeMode theme)
    {
        var g = new InputGrid(viewportHeight: 600, viewportWidth: 600);
        var style = KumikoTheme.Create(theme);
        g.City.IsVisible = false;
        g.Source.SetSort(g.Age, KumikoSortDirection.Ascending);
        g.Score.FreezeMode = ColumnFreezeMode.Right;
        g.Input.ShowColumnChooser(g.Scroll, g.Selection, style, g.Source);
        var chooser = Assert.IsType<DrawnColumnChooser>(g.Input.ActivePopup);
        g.Key(KumikoKey.Down); // a focused entry

        Assert.Equal(0, MeasureSteadyState(ctx =>
            g.Renderer.RenderFrame(ctx, g.Source, g.Scroll, g.Selection, style, -1, 0, -1, 0, g.Edit,
                g.Input.PopupManager)));
        Assert.True(chooser.IsVisible);
    }

    [Fact]
    public void SteadyState_ALongScrolledListWithADragInProgress_AllocatesNothing()
    {
        var source = new KumikoSource();
        source.SetColumns(Enumerable.Range(0, 200).Select(i => new KumikoColumn { Header = $"Column {i}", PropertyName = "P" }));
        var chooser = new DrawnColumnChooser(source);
        chooser.OnScroll(new KumikoPointerEventArgs { Action = InputAction.Scroll, ScrollDeltaY = -3 });
        Assert.True(chooser.ListScrollOffset > 0);
        var row = chooser.EntryBounds((int)(chooser.ListScrollOffset / chooser.ItemHeight) + 1);
        float y = row.Y + row.Height / 2;
        chooser.OnPointerDown(new KumikoPointerEventArgs { Action = InputAction.Pressed, X = chooser.GripX, Y = y });
        chooser.OnPointerMove(new KumikoPointerEventArgs { Action = InputAction.Moved, X = chooser.GripX, Y = y + 50 });
        Assert.True(chooser.IsDragging);

        Assert.Equal(0, MeasureSteadyState(chooser.OnDraw));
    }

    [Fact]
    public void SteadyState_WhileSearching_AllocatesNothing()
    {
        var source = new KumikoSource();
        source.SetColumns(Enumerable.Range(0, 50).Select(i => new KumikoColumn { Header = $"Column {i}", PropertyName = "P" }));
        var chooser = new DrawnColumnChooser(source) { SearchText = "1" };

        Assert.Equal(0, MeasureSteadyState(chooser.OnDraw));
    }

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
