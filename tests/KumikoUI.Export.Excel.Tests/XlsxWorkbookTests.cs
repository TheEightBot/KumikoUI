using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using KumikoUI.Core.Export;
using KumikoUI.Core.Models;
using static KumikoUI.Export.Excel.Tests.Xlsx;

namespace KumikoUI.Export.Excel.Tests;

public class XlsxWorkbookTests
{
    [Fact]
    public void Exporter_DescribesTheFormat()
    {
        var exporter = new KumikoXlsxExporter();
        Assert.Equal("Excel", exporter.FormatName);
        Assert.Equal(".xlsx", exporter.FileExtension);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", exporter.MediaType);
    }

    [Fact]
    public async Task DefaultWorkbook_HasTheRequiredPartsAndValidates()
    {
        var bytes = await ExportAsync(Source());

        Assert.Equal(new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml" },
            PartNames(bytes));
        AssertValid(bytes);
        Assert.Equal("A1:E6", (string)Sheet(bytes).Descendants(Ns + "dimension").Single().Attribute("ref")!);
        Assert.Equal("Sheet1", (string)Part(bytes, "xl/workbook.xml").Descendants(Ns + "sheet").Single().Attribute("name")!);
    }

    [Fact]
    public async Task RoundTrip_ReadsBackTypedValuesWithTheOpenXmlSdk()
    {
        var bytes = await ExportAsync(Source());

        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        var workbookPart = document.WorkbookPart!;
        var sheet = workbookPart.WorksheetParts.Single().Worksheet!;
        var rows = sheet.Descendants<Row>().ToList();
        Assert.Equal(6, rows.Count);

        var header = rows[0].Elements<Cell>().Select(c => c.InlineString!.Text!.Text).ToArray();
        Assert.Equal(new[] { "Name", "Dept", "Salary", "Hired", "Active" }, header);

        var ada = rows[1].Elements<Cell>().ToList();
        Assert.Equal(CellValues.InlineString, ada[0].DataType!.Value);
        Assert.Equal("Ada", ada[0].InlineString!.Text!.Text);
        Assert.Null(ada[2].DataType); // a number
        Assert.Equal(1200.5, double.Parse(ada[2].CellValue!.Text, CultureInfo.InvariantCulture));
        Assert.Equal(new DateTime(2020, 1, 15), DateTime.FromOADate(double.Parse(ada[3].CellValue!.Text, CultureInfo.InvariantCulture)));
        Assert.Equal(CellValues.Boolean, ada[4].DataType!.Value);
        Assert.Equal("1", ada[4].CellValue!.Text);

        var formats = workbookPart.WorkbookStylesPart!.Stylesheet!.NumberingFormats!.Elements<NumberingFormat>()
            .ToDictionary(n => n.NumberFormatId!.Value, n => n.FormatCode!.Value);
        var xfs = workbookPart.WorkbookStylesPart.Stylesheet!.CellFormats!.Elements<CellFormat>().ToList();
        Assert.Equal("#,##0.00", formats[xfs[(int)ada[2].StyleIndex!.Value].NumberFormatId!.Value]);
        Assert.Equal("yyyy\"-\"mm\"-\"dd", formats[xfs[(int)ada[3].StyleIndex!.Value].NumberFormatId!.Value]);
    }

    [Fact]
    public async Task HeaderRow_IsBoldAndFollowsTheColumnAlignment()
    {
        var bytes = await ExportAsync(Source());
        var cells = Cells(bytes);

        Assert.NotNull(Font(bytes, cells["A1"].Style).Element(Ns + "b"));
        Assert.Null(Font(bytes, cells["A2"].Style).Element(Ns + "b"));
        Assert.Equal("right", (string?)CellFormat(bytes, cells["C1"].Style).Element(Ns + "alignment")?.Attribute("horizontal"));
        Assert.Equal("right", (string?)CellFormat(bytes, cells["C2"].Style).Element(Ns + "alignment")?.Attribute("horizontal"));
        Assert.Null(CellFormat(bytes, cells["A2"].Style).Element(Ns + "alignment"));

        var plain = await ExportAsync(Source(), xlsx: new KumikoXlsxOptions { ApplyColumnAlignment = false });
        Assert.Null(CellFormat(plain, Cells(plain)["C2"].Style).Element(Ns + "alignment"));
    }

