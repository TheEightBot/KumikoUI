using KumikoUI.Core.Input;
using KumikoUI.Core.Layout;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Input;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// Column sizing (#24): Auto columns are fitted to a bounded sample of their cells when the data changes, a resize
/// drag or an auto-fit of a Star or Auto column persists, and star space a clamped column frees is shared again.
/// </summary>
/// <remarks>
/// The recording context measures 8 px per character and cells have 8 px of padding on each side, so a cell fit is
/// 8 n + 32 for an n-character text (with the 16 px auto-fit buffer). "Name" headers fit in 4 * 8 + 46 = 78 px.
/// </remarks>
public class ColumnSizingTests
{
    private const string LongName = "A considerably longer name"; // 26 characters
    private const float LongNameFit = 26 * 8 + 16 + 16;           // 240

    private static float CellFit(string text) => text.Length * RecordingDrawingContext.CharWidth + 16 + 16;

    private static RenderHarness AutoNameGrid(int rows = 20, float height = 400)
    {
        var h = RenderHarness.Create(rows: rows, height: height);
        h.Column("Name").SizeMode = ColumnSizeMode.Auto;
        return h;
    }

    // ── Auto ──

    [Fact]
    public void AutoColumn_IsFittedToItsWidestCell_OnTheFirstFrame()
    {
        var h = AutoNameGrid();
        h.People[3].Name = LongName;
        var name = h.Column("Name");

        h.Render();

        Assert.Equal(LongNameFit, name.ContentFitWidth);
        Assert.Equal(LongNameFit, name.Width);
        Assert.Equal(ColumnSizeMode.Auto, name.SizeMode);
    }

    [Fact]
    public void AutoColumn_WithShortCells_IsNarrowerThanTheDefaultWidth_ButFitsItsHeader()
    {
        var h = AutoNameGrid(); // "P00".."P19": 56 px of cell, below the 78 px header fit
        var name = h.Column("Name");

        h.Render();

        Assert.Equal(name.HeaderFitWidth, name.Width);
        Assert.True(name.Width < KumikoColumn.DefaultWidth);
    }

    [Fact]
    public void AutoColumn_OverridesASetWidth()
    {
        var h = AutoNameGrid(); // the harness sets Width = 150 on every column
        h.People[0].Name = LongName;

        h.Render();

        Assert.Equal(LongNameFit, h.Column("Name").Width);
    }

    [Fact]
    public void AutoColumn_IsHeldToItsMinAndMaxWidth()
    {
        var h = AutoNameGrid();
        h.People[0].Name = LongName;
        var name = h.Column("Name");
        name.MaxWidth = 100;

        h.Render();
        Assert.Equal(100, name.Width);

        name.MaxWidth = float.MaxValue;
        name.MinWidth = 300;
        h.Render();
        Assert.Equal(300, name.Width);
    }

    [Fact]
    public void AutoColumn_MeasuresTheRowsOnScreen_AndTheRowsAtEachEnd_NotEveryRow()
    {
        // 1000 rows, 10 on screen: rows 0-99 and 900-999 are sampled, so a long name at row 500 is not.
        var h = AutoNameGrid(rows: 1000);
        h.People[500].Name = LongName;
        h.People[950].Name = "A tail name";
        var name = h.Column("Name");

        h.Render();
        Assert.Equal(CellFit("A tail name"), name.Width);

        // Scrolling alone measures nothing, so the width does not jump while the grid scrolls...
        h.Scroll.OffsetY = 495 * h.Style.RowHeight;
        h.Render();
        Assert.Equal(CellFit("A tail name"), name.Width);

        // ...but the next change to the view samples the rows then on screen.
        h.Source.Refresh();
        h.Render();
        Assert.Equal(LongNameFit, name.Width);
    }

    [Fact]
    public void AutoColumn_IsMeasuredAgain_WhenTheDataChanges_AndOnlyThen()
    {
        var h = AutoNameGrid();
        var name = h.Column("Name");
        h.Render();
        float before = name.Width;

        // An unnotified change is not seen until the view changes.
        h.People[2].Name = LongName;
        h.Render();
        Assert.Equal(before, name.Width);

        Assert.True(h.Source.SetCellValue(5, name, "Medium length"));
        h.Render();
        Assert.Equal(LongNameFit, name.Width); // the change re-samples every sampled row, row 2 included
    }

    [Fact]
    public void AutoColumn_FollowsANarrowerSample_AfterRowsAreFilteredOut()
    {
        var h = AutoNameGrid();
        h.People[0].Name = LongName;
        var name = h.Column("Name");
        h.Render();
        Assert.Equal(LongNameFit, name.Width);

        h.Source.SetFilter(item => ((RenderHarness.Person)item).Name != LongName);
        h.Render();

        Assert.Equal(name.HeaderFitWidth, name.Width);
    }

