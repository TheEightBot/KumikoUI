using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Maui.Tests;

/// <summary>The grid-wide header cell renderer of the MAUI <see cref="KumikoDataGrid"/> (#333).</summary>
public class KumikoDataGridHeaderCellRendererTests
{
    private sealed class NoopHeaderRenderer : IHeaderCellRenderer
    {
        public void Render(IDrawingContext ctx, in HeaderCellRenderContext context)
        {
        }
    }

    [Fact]
    public void HeaderCellRenderer_IsNullByDefault_AndReachesTheRenderer()
    {
        var grid = new KumikoDataGrid();
        Assert.Null(grid.HeaderCellRenderer);

        var renderer = new NoopHeaderRenderer();
        grid.HeaderCellRenderer = renderer;

        Assert.Same(renderer, grid.HeaderCellRenderer);
    }

    [Fact]
    public void ACustomHeaderRenderer_CanBeSetOnAColumnDeclaredOnTheGrid()
    {
        var renderer = new NoopHeaderRenderer();
        var column = new KumikoColumn { Header = "Name", PropertyName = "Name", CustomHeaderRenderer = renderer };
        var grid = new KumikoDataGrid { Columns = { column } };

        Assert.Same(renderer, grid.Columns[0].CustomHeaderRenderer);
    }
}
