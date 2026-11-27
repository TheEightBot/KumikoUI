using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// A visible selection column allocates nothing per frame (#321), like the rest of the render loop: its boxes, the
/// check-all header box in every state and the dimmed boxes of the None mode draw from reused paints.
/// </summary>
[Collection(nameof(RenderAllocationCollection))]
public class SelectionColumnAllocationTests
{
    private const int WarmUpFrames = 20;
    private const int MeasuredFrames = 100;

    private sealed class Row
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
    }

    private static long Measure(KumikoSelectionMode mode, bool hover, Action<KumikoSelectionModel>? configure = null)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoSelectionColumn(),
            new KumikoColumn { Header = "Name", PropertyName = nameof(Row.Name), Width = 110 },
            new KumikoColumn { Header = "Quantity", PropertyName = nameof(Row.Quantity), Width = 110, ColumnType = KumikoColumnType.Numeric },
        });
        source.SetItems(Enumerable.Range(0, 500).Select(i => new Row { Name = $"Row {i}", Quantity = i }).ToList());
        var style = new KumikoStyle();
        var scroll = new ScrollState { ViewportWidth = 400, ViewportHeight = 600 };
        var selection = new KumikoSelectionModel(source) { Mode = mode };
        configure?.Invoke(selection);
        var renderer = new KumikoRenderer();
        if (hover)
        {
            var input = new KumikoInputController();
            renderer.InputController = input;
            renderer.Render(new NoOpDrawingContext(), source, scroll, selection, style);
            input.HandlePointer(new KumikoPointerEventArgs { Action = InputAction.Hover, X = 22, Y = style.HeaderHeight + 18 },
                scroll, selection, style, source);
        }

        var ctx = new NoOpDrawingContext();
        for (int i = 0; i < WarmUpFrames; i++)
            renderer.Render(ctx, source, scroll, selection, style);

        long least = long.MaxValue;
        for (int attempt = 0; attempt < 3 && least > 0; attempt++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < MeasuredFrames; i++)
                renderer.Render(ctx, source, scroll, selection, style);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        return least;
    }

    [Fact]
    public void NothingSelected_AllocatesNothing() => Assert.Equal(0, Measure(KumikoSelectionMode.Extended, hover: false));

    [Fact]
    public void SomeSelected_IndeterminateHeader_AllocatesNothing() =>
        Assert.Equal(0, Measure(KumikoSelectionMode.Extended, hover: false, s =>
        {
            s.SetRangeSelected(2, 40, true);
        }));

    [Fact]
    public void AllSelected_AllocatesNothing() =>
        Assert.Equal(0, Measure(KumikoSelectionMode.Multiple, hover: false, s => s.SelectAll()));

    [Fact]
    public void NoneMode_DimmedBoxes_AllocateNothing() => Assert.Equal(0, Measure(KumikoSelectionMode.None, hover: false));

    [Fact]
    public void SingleMode_AllocatesNothing() =>
        Assert.Equal(0, Measure(KumikoSelectionMode.Single, hover: false, s => s.HandleCheckBoxToggle(3, false)));

    [Fact]
    public void AHoveredBox_AllocatesNothing() =>
        Assert.Equal(0, Measure(KumikoSelectionMode.Extended, hover: true));
}
