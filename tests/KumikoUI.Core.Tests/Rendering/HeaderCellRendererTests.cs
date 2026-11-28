using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// A column's header content can be drawn by a custom <see cref="IHeaderCellRenderer"/> (#333): per column, grid-wide
/// or the built-in text, with the grid keeping everything outside the content bounds.
/// </summary>
public class HeaderCellRendererTests
{
    private static DrawOp HeaderText(RecordingDrawingContext ctx, string header) =>
        Assert.Single(ctx.Ops, o => o.Kind == DrawOpKind.TextInRect && o.Text == header && Math.Abs(o.Rect.Y) < 0.01f);

    // ── Resolution ──

    [Fact]
    public void WithoutARenderer_TheBuiltInTextRendererDrawsTheHeader()
    {
        var column = new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 150 };

        Assert.IsType<TextHeaderCellRenderer>(KumikoRenderer.ResolveHeaderCellRenderer(column, null));
        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[] { column }));
        var op = HeaderText(h.Render(), "Name");

        Assert.Equal(8f, op.Rect.X, 0.01f); // header padding
    }

    [Fact]
    public void AColumnRenderer_WinsOverTheGridWideRenderer_WhichWinsOverTheText()
    {
        var own = new RecordingHeaderRenderer();
        var gridWide = new RecordingHeaderRenderer();
        var withOwn = new KumikoColumn { Header = "Own", PropertyName = "Name", Width = 150, CustomHeaderRenderer = own };
        var plain = new KumikoColumn { Header = "Plain", PropertyName = "Age", Width = 150 };

        Assert.Same(own, KumikoRenderer.ResolveHeaderCellRenderer(withOwn, gridWide));
        Assert.Same(gridWide, KumikoRenderer.ResolveHeaderCellRenderer(plain, gridWide));
        Assert.IsType<TextHeaderCellRenderer>(KumikoRenderer.ResolveHeaderCellRenderer(plain, null));

        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[] { withOwn, plain }));
        h.Renderer.HeaderCellRenderer = gridWide;
        h.Render();

        Assert.Equal(new[] { "Own" }, own.Contexts.Select(c => c.Column.Header));
        Assert.Equal(new[] { "Plain" }, gridWide.Contexts.Select(c => c.Column.Header));
    }

    [Fact]
    public void ChangingTheColumnRenderer_RaisesPropertyChangedAndRedraws()
    {
        var column = new KumikoColumn { Header = "Name", PropertyName = "Name" };
        var raised = new List<string?>();
        column.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        column.CustomHeaderRenderer = new RecordingHeaderRenderer();
        column.CustomHeaderRenderer = column.CustomHeaderRenderer; // same value: no event

        Assert.Equal(new[] { nameof(KumikoColumn.CustomHeaderRenderer) }, raised);
    }

    // ── The context ──

    [Fact]
    public void TheContext_CarriesTheColumnItsStateAndTheContentBoundsWithoutTheGlyphs()
    {
        var renderer = new RecordingHeaderRenderer();
        var column = new KumikoColumn
        {
            Header = "Name", PropertyName = "Name", Width = 150, CustomHeaderRenderer = renderer,
            HeaderCellStyle = new KumikoCellStyle { TextAlignment = KumikoTextAlignment.Right },
        };
        var other = new KumikoColumn { Header = "Age", PropertyName = "Age", Width = 100 };
        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[] { other, column }));
        h.Source.SetSort(column, KumikoSortDirection.Descending);
        h.Source.SetColumnFilter(column, new FilterDescription(column)
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "P" },
        });

        h.Render();

        var c = Assert.Single(renderer.Contexts);
        Assert.Same(column, c.Column);
        Assert.Equal(1, c.ColumnIndex);
        Assert.Equal(KumikoSortDirection.Descending, c.SortDirection);
        Assert.True(c.IsFiltered);
        Assert.Same(column.HeaderCellStyle, c.HeaderCellStyle);
        Assert.Equal(KumikoTextAlignment.Right, c.TextAlignment);
        Assert.Equal(new KumikoRect(100, 0, 150, h.Style.HeaderHeight), c.CellBounds);
        // The padding on both sides, and the room of the sort arrow (12) and the filter icon (14) on the right.
        Assert.Equal(108f, c.Bounds.X, 0.01f);
        Assert.Equal(150f - 16f - 12f - 14f, c.Bounds.Width, 0.01f);
        Assert.Equal(h.Style.HeaderHeight, c.Bounds.Height, 0.01f);
    }

    [Fact]
    public void TheContext_TextPaintCarriesTheColumnsHeaderOverrides()
    {
        var renderer = new RecordingHeaderRenderer();
        var color = new KumikoColor(10, 20, 30);
        var column = new KumikoColumn
        {
            Header = "Name", PropertyName = "Name", Width = 150, CustomHeaderRenderer = renderer,
            HeaderCellStyle = new KumikoCellStyle { TextColor = color },
        };
        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[] { column }));

        h.Render();

        Assert.Equal(color, renderer.Contexts.Single().TextPaint.Color);
    }

    [Fact]
    public void ACustomRenderer_IsClippedToItsBounds_AndTheClipIsReleased()
    {
        var column = new KumikoColumn
        {
            Header = "Name", PropertyName = "Name", Width = 150,
            CustomHeaderRenderer = new RecordingHeaderRenderer { DrawsWideMarker = true },
        };
        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[] { column }));

        var ctx = h.Render();

        var marker = Assert.Single(ctx.Ops, o => o.Kind == DrawOpKind.FillRect && o.Color == new KumikoColor(1, 2, 3));
        Assert.NotNull(marker.Clip);
        Assert.Equal(8f, marker.Clip!.Value.X, 0.01f);
        Assert.Equal(0, ctx.UnbalancedRestores);
        Assert.Equal(0, ctx.SaveDepth);
    }

    [Fact]
    public void TheTextRenderer_StillAlignsByTheHeaderCellStyle()
    {
        var column = new KumikoColumn
        {
            Header = "Hi", PropertyName = "Name", Width = 150,
            HeaderCellStyle = new KumikoCellStyle { TextAlignment = KumikoTextAlignment.Right },
        };
        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[] { column }));

        var op = HeaderText(h.Render(), "Hi");

        Assert.Equal(KumikoTextAlignment.Right, op.HAlign);
    }

    // ── What the grid keeps ──

    [Fact]
    public void WithACustomRenderer_TheGridStillDrawsTheBackgroundHoverAndGlyphs()
    {
        var plain = RenderHarness.Create(configure: h =>
        {
            h.Source.SetColumns(new[] { new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 150 } });
        });
        var custom = RenderHarness.Create(configure: h =>
        {
            h.Source.SetColumns(new[]
            {
                new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 150, CustomHeaderRenderer = new RecordingHeaderRenderer() },
            });
        });
        plain.Source.SetSort(plain.Source.Columns[0], KumikoSortDirection.Ascending);
        custom.Source.SetSort(custom.Source.Columns[0], KumikoSortDirection.Ascending);

        var expected = plain.Render();
        var actual = custom.Render();

        // Same fills, lines and glyphs; only the header text op is replaced by the renderer's own drawing.
        Assert.Equal(expected.Ops.Count(o => o.Kind == DrawOpKind.FillPolygon), actual.Ops.Count(o => o.Kind == DrawOpKind.FillPolygon));
        Assert.Equal(expected.Ops.Count(o => o.Kind == DrawOpKind.Line), actual.Ops.Count(o => o.Kind == DrawOpKind.Line));
        Assert.Equal(expected.Ops.Count(o => o.Kind == DrawOpKind.FillRect), actual.Ops.Count(o => o.Kind == DrawOpKind.FillRect));
    }

    [Fact]
    public void FrozenAndScrollingPanes_EachDrawTheirHeadersThroughTheRenderer()
    {
        var renderer = new RecordingHeaderRenderer();
        var h = RenderHarness.Create(configure: h =>
        {
            h.Source.SetColumns(new[]
            {
                new KumikoColumn { Header = "L", PropertyName = "Name", Width = 100, IsFrozen = true },
                new KumikoColumn { Header = "M", PropertyName = "Age", Width = 100 },
                new KumikoColumn { Header = "R", PropertyName = "City", Width = 100, IsFrozenRight = true },
            });
        });
        h.Renderer.HeaderCellRenderer = renderer;

        h.Render();

        Assert.Equal(new[] { "L", "M", "R" }, renderer.Contexts.Select(c => c.Column.Header).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(3, renderer.Contexts.Count);
    }

    [Fact]
    public void TheDragGhost_IsDrawnThroughTheSameRenderer()
    {
        var renderer = new RecordingHeaderRenderer();
        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[]
        {
            new KumikoColumn { Header = "A", PropertyName = "Name", Width = 100, CustomHeaderRenderer = renderer },
            new KumikoColumn { Header = "B", PropertyName = "Age", Width = 100 },
        }));
        h.Render();
        renderer.Contexts.Clear();

        h.Render(dragColumnIndex: 0, dragColumnScreenX: 55);

        Assert.Equal(2, renderer.Contexts.Count); // the header in place, and the ghost
        Assert.Contains(renderer.Contexts, c => Math.Abs(c.CellBounds.X - 55f) < 0.01f);
    }

    // ── Auto-fit ──

    [Fact]
    public void MeasureWidth_FeedsTheHeaderFitWidth()
    {
        var column = new KumikoColumn
        {
            Header = "Revenue", PropertyName = "Salary", CustomHeaderRenderer = new RecordingHeaderRenderer { Width = 200 },
        };
        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[] { column }));

        h.Render();

        // 200 of content + 16 padding + 12 sort + 14 filter + 4 margin
        Assert.Equal(246f, column.HeaderFitWidth, 0.01f);
        Assert.Equal(246f, column.Width, 0.01f);
    }

    [Fact]
    public void ANullMeasureWidth_FallsBackToTheTextMeasurement()
    {
        var column = new KumikoColumn
        {
            Header = "Revenue", PropertyName = "Salary", CustomHeaderRenderer = new RecordingHeaderRenderer { Width = null },
        };
        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[] { column }));

        h.Render();

        Assert.Equal(7 * 8f + 46f, column.HeaderFitWidth, 0.01f);
    }

    [Fact]
    public void TheMeasuredWidth_IsCachedUntilTheTextOrTheRendererChanges()
    {
        var first = new RecordingHeaderRenderer { Width = 100 };
        var second = new RecordingHeaderRenderer { Width = 300 };
        var column = new KumikoColumn { Header = "Revenue", PropertyName = "Salary", CustomHeaderRenderer = first };
        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[] { column }));

        h.Render();
        h.Render();
        Assert.Equal(1, first.Measures);

        column.Header = "Revenue (USD)";
        h.Render();
        Assert.Equal(2, first.Measures);

        column.CustomHeaderRenderer = second;
        h.Render();
        h.Render();
        Assert.Equal(1, second.Measures);
        Assert.Equal(300f + 46f, column.HeaderFitWidth, 0.01f);
    }

    [Fact]
    public void TheGridWideRenderer_IsMeasuredToo_AndReplacingItMeasuresAgain()
    {
        var column = new KumikoColumn { Header = "Revenue", PropertyName = "Salary" };
        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[] { column }));
        h.Renderer.HeaderCellRenderer = new RecordingHeaderRenderer { Width = 120 };
        h.Render();
        Assert.Equal(120f + 46f, column.HeaderFitWidth, 0.01f);

        h.Renderer.HeaderCellRenderer = new RecordingHeaderRenderer { Width = 220 };
        h.Render();
        Assert.Equal(220f + 46f, column.HeaderFitWidth, 0.01f);

        h.Renderer.HeaderCellRenderer = null;
        h.Render();
        Assert.Equal(7 * 8f + 46f, column.HeaderFitWidth, 0.01f);
    }

    [Fact]
    public void AnInvalidMeasuredWidth_IsTreatedAsZero()
    {
        var column = new KumikoColumn
        {
            Header = "Revenue", PropertyName = "Salary", CustomHeaderRenderer = new RecordingHeaderRenderer { Width = float.NaN },
        };
        var h = RenderHarness.Create(configure: h => h.Source.SetColumns(new[] { column }));

        h.Render();

        Assert.Equal(46f, column.HeaderFitWidth, 0.01f);
    }

    [Fact]
    public void GetAccessibleText_DefaultsToNull()
    {
        IHeaderCellRenderer renderer = new TextHeaderCellRenderer();
        var column = new KumikoColumn { Header = "Name" };
        var context = new HeaderCellRenderContext(default, default, column, 0, KumikoSortDirection.None, 0, false,
            new KumikoStyle(), null, new PaintCache(), new KumikoPaint(), KumikoTextAlignment.Left);

        Assert.Null(renderer.GetAccessibleText(in context));
        Assert.Null(renderer.MeasureWidth(new RecordingDrawingContext(), in context));
    }

    /// <summary>Records every context it renders and measures, and optionally draws a marker wider than its bounds.</summary>
    private sealed class RecordingHeaderRenderer : IHeaderCellRenderer
    {
        public List<HeaderCellRenderContext> Contexts { get; } = new();
        public int Measures { get; private set; }
        public float? Width { get; init; }
        public bool DrawsWideMarker { get; init; }
        private readonly KumikoPaint _marker = new() { Color = new KumikoColor(1, 2, 3) };

        public void Render(IDrawingContext ctx, in HeaderCellRenderContext context)
        {
            Contexts.Add(context);
            if (DrawsWideMarker)
                ctx.FillRect(new KumikoRect(context.Bounds.X, 0, 600, 10), _marker);
        }

        public float? MeasureWidth(IDrawingContext ctx, in HeaderCellRenderContext context)
        {
            Measures++;
            return Width;
        }
    }
}
