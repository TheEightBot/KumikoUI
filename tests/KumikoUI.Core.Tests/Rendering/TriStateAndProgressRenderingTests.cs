using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.TestHelpers.RenderHarness;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// The built-in <see cref="KumikoColumnType.ProgressBar"/> renderer and the indeterminate glyph of
/// three-state Boolean columns (#38), drawn through the grid so the column options and style colors apply.
/// </summary>
public class TriStateAndProgressRenderingTests
{
    private sealed class Flag
    {
        public bool? State { get; set; }
    }

    private static RenderHarness Progress(Action<KumikoColumn>? configure = null)
    {
        var h = Create(rows: 5);
        var age = new KumikoColumn
        {
            Header = "Age", PropertyName = "Age", Width = 100, ColumnType = KumikoColumnType.ProgressBar,
            ProgressMinimum = 20, ProgressMaximum = 24,
        };
        configure?.Invoke(age);
        h.Source.SetColumns(new[] { new KumikoColumn { Header = "Name", PropertyName = "Name", Width = 150 }, age });
        return h;
    }

    // Row 1: Age 21 of 20..24 = 25%. The Age column spans x 150-250, the cell content x 158-242 (84 wide),
    // the track x 162-238 (76 wide), so the fill clip is 19 wide.
    private static IEnumerable<DrawOp> RowOps(RecordingDrawingContext ctx, RenderHarness h, int row, float x0, float x1) =>
        ctx.Ops.Where(o => o.Rect.X >= x0 - 0.01f && o.Rect.Right <= x1 + 0.01f &&
                           o.Rect.Y >= h.RowTop(row) && o.Rect.Bottom <= h.RowTop(row + 1));

    [Fact]
    public void ProgressBar_DrawsStyleTrackAndAccentFillClippedToTheValue()
    {
        var h = Progress();

        var ctx = h.Render();

        var ops = RowOps(ctx, h, 1, 150, 250).Where(o => o.Kind == DrawOpKind.FillRoundRect).ToList();
        Assert.Equal(2, ops.Count);
        Assert.Equal(h.Style.GridLineColor, ops[0].Color);
        Assert.Equal(h.Style.AccentColor, ops[1].Color);
        Assert.Equal(76f, ops[0].Rect.Width, 2);
        Assert.Equal(19f, ops[1].Clip!.Value.Width, 2);
    }

    [Fact]
    public void ProgressBar_AtTheMinimum_DrawsOnlyTheTrack()
    {
        var h = Progress();

        var ctx = h.Render();

        Assert.Single(RowOps(ctx, h, 0, 150, 250), o => o.Kind == DrawOpKind.FillRoundRect);
    }

    [Fact]
    public void ProgressBar_ShowsTheValueFormattedWithTheColumnFormat()
    {
        var h = Progress(c => c.Format = "N1");

        var ctx = h.Render();

        Assert.NotEmpty(ctx.TextsEqual("21.0"));
    }

    [Fact]
    public void ProgressBar_DefaultsToWholeNumberText()
    {
        var h = Progress();

        var ctx = h.Render();

        Assert.NotEmpty(ctx.TextsEqual("21"));
    }

    [Fact]
    public void ProgressBar_ShowTextFalse_DrawsNoLabel()
    {
        var h = Progress(c => c.ProgressShowText = false);

        var ctx = h.Render();

        Assert.DoesNotContain(ctx.Texts, o => o.Text == "21");
    }

    [Fact]
    public void ProgressBar_FillColorOverridesTheStyleAccent()
    {
        var custom = new KumikoColor(9, 8, 7);
        var h = Progress(c => c.ProgressFillColor = custom);

        var ctx = h.Render();

        Assert.Contains(RowOps(ctx, h, 1, 150, 250), o => o.Kind == DrawOpKind.FillRoundRect && o.Color == custom);
        Assert.DoesNotContain(RowOps(ctx, h, 1, 150, 250), o => o.Kind == DrawOpKind.FillRoundRect && o.Color == h.Style.AccentColor);
    }

    [Fact]
    public void ProgressBar_IsReadOnly_NoEditorOpens()
    {
        var h = Progress();
        var edit = new EditSession { Style = h.Style };
        var column = h.Source.Columns[1];

        bool began = edit.BeginEdit(1, 1, column, h.Source, new KumikoRect(150, 76, 100, 36));

        Assert.False(began);
        Assert.False(edit.IsEditing);
    }

    [Fact]
    public void ProgressBar_SortsAndFiltersAsNumbers()
    {
        var h = Create(rows: 5);
        var salary = new KumikoColumn { Header = "Salary", PropertyName = "Salary", Width = 100, ColumnType = KumikoColumnType.ProgressBar };
        h.Source.SetColumns(new[] { salary });

        h.Source.ToggleSort(salary);

        Assert.Equal("P00", ((RenderHarness.Person)h.Source.GetItem(0)).Name);
        var greater = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = "1010" };
        Assert.True(greater.Evaluate(1020m, KumikoColumnType.ProgressBar));
        Assert.False(greater.Evaluate(1000m, KumikoColumnType.ProgressBar));
    }

    // ── Three-state glyph ──

    private static RenderHarness States(bool threeState)
    {
        var h = Create(rows: 3);
        var col = new KumikoColumn
        {
            Header = "State", PropertyName = "State", Width = 100,
            ColumnType = KumikoColumnType.Boolean, IsThreeState = threeState,
        };
        h.Source.SetColumns(new[] { col });
        h.Source.SetItems(new[] { new Flag { State = true }, new Flag { State = false }, new Flag { State = null } });
        return h;
    }

    [Fact]
    public void ThreeState_Null_DrawsAFilledDashInTheAccentColor_AndNoCheckMark()
    {
        var h = States(threeState: true);

        var ctx = h.Render();

        var dash = Assert.Single(ctx.Polygons, o => o.Color == h.Style.AccentColor && o.Rect.Y >= h.RowTop(2));
        Assert.Equal(PaintStyle.Fill, dash.Style);
        Assert.Equal(4, dash.Points!.Length);
        Assert.True(dash.Rect.Width > dash.Rect.Height);
        Assert.DoesNotContain(ctx.Polylines, o => o.Rect.Y >= h.RowTop(2));
    }

    [Fact]
    public void ThreeState_CheckedRow_DrawsAChevron_UncheckedRowDrawsNeither()
    {
        var h = States(threeState: true);

        var ctx = h.Render();

        Assert.Single(ctx.Polylines, o => o.Rect.Y >= h.RowTop(0) && o.Rect.Y < h.RowTop(1));
        Assert.DoesNotContain(ctx.Polygons, o => o.Color == h.Style.AccentColor && o.Rect.Y < h.RowTop(2));
        Assert.DoesNotContain(ctx.Polylines, o => o.Rect.Y >= h.RowTop(1) && o.Rect.Y < h.RowTop(2));
    }

    [Fact]
    public void TwoState_Null_DrawsAsUnchecked()
    {
        var h = States(threeState: false);

        var ctx = h.Render();

        Assert.DoesNotContain(ctx.Polygons, o => o.Color == h.Style.AccentColor && o.Rect.Y >= h.RowTop(2));
        Assert.DoesNotContain(ctx.Polylines, o => o.Rect.Y >= h.RowTop(2));
    }
}