    [Fact]
    public void AutoColumn_IsMeasuredAgain_WhenItsCellFontChanges()
    {
        var h = AutoNameGrid();
        h.People[0].Name = LongName;
        var name = h.Column("Name");
        h.Render();
        Assert.Equal(LongNameFit, name.Width);

        // The recording context measures every font alike, so watch the measurement being redone: the cached text
        // width is replaced by one that also sees a change made without notification.
        h.People[1].Name = LongName + LongName;
        name.CellStyle = new KumikoCellStyle { Font = new KumikoFont("Default", 20) };
        h.Render();

        Assert.Equal(CellFit(LongName + LongName), name.Width);
    }

    [Fact]
    public void AutoColumn_WithNoRows_FitsItsHeader_AndGrowsWhenRowsArrive()
    {
        var h = AutoNameGrid(rows: 0);
        var name = h.Column("Name");
        h.Render();
        Assert.Equal(name.HeaderFitWidth, name.Width);

        h.Source.SetItems(new List<RenderHarness.Person> { new() { Name = LongName } });
        h.Render();

        Assert.Equal(LongNameFit, name.Width);
    }

    [Fact]
    public void AutoColumn_TakesItsWidthBeforeStarColumnsShareTheRest()
    {
        var h = AutoNameGrid();
        h.People[0].Name = LongName;
        foreach (var column in h.Source.Columns.Where(c => c.PropertyName != "Name"))
            column.SizeMode = ColumnSizeMode.Star;

        h.Render();

        Assert.Equal(LongNameFit, h.Column("Name").Width);
        Assert.Equal(600, h.Scroll.ContentWidth, 0.01f);
        Assert.Equal((600 - LongNameFit) / 3, h.Column("City").Width, 0.01f);
    }

    [Fact]
    public void ContentFitWidth_IsZero_ForOtherSizeModes()
    {
        var h = AutoNameGrid();
        h.Render();
        var name = h.Column("Name");
        Assert.True(name.ContentFitWidth > 0);

        name.SizeMode = ColumnSizeMode.Fixed;
        h.Render();

        Assert.Equal(0, name.ContentFitWidth);
        Assert.Equal(0, h.Column("Age").ContentFitWidth);
        Assert.Equal(name.HeaderFitWidth, name.Width); // a Fixed column keeps the width it was last laid out at
    }

    // ── Sampled auto-fit ──

    [Fact]
    public void CalculateAutoFitWidth_WithAVisibleRange_AlsoMeasuresTheRowsOnScreen()
    {
        var h = AutoNameGrid(rows: 1000);
        h.People[500].Name = LongName;
        var engine = new KumikoLayoutEngine();
        var ctx = new RecordingDrawingContext();
        var name = h.Column("Name");

        float sampled = engine.CalculateAutoFitWidth(name, h.Source, ctx, h.Style);
        float withScreen = engine.CalculateAutoFitWidth(name, h.Source, ctx, h.Style, 495, 505);

        Assert.Equal(engine.MeasureHeaderFitWidth(name, ctx, h.Style), sampled); // row 500 is not sampled
        Assert.Equal(LongNameFit, withScreen);
    }

    [Fact]
    public void CalculateAutoFitWidth_WithNoSampleRows_MeasuresOnlyTheRowsOnScreen()
    {
        var h = AutoNameGrid(rows: 50);
        h.People[0].Name = LongName;
        h.People[20].Name = "On screen";
        var engine = new KumikoLayoutEngine { AutoFitSampleRows = 0 };
        var name = h.Column("Name");

        float width = engine.CalculateAutoFitWidth(name, h.Source, new RecordingDrawingContext(), h.Style, 18, 22);

        Assert.Equal(CellFit("On screen"), width);
    }

    [Fact]
    public void CalculateAutoFitWidth_UsesTheColumnsCellPadding()
    {
        var h = AutoNameGrid();
        h.People[0].Name = LongName;
        var name = h.Column("Name");
        name.CellStyle = new KumikoCellStyle { Padding = 20 };

        float width = new KumikoLayoutEngine().CalculateAutoFitWidth(name, h.Source, new RecordingDrawingContext(), h.Style);

        Assert.Equal(26 * 8 + 40 + 16, width);
    }

    // ── Resize and auto-fit persist ──

    [Fact]
    public void ResizingAStarColumn_SwitchesItToFixed_AndTheWidthPersists()
    {
        var g = new InputGrid(configure: x =>
        {
            foreach (var column in x.Source.Columns) column.SizeMode = ColumnSizeMode.Star;
        });
        // Five equal stars in 300 px: Name spans 0..60, so its grip starts at x 56.
        Assert.Equal(60, g.Name.Width, 0.01f);

        g.Press(56, InputGrid.HeaderY);
        g.Move(56 + 40, InputGrid.HeaderY);
        g.Release(56 + 40, InputGrid.HeaderY);
        g.Render();
        g.Render();

        Assert.Equal(ColumnSizeMode.Fixed, g.Name.SizeMode);
        Assert.Equal(100, g.Name.Width, 0.01f);
        Assert.Equal(50, g.Age.Width, 0.01f); // the other stars share the 200 px left
        Assert.Equal(ColumnSizeMode.Star, g.Age.SizeMode);
    }

