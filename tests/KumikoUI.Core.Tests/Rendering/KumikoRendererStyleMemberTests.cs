using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.TestHelpers.RenderHarness;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// Each <see cref="KumikoStyle"/> member that styles one part of the grid changes what that part draws:
/// the selection text color, the group header's count, the caption summary and the summary row labels.
/// </summary>
public class KumikoRendererStyleMemberTests
{
    private static readonly KumikoFont CustomFont = new("Custom", 21, bold: true);

    // ── SelectionTextColor ────────────────────────────────────────

    [Fact]
    public void SelectionTextColor_DrawsTheTextOfSelectedRowsOnly()
    {
        var h = Create(rows: 6);
        h.Selection.HandleRowSelection(2, shift: false, ctrl: false);

        var ctx = h.Render();

        Assert.Equal(h.Style.SelectionTextColor, ctx.SingleText("P02").Color);
        Assert.Equal(h.Style.CellTextColor, ctx.SingleText("P03").Color);
        Assert.NotEqual(h.Style.SelectionTextColor, h.Style.CellTextColor);
    }

    [Fact]
    public void SelectionTextColor_ChangingIt_ChangesTheSelectedText()
    {
        var h = Create(rows: 6);
        h.Selection.HandleRowSelection(2, shift: false, ctrl: false);
        var custom = new KumikoColor(1, 2, 3);
        h.Style.SelectionTextColor = custom;

        var ctx = h.Render();

        Assert.Equal(custom, ctx.SingleText("P02").Color);
    }

    [Fact]
    public void SelectionTextColor_KeepsTheCellFont()
    {
        var h = Create(rows: 6);
        h.Style.CellFont = CustomFont;
        h.Selection.HandleRowSelection(2, shift: false, ctrl: false);

        var ctx = h.Render();

        Assert.Equal(CustomFont, ctx.SingleText("P02").Font);
    }

    [Fact]
    public void SelectionTextColor_CellStyleTextColor_TakesPrecedence()
    {
        var h = Create(rows: 6);
        var own = new KumikoColor(9, 8, 7);
        h.Style.CellStyleResolver = (_, col) => col.PropertyName == "Name" ? new KumikoCellStyle { TextColor = own } : null;
        h.Selection.HandleRowSelection(2, shift: false, ctrl: false);

        var ctx = h.Render();

        Assert.Equal(own, ctx.SingleText("P02").Color);
        // The cell resolver sets no color for Age, so that cell takes the selection color.
        Assert.Equal(h.Style.SelectionTextColor, ctx.SingleText("22").Color);
    }

    [Fact]
    public void SelectionTextColor_RowStyleTextColor_TakesPrecedence()
    {
        var h = Create(rows: 6);
        var own = new KumikoColor(9, 8, 7);
        h.Style.RowStyleResolver = _ => new KumikoRowStyle { TextColor = own };
        h.Selection.HandleRowSelection(2, shift: false, ctrl: false);

        var ctx = h.Render();

        Assert.Equal(own, ctx.SingleText("P02").Color);
    }

    [Fact]
    public void SelectionTextColor_RowStyleFontOnly_StillUsesTheSelectionColor()
    {
        var h = Create(rows: 6);
        h.Style.RowStyleResolver = _ => new KumikoRowStyle { Font = CustomFont };
        h.Selection.HandleRowSelection(2, shift: false, ctrl: false);

        var text = h.Render().SingleText("P02");

        Assert.Equal(h.Style.SelectionTextColor, text.Color);
        Assert.Equal(CustomFont, text.Font);
    }

    // ── Group header count and caption summary ────────────────────

    [Fact]
    public void GroupHeaderCount_UsesItsOwnColorAndFont()
    {
        var h = Create();
        h.Style.GroupHeaderFont = new KumikoFont("Head", 15, bold: true);
        h.Style.GroupCountFont = CustomFont;
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render();

        var count = ctx.SingleText(" (10)");
        Assert.Equal(h.Style.GroupHeaderCountColor, count.Color);
        Assert.Equal(CustomFont, count.Font);
        var title = ctx.SingleText("City: Oslo");
        Assert.Equal(h.Style.GroupHeaderTextColor, title.Color);
        Assert.Equal(h.Style.GroupHeaderFont, title.Font);
    }

    [Fact]
    public void CaptionSummary_UsesItsOwnColorAndFont()
    {
        var h = Create();
        h.Style.CaptionSummaryFont = CustomFont;
        h.Source.CaptionSummary = new CaptionSummaryDescription { Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } };
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render();

        var caption = ctx.Texts.First(t => t.Text!.StartsWith("  —  ", StringComparison.Ordinal));
        Assert.Equal(h.Style.CaptionSummaryTextColor, caption.Color);
        Assert.Equal(CustomFont, caption.Font);
        Assert.NotEqual(h.Style.GroupHeaderTextColor, caption.Color);
    }

    [Fact]
    public void GroupHeader_RunsFollowEachOther_WithoutCaption()
    {
        var h = Create();
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render();

        Assert.Empty(ctx.Texts.Where(t => t.Color == h.Style.CaptionSummaryTextColor));
    }

    [Fact]
    public void GroupHeader_WhenTheTitleFillsTheRow_DrawsNoCountOrCaption()
    {
        var h = Create(width: 100);
        h.Source.AddGroupDescription(new GroupDescription("City"));

        var ctx = h.Render();

        // "City: Oslo" is 10 characters wide, past the right padding of a 100 px row.
        Assert.Empty(ctx.TextsEqual(" (10)"));
    }

    // ── Summary row labels ────────────────────────────────────────

    [Fact]
    public void SummaryRowLabelFont_DrawsTheTitleInTheLabelFont_AndValuesInTheValueFont()
    {
        var h = Create();
        h.Style.SummaryRowLabelFont = CustomFont;
        h.Source.AddTableSummaryRow(new TableSummaryRow("Totals", SummaryPosition.Top)
        {
            Title = "Totals",
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) },
        });

        var ctx = h.Render();

        var title = ctx.SingleText("Totals");
        Assert.Equal(CustomFont, title.Font);
        Assert.Equal(h.Style.SummaryRowLabelColor, title.Color);
        var value = ctx.SingleText(h.Source.ComputedTopSummaries[0].Values["Age"]);
        Assert.Equal(h.Style.SummaryRowFont, value.Font);
    }
}
