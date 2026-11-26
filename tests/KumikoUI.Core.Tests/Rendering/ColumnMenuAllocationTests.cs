using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Tests.Input;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// An open column menu (#319) adds no allocation to the frame: its items are laid out and measured when they change,
/// and drawing them reuses cached paints and the items' strings, with the column menu buttons drawn in the headers.
/// </summary>
[Collection(nameof(RenderAllocationCollection))]
public class ColumnMenuAllocationTests
{
    private const int WarmUpFrames = 20;
    private const int MeasuredFrames = 100;

    [Fact]
    public void SteadyState_AnOpenColumnMenu_WithAHighlightedItemAndMenuButtons_AllocatesNothing()
    {
        var g = new InputGrid(viewportHeight: 600, viewportWidth: 600, configure: x =>
        {
            x.Input.IsColumnMenuEnabled = true;
            x.Input.ShowColumnMenuButton = true;
            x.Renderer.ShowColumnMenuButton = true;
        });
        Assert.True(g.Input.ShowColumnMenu(g.Age, g.Scroll, g.Selection, g.Style, g.Source));
        var menu = Assert.IsType<DrawnMenu>(g.Input.ActivePopup);
        Assert.True(menu.HighlightedIndex >= 0);

        Assert.Equal(0, MeasureSteadyState(ctx =>
            g.Renderer.RenderFrame(ctx, g.Source, g.Scroll, g.Selection, g.Style, -1, 0, -1, 0, g.Edit,
                g.Input.PopupManager)));
    }

    [Fact]
    public void SteadyState_ATallMenuThatScrolls_AllocatesNothing()
    {
        var menu = new DrawnMenu { MaxHeight = 120 };
        for (int i = 0; i < 20; i++) menu.Items.Add(new DrawnMenuItem($"k{i}", $"Item {i}") { IsChecked = i % 2 == 0 });
        menu.OnScroll(new KumikoPointerEventArgs { Action = InputAction.Scroll, ScrollDeltaY = -1 });

        Assert.Equal(0, MeasureSteadyState(menu.OnDraw));
    }

    private static long MeasureSteadyState(Action<KumikoUI.Core.Rendering.IDrawingContext> render)
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