    [Fact]
    public void ResizingAnAutoColumn_SwitchesItToFixed_SoADataChangeDoesNotUndoIt()
    {
        var g = new InputGrid(configure: x => x.Name.SizeMode = ColumnSizeMode.Auto);
        float fitted = g.Name.Width;
        float grip = fitted - 4;

        g.Press(grip, InputGrid.HeaderY);
        g.Move(grip + 50, InputGrid.HeaderY);
        g.Release(grip + 50, InputGrid.HeaderY);
        g.Source.Refresh();
        g.Render();

        Assert.Equal(ColumnSizeMode.Fixed, g.Name.SizeMode);
        Assert.Equal(fitted + 50, g.Name.Width, 0.01f);
    }

    [Fact]
    public void AutoFittingAStarColumn_SwitchesItToFixed_AndTheWidthPersists()
    {
        var h = RenderHarness.Create();
        h.People[0].Name = LongName;
        foreach (var column in h.Source.Columns) column.SizeMode = ColumnSizeMode.Star;
        var name = h.Column("Name");
        h.Render();
        Assert.Equal(150, name.Width, 0.01f);

        float width = h.Renderer.AutoFitColumn(name, h.Source, new RecordingDrawingContext(), h.Style);
        h.Render();
        h.Render();

        Assert.Equal(LongNameFit, width);
        Assert.Equal(LongNameFit, name.Width, 0.01f);
        Assert.Equal(ColumnSizeMode.Fixed, name.SizeMode);
        Assert.True(name.HasExplicitWidth);
        Assert.Equal((600 - LongNameFit) / 3, h.Column("Age").Width, 0.01f);
    }

    [Fact]
    public void AutoFitColumn_SamplesTheRowsOnScreenInTheLastFrame()
    {
        var h = RenderHarness.Create(rows: 1000);
        h.People[500].Name = LongName;
        var name = h.Column("Name");
        h.Scroll.OffsetY = 495 * h.Style.RowHeight;
        h.Render();

        float width = h.Renderer.AutoFitColumn(name, h.Source, new RecordingDrawingContext(), h.Style);

        Assert.Equal(LongNameFit, width);
    }

    [Fact]
    public void DoubleClickingTheGripOfAStarColumn_ThenAutoFitting_KeepsTheFittedWidth()
    {
        var g = new InputGrid(configure: x => x.Name.SizeMode = ColumnSizeMode.Star);
        g.Input.AutoFitColumnRequested += (_, e) =>
            g.Renderer.AutoFitColumn(e.Column, g.Source, new RecordingDrawingContext(), g.Style);
        float grip = g.Name.Width - 4;

        g.Press(grip, InputGrid.HeaderY, clicks: 2);
        g.Render();

        // "Name" header fit is 78; the "N00".."N29" cells fit in 56.
        Assert.Equal(ColumnSizeMode.Fixed, g.Name.SizeMode);
        Assert.Equal(78, g.Name.Width, 0.01f);
    }

    // ── Star distribution after min/max clamping ──

    [Fact]
    public void TwoStarColumns_OneHeldAtItsMaxWidth_TheOtherTakesTheSlack()
    {
        var h = RenderHarness.Create(width: 500, configure: x => x.Source.SetColumns(new[]
        {
            new KumikoColumn { Header = "A", PropertyName = "Name", SizeMode = ColumnSizeMode.Star, MaxWidth = 100 },
            new KumikoColumn { Header = "B", PropertyName = "City", SizeMode = ColumnSizeMode.Star },
        }));

        h.Render();

        Assert.Equal(100, h.Source.Columns[0].Width, 0.01f);
        Assert.Equal(400, h.Source.Columns[1].Width, 0.01f);
        Assert.Equal(500, h.Scroll.ContentWidth, 0.01f);
    }

    [Fact]
    public void ThreeStarColumns_ClampedAtMaxAndMin_TheOpenOneTakesWhatIsLeft()
    {
        var a = new KumikoColumn { Header = "A", PropertyName = "Name", SizeMode = ColumnSizeMode.Star, StarWeight = 2, MaxWidth = 120 };
        var b = new KumikoColumn { Header = "B", PropertyName = "City", SizeMode = ColumnSizeMode.Star, MinWidth = 250 };
        var c = new KumikoColumn { Header = "C", PropertyName = "Age", SizeMode = ColumnSizeMode.Star };
        var h = RenderHarness.Create(width: 600, configure: x => x.Source.SetColumns(new[] { a, b, c }));

        h.Render();

        // Even shares by weight would be 300 / 150 / 150: A is held at 120 and B at 250, so C gets 230.
        Assert.Equal(120, a.Width, 0.01f);
        Assert.Equal(250, b.Width, 0.01f);
        Assert.Equal(230, c.Width, 0.01f);
        Assert.Equal(600, h.Scroll.ContentWidth, 0.01f);
    }
}