    [Fact]
    public async Task FreezePane_KeepsTheHeaderAndTheFrozenColumnsInView()
    {
        var bytes = await ExportAsync(Source());
        var pane = Sheet(bytes).Descendants(Ns + "pane").Single();
        Assert.Equal("1", (string?)pane.Attribute("ySplit"));
        Assert.Null(pane.Attribute("xSplit"));
        Assert.Equal("frozen", (string?)pane.Attribute("state"));
        Assert.Equal("A2", (string?)pane.Attribute("topLeftCell"));
        Assert.Equal("bottomLeft", (string?)pane.Attribute("activePane"));

        var source = Source();
        source.Columns[0].IsFrozen = true;
        var frozen = await ExportAsync(source);
        pane = Sheet(frozen).Descendants(Ns + "pane").Single();
        Assert.Equal("1", (string?)pane.Attribute("xSplit"));
        Assert.Equal("B2", (string?)pane.Attribute("topLeftCell"));
        Assert.Equal("bottomRight", (string?)pane.Attribute("activePane"));
        AssertValid(frozen);

        var none = await ExportAsync(Source(), xlsx: new KumikoXlsxOptions { FreezeHeader = false });
        Assert.Empty(Sheet(none).Descendants(Ns + "pane"));
    }

    [Fact]
    public async Task AutoFilter_CoversTheHeaderAndTheRows()
    {
        var bytes = await ExportAsync(Source());
        Assert.Equal("A1:E6", (string?)Sheet(bytes).Descendants(Ns + "autoFilter").Single().Attribute("ref"));
        var name = Part(bytes, "xl/workbook.xml").Descendants(Ns + "definedName").Single();
        Assert.Equal("_xlnm._FilterDatabase", (string?)name.Attribute("name"));
        Assert.Equal("'Sheet1'!$A$1:$E$6", name.Value);

        var off = await ExportAsync(Source(), xlsx: new KumikoXlsxOptions { AutoFilter = false });
        Assert.Empty(Sheet(off).Descendants(Ns + "autoFilter"));
        Assert.Empty(Part(off, "xl/workbook.xml").Descendants(Ns + "definedName"));

        var noHeader = await ExportAsync(Source(), new KumikoExportOptions { IncludeHeaders = false });
        Assert.Empty(Sheet(noHeader).Descendants(Ns + "autoFilter"));
        Assert.Empty(Sheet(noHeader).Descendants(Ns + "pane"));
        Assert.Equal("A1:E5", (string)Sheet(noHeader).Descendants(Ns + "dimension").Single().Attribute("ref")!);
        AssertValid(noHeader);
    }

    [Fact]
    public async Task ColumnWidths_ComeFromTheGridOrAreLeftToExcel()
    {
        var bytes = await ExportAsync(Source());
        var cols = Sheet(bytes).Descendants(Ns + "col").ToList();
        Assert.Equal(5, cols.Count);
        Assert.Equal(20.0, (double)cols[0].Attribute("width")!); // 140 px / 7 px per character
        Assert.Equal(Math.Truncate(KumikoColumn.DefaultWidth / 7.0 * 256) / 256, (double)cols[1].Attribute("width")!);

        var fixedWidths = await ExportAsync(Source(), xlsx: new KumikoXlsxOptions { ColumnWidths = KumikoXlsxColumnWidths.Fixed });
        Assert.Empty(Sheet(fixedWidths).Descendants(Ns + "cols"));
    }

