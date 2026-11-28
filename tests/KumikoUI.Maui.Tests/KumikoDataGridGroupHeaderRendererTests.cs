using System.Reflection;
using KumikoUI.Core;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Maui.Tests;

/// <summary>The MAUI grid's group header renderer (#332): <see cref="KumikoDataGrid.GroupHeaderRenderer"/> reaches the renderer.</summary>
public class KumikoDataGridGroupHeaderRendererTests
{
    private sealed class NoOpRenderer : IGroupHeaderRenderer
    {
        public void Render(IDrawingContext ctx, in GroupHeaderRenderContext context)
        {
        }
    }

    private static KumikoRenderer RendererOf(KumikoDataGrid grid) =>
        (KumikoRenderer)typeof(KumikoDataGrid).GetField("_renderer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(grid)!;

    [Fact]
    public void GroupHeaderRenderer_IsNullByDefault_AndReachesTheRenderer()
    {
        var grid = new KumikoDataGrid();
        var renderer = new NoOpRenderer();

        Assert.Null(grid.GroupHeaderRenderer);
        Assert.Null(RendererOf(grid).GroupHeaderRenderer);

        grid.GroupHeaderRenderer = renderer;
        Assert.Same(renderer, RendererOf(grid).GroupHeaderRenderer);

        grid.GroupHeaderRenderer = null;
        Assert.Null(RendererOf(grid).GroupHeaderRenderer);
    }

    [Fact]
    public void CustomHeaderRenderer_OnAGroupDescription_IsKeptWhenTheGridMirrorsIt()
    {
        var grid = new KumikoDataGrid();
        var renderer = new NoOpRenderer();
        var description = new GroupDescription("Name") { CustomHeaderRenderer = renderer };

        grid.GroupDescriptions.Add(description);

        Assert.Same(renderer, grid.GroupDescriptions[0].CustomHeaderRenderer);
        Assert.Same(renderer, grid.DataSource.GroupDescriptions[0].CustomHeaderRenderer);
    }
}