    [Fact]
    public async Task GroupedRows_BecomeAnOutline()
    {
        var source = Source();
        source.AddGroupDescription(new GroupDescription("Dept"));
        source.AddGroupSummaryRow(new GroupSummaryRow("sum") { Title = "Subtotal", Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } });
        source.AddTableSummaryRow(new TableSummaryRow { Name = "total", Title = "Total", Position = SummaryPosition.Bottom, Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } });

        var bytes = await ExportAsync(source);
        AssertValid(bytes);
        var sheet = Sheet(bytes);
        Assert.Equal("0", (string?)sheet.Descendants(Ns + "outlinePr").Single().Attribute("summaryBelow"));
        Assert.Equal("1", (string?)sheet.Descendants(Ns + "sheetFormatPr").Single().Attribute("outlineLevelRow"));

        var rows = sheet.Descendants(Ns + "row").Select(r => (int?)r.Attribute("outlineLevel") ?? 0).ToList();
        // header, Eng group (header, 3 rows, subtotal), Ops group (header, 2 rows, subtotal), total
        Assert.Equal(new[] { 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 0 }, rows);

        var cells = Cells(bytes);
        Assert.StartsWith("Dept: Eng (3)", cells["A2"].Text);
        Assert.NotNull(Font(bytes, cells["A2"].Style).Element(Ns + "b"));
        Assert.Equal("Subtotal", cells["A6"].Text);
        Assert.Equal(3500.5, double.Parse(cells["C6"].Value!, CultureInfo.InvariantCulture));
        Assert.Equal("General", NumberFormat(bytes, cells["C6"].Style));
        Assert.Equal("Total", cells["A11"].Text);
        Assert.Equal(5100.75, double.Parse(cells["C11"].Value!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task NestedGroups_UseOneOutlineLevelPerGroupLevel()
    {
        var source = Source();
        source.AddGroupDescription(new GroupDescription("Dept"));
        source.AddGroupDescription(new GroupDescription("Active"));
        var bytes = await ExportAsync(source);
        AssertValid(bytes);

        var sheet = Sheet(bytes);
        Assert.Equal("2", (string?)sheet.Descendants(Ns + "sheetFormatPr").Single().Attribute("outlineLevelRow"));
        var levels = sheet.Descendants(Ns + "row").Select(r => (int?)r.Attribute("outlineLevel") ?? 0).ToList();
        Assert.Contains(2, levels);
        Assert.Equal(0, levels[1]); // the outer group header
        Assert.Equal(1, levels[2]); // the first inner group header
    }

    [Fact]
    public async Task ColumnsLayout_WritesTheGroupKeyColumnsAndNoOutline()
    {
        var source = Source();
        source.AddGroupDescription(new GroupDescription("Dept"));
        var bytes = await ExportAsync(source, new KumikoExportOptions { GroupLayout = KumikoExportGroupLayout.Columns });
        AssertValid(bytes);

        var cells = Cells(bytes);
        Assert.Equal("Dept", cells["A1"].Text);
        Assert.Equal("Eng", cells["A2"].Text);
        Assert.Equal("Ada", cells["B2"].Text);
        Assert.Empty(Sheet(bytes).Descendants(Ns + "outlinePr"));
        Assert.All(Sheet(bytes).Descendants(Ns + "row"), r => Assert.Null(r.Attribute("outlineLevel")));
    }

    [Fact]
    public async Task GridColors_FillTheHeaderGroupAndSummaryRows()
    {
        var style = new KumikoStyle { HeaderBackgroundColor = new(10, 20, 30), HeaderTextColor = new(250, 251, 252) };
        var source = Source();
        source.AddGroupDescription(new GroupDescription("Dept"));
        var bytes = await ExportAsync(source, xlsx: new KumikoXlsxOptions { UseGridColors = true, Style = style });
        AssertValid(bytes);

        var cells = Cells(bytes);
        var styles = Part(bytes, "xl/styles.xml");
        int fillId = (int)CellFormat(bytes, cells["A1"].Style).Attribute("fillId")!;
        var fill = styles.Descendants(Ns + "fills").Single().Elements(Ns + "fill").ElementAt(fillId);
        Assert.Equal("FF0A141E", (string?)fill.Descendants(Ns + "fgColor").Single().Attribute("rgb"));
        Assert.Equal("FFFAFBFC", (string?)Font(bytes, cells["A1"].Style).Element(Ns + "color")?.Attribute("rgb"));

        // The group header band is painted across the row, its other cells empty so the title still overflows.
        Assert.True(cells.ContainsKey("E2"));
        Assert.Null(cells["E2"].Text);
        Assert.NotEqual(0, (int)CellFormat(bytes, cells["E2"].Style).Attribute("fillId")!);
    }

    [Fact]
    public async Task DocumentProperties_AreWrittenWhenGiven()
    {
        var bytes = await ExportAsync(Source(), xlsx: new KumikoXlsxOptions { Title = "Staff & pay", Author = "Ops <team>" });
        AssertValid(bytes);
        Assert.Contains("docProps/core.xml", PartNames(bytes));

        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        Assert.Equal("Staff & pay", document.PackageProperties.Title);
        Assert.Equal("Ops <team>", document.PackageProperties.Creator);
    }

    [Fact]
    public async Task SheetName_IsSanitizedInTheWorkbookAndTheFilterName()
    {
        var bytes = await ExportAsync(Source(), xlsx: new KumikoXlsxOptions { SheetName = "Q1: [Bob's] *sales*?/\\" });
        AssertValid(bytes);
        Assert.Equal("Q1 Bob's sales", (string)Part(bytes, "xl/workbook.xml").Descendants(Ns + "sheet").Single().Attribute("name")!);
        Assert.Equal("'Q1 Bob''s sales'!$A$1:$E$6", Part(bytes, "xl/workbook.xml").Descendants(Ns + "definedName").Single().Value);
    }

    [Fact]
    public async Task DisplayTextMode_WritesEveryCellAsTheGridText()
    {
        var bytes = await ExportAsync(Source(), new KumikoExportOptions { ValueMode = KumikoExportValueMode.DisplayText });
        AssertValid(bytes);
        var cells = Cells(bytes);
        Assert.All(cells.Values, c => Assert.Equal("inlineStr", c.Type));
        Assert.Equal("1,200.50", cells["C2"].Text);
        Assert.Equal("2020-01-15", cells["D2"].Text);
        Assert.Equal("True", cells["E2"].Text);
    }

    [Fact]
    public async Task InvalidOptions_ThrowBeforeAnythingIsWritten()
    {
        var stream = new ForwardOnlyStream();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Source().ExportToExcelAsync(stream, xlsx: new KumikoXlsxOptions { ColumnWidths = (KumikoXlsxColumnWidths)42 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Source().ExportToExcelAsync(stream, new KumikoExportOptions { Rows = KumikoExportRows.Selected }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((KumikoSource)null!).ExportToExcelAsync(stream));
        Assert.Equal(0, stream.BytesWritten);
    }
}
