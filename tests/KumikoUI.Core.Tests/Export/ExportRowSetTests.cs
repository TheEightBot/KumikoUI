using System.Globalization;
using KumikoUI.Core.Export;
using KumikoUI.Core.Models;
using static KumikoUI.Core.Tests.Export.ExportTests;

namespace KumikoUI.Core.Tests.Export;

public class ExportScopeTests
{
    private static List<string> Names(string csv) => Rfc4180.Parse(csv).Skip(1).Select(r => r[0]).ToList();

    private static KumikoExportOptions NameOnly(KumikoSource s, KumikoExportOptions? o = null) =>
        (o ?? new KumikoExportOptions()) with { Columns = new[] { Col(s, "Name") } };

    [Fact]
    public void Filtered_FollowsFiltersAndSort()
    {
        var source = Source();
        source.SetFilter(p => ((Person)p).Dept == "Eng");
        source.SetSort(Col(source, "Salary"), KumikoSortDirection.Descending);
        Assert.Equal(new[] { "Cy", "Ada", "Ed" }, Names(source.ExportToCsvString(NameOnly(source))));
    }

    [Fact]
    public void All_IgnoresFiltersAndSort_UsesSourceOrder()
    {
        var source = Source();
        source.SetFilter(p => ((Person)p).Dept == "Eng");
        source.SetSort(Col(source, "Salary"), KumikoSortDirection.Descending);
        var options = NameOnly(source, new KumikoExportOptions { Rows = KumikoExportRows.All });
        Assert.Equal(new[] { "Ada", "Bob", "Cy", "Di", "Ed" }, Names(source.ExportToCsvString(options)));
    }

    [Fact]
    public void Filtered_IncludesRowsAndSummariesOfCollapsedGroups_Visible_DoesNot()
    {
        var source = Source();
        source.AddGroupDescription(new GroupDescription("Dept"));
        source.AddGroupSummaryRow(new GroupSummaryRow("sum") { Title = "Total", Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) } });
        source.CollapseAllGroups();
        Assert.Equal(2, source.RowCount); // two collapsed headers

        var columns = new[] { Col(source, "Name"), Col(source, "Salary") };
        var filtered = Rfc4180.Parse(source.ExportToCsvString(new KumikoExportOptions { Columns = columns }));
        Assert.Equal(new[] { "Name", "Dept: Eng (3)", "Ada", "Cy", "Ed", "Total", "Dept: Ops (2)", "Bob", "Di", "Total" },
            filtered.Select(r => r[0]).ToList());
        Assert.Equal("Sum: 3,500.50", filtered[5][1].Replace("3500.5", "3,500.50"));

        var visible = Rfc4180.Parse(source.ExportToCsvString(new KumikoExportOptions { Columns = columns, Rows = KumikoExportRows.Visible }));
        Assert.Equal(new[] { "Name", "Dept: Eng (3)", "Dept: Ops (2)" }, visible.Select(r => r[0]).ToList());

        Assert.Equal(2, source.RowCount); // the live view was not touched
        Assert.False(source.GetGroupHeaderInfo(0)!.IsExpanded);
    }

    [Fact]
    public void Selected_WritesViewOrder_IgnoresDuplicatesAndGroupRows()
    {
        var source = Source();
        source.SetSort(Col(source, "Name"), KumikoSortDirection.Descending); // Ed, Di, Cy, Bob, Ada
        var all = Enumerable.Range(0, source.RowCount).Select(source.GetItem).ToList();
        var pick = new object[] { all[4], all[0], all[0], all[2] };
        string csv = source.ExportToCsvString(NameOnly(source, new KumikoExportOptions { Rows = KumikoExportRows.Selected, Selection = KumikoExportSelection.FromItems(pick) }));
        Assert.Equal(new[] { "Ed", "Cy", "Ada" }, Names(csv));

        string byRows = source.ExportToCsvString(NameOnly(source, new KumikoExportOptions { Rows = KumikoExportRows.Selected, Selection = KumikoExportSelection.FromRows(new[] { 3, 1, 1, -4, 99 }) }));
        Assert.Equal(new[] { "Di", "Bob" }, Names(byRows)); // rows 3 and 1 of Ed, Di, Cy, Bob, Ada, in view order
    }

    [Fact]
    public void Selected_FromSelectionModel_UsesTheItemsSelectedWhenTheExportStarts()
    {
        var source = Source();
        var model = new KumikoSelectionModel(source);
        model.SetSelectedItems(new[] { source.GetItem(1), source.GetItem(3) });
        var options = NameOnly(source, new KumikoExportOptions { Rows = KumikoExportRows.Selected, Selection = KumikoExportSelection.FromSelection(model) });
        model.Select(source.GetItem(0));
        Assert.Equal(new[] { "Ada", "Bob", "Di" }, Names(source.ExportToCsvString(options)));
    }

    [Fact]
    public void Selected_InGroups_KeepsTheHeaderOfGroupsThatHaveSelectedRowsOnly()
    {
        var source = Source();
        source.AddGroupDescription(new GroupDescription("Dept"));
        var ed = Enumerable.Range(0, source.RowCount).Where(r => !source.IsNonDataRow(r)).Select(source.GetItem).Cast<Person>().Single(p => p.Name == "Ed");
        string csv = source.ExportToCsvString(NameOnly(source, new KumikoExportOptions { Rows = KumikoExportRows.Selected, Selection = KumikoExportSelection.FromItems(new object[] { ed }) }));
        Assert.Equal(new[] { "Dept: Eng (3)", "Ed" }, Names(csv));
    }

    [Fact]
    public async Task Selected_WithoutASelection_ThrowsBeforeWritingAnything()
    {
        var stream = new CountingStream();
        await Assert.ThrowsAsync<ArgumentException>(() => Source().ExportToCsvAsync(stream, new KumikoExportOptions { Rows = KumikoExportRows.Selected }));
        Assert.Equal(0, stream.BytesWritten);
    }

    [Fact]
    public async Task InvalidDelimiter_ThrowsBeforeWritingAnything()
    {
        var stream = new CountingStream();
        await Assert.ThrowsAsync<ArgumentException>(() => Source().ExportToCsvAsync(stream, csv: new KumikoCsvOptions { Delimiter = '"' }));
        Assert.Equal(0, stream.BytesWritten);
    }

    [Fact]
    public void NoColumns_Throws()
    {
        var source = Source();
        Assert.Throws<ArgumentException>(() => source.ExportToCsvString(new KumikoExportOptions { Columns = Array.Empty<KumikoColumn>() }));
    }
}

public class ExportColumnTests
{
    private static string Header(string csv) => csv.Split("\r\n")[0];

    [Fact]
    public void HiddenColumnsAreSkipped_UnlessIncluded()
    {
        var source = Source();
        Col(source, "Dept").IsVisible = false;
        Assert.Equal("Name,Salary,Hired,Active", Header(source.ExportToCsvString()));
        Assert.Equal("Name,Dept,Salary,Hired,Active", Header(source.ExportToCsvString(new KumikoExportOptions { IncludeHiddenColumns = true })));
    }

    [Fact]
    public void ExplicitColumns_KeepTheirOrder_AndOverrideVisibility()
    {
        var source = Source();
        Col(source, "Dept").IsVisible = false;
        string csv = source.ExportToCsvString(new KumikoExportOptions { Columns = new[] { Col(source, "Active"), Col(source, "Dept") } });
        Assert.Equal("Active,Dept", Header(csv));
    }

    [Fact]
    public void ColumnFilterAndHeaderSelector()
    {
        var source = Source();
        string csv = source.ExportToCsvString(new KumikoExportOptions
        {
            ColumnFilter = c => c.PropertyName is "Name" or "Salary",
            HeaderSelector = c => c.PropertyName.ToUpperInvariant(),
        });
        Assert.Equal("NAME,SALARY", Header(csv));
        Assert.StartsWith("Name,Salary", Header(source.ExportToCsvString(new KumikoExportOptions { ColumnFilter = c => c.PropertyName is "Name" or "Salary" })));
    }

    [Fact]
    public void IncludeHeadersFalse_WritesDataOnly()
    {
        var source = Source();
        Assert.StartsWith("Ada,", source.ExportToCsvString(new KumikoExportOptions { IncludeHeaders = false }));
    }

    [Fact]
    public void FrozenColumns_AreWrittenInPaneOrder()
    {
        var source = Source();
        Col(source, "Active").IsFrozen = true;
        Col(source, "Name").IsFrozenRight = true;
        Assert.Equal("Active,Dept,Salary,Hired,Name", Header(source.ExportToCsvString()));
        Assert.Equal(1, KumikoExportSnapshot.Create(source).FrozenColumnCount);
    }

    [Fact]
    public void ImageTemplateAndValuelessCustomColumns_AreSkippedUnlessListed()
    {
        var source = Source();
        var image = new KumikoColumn { Header = "Img", PropertyName = "Name", ColumnType = KumikoColumnType.Image };
        var template = new KumikoColumn { Header = "Tpl", PropertyName = "Name", ColumnType = KumikoColumnType.Template };
        var name = source.Columns[0];
        source.SetColumns(source.Columns.Concat(new[] { image, template }).ToArray());
        Assert.Equal("Name,Dept,Salary,Hired,Active", Header(source.ExportToCsvString()));
        Assert.Equal("Name,Img", Header(source.ExportToCsvString(new KumikoExportOptions { Columns = new[] { name, image } })));
    }
}

public class ExportGroupTests
{
    private static KumikoSource Grouped()
    {
        var source = Source();
        source.AddGroupDescription(new GroupDescription("Dept"));
        source.AddGroupSummaryRow(new GroupSummaryRow("g") { Title = "Subtotal", Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) { Label = "" } } });
        source.AddTableSummaryRow(new TableSummaryRow("top", SummaryPosition.Top) { Title = "Grand", Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) { Label = "" } } });
        source.AddTableSummaryRow(new TableSummaryRow("bottom", SummaryPosition.Bottom) { Title = "Count", Columns = { new SummaryColumnDescription("Name", SummaryType.Count) { Label = "" } } });
        return source;
    }

    [Fact]
    public void RowsLayout_MirrorsTheGridsFlatView()
    {
        var source = Grouped();
        var options = new KumikoExportOptions { Columns = new[] { Col(source, "Name"), Col(source, "Salary") }, ValueMode = KumikoExportValueMode.DisplayText };
        var exported = KumikoExportSnapshot.Create(source, options).Rows.Select(r => (r.Kind, r.Level, r.Cells[0].Text)).ToList();

        var expected = new List<(KumikoExportRowKind, int, string)> { (KumikoExportRowKind.Header, 0, "Name"), (KumikoExportRowKind.TableSummary, 0, "Grand") };
        for (int i = 0; i < source.RowCount; i++)
        {
            if (source.GetGroupHeaderInfo(i) is { } h)
                expected.Add((KumikoExportRowKind.GroupHeader, h.Level, $"{h.HeaderText}: {h.DisplayText} ({h.ItemCount})"));
            else if (source.IsGroupSummaryRow(i)) expected.Add((KumikoExportRowKind.GroupSummary, 1, "Subtotal"));
            else expected.Add((KumikoExportRowKind.Data, 1, ((Person)source.GetItem(i)).Name));
        }
        expected.Add((KumikoExportRowKind.TableSummary, 0, "5")); // the Count in the first column, so no title
        Assert.Equal(expected, exported);
    }

    [Fact]
    public void SummaryValuesLandInTheColumnOfTheirProperty()
    {
        var source = Grouped();
        var rows = Rfc4180.Parse(source.ExportToCsvString(new KumikoExportOptions { Columns = new[] { Col(source, "Name"), Col(source, "Salary") } }));
        Assert.Equal(new[] { "Grand", "5100.75" }, rows[1]);
        Assert.Equal(new[] { "Subtotal", "3500.5" }, rows[6]);
        Assert.Equal(new[] { "5", "" }, rows[^1]);
    }

    [Fact]
    public void ColumnsLayout_PrefixesKeyColumns_AndOmitsGroupRows()
    {
        var source = Source();
        source.AddGroupDescription(new GroupDescription("Dept"));
        var rows = Rfc4180.Parse(source.ExportToCsvString(new KumikoExportOptions { GroupLayout = KumikoExportGroupLayout.Columns, Columns = new[] { Col(source, "Name") } }));
        Assert.Equal(new[] { "Dept", "Name" }, rows[0]);
        Assert.Equal(new[] { "Eng", "Ada" }, rows[1]);
        Assert.Equal(new[] { "Ops", "Di" }, rows[5]);
        Assert.Equal(6, rows.Count);
    }

    [Fact]
    public void ColumnsLayout_NestedGroups_HaveOneKeyColumnPerLevel()
    {
        var source = Source();
        source.AddGroupDescription(new GroupDescription("Dept"));
        source.AddGroupDescription(new GroupDescription("Active"));
        var rows = Rfc4180.Parse(source.ExportToCsvString(new KumikoExportOptions { GroupLayout = KumikoExportGroupLayout.Columns, Columns = new[] { Col(source, "Name") } }));
        Assert.Equal(new[] { "Dept", "Active", "Name" }, rows[0]);
        Assert.All(rows.Skip(1), r => Assert.Equal(3, r.Count));
        Assert.Contains(rows, r => r.SequenceEqual(new[] { "Eng", "False", "Ed" }));
    }

    [Fact]
    public void NoneLayout_IgnoresGroupingAndUsesTheGridSort()
    {
        var source = Source();
        source.AddGroupDescription(new GroupDescription("Dept"));
        source.SetSort(Col(source, "Name"), KumikoSortDirection.Descending);
        var rows = Rfc4180.Parse(source.ExportToCsvString(new KumikoExportOptions { GroupLayout = KumikoExportGroupLayout.None, Columns = new[] { Col(source, "Name") } }));
        Assert.Equal(new[] { "Name", "Ed", "Di", "Cy", "Bob", "Ada" }, rows.Select(r => r[0]).ToArray());
    }

    [Fact]
    public void NestedGroups_ReportTheirLevels()
    {
        var source = Source();
        source.AddGroupDescription(new GroupDescription("Dept"));
        source.AddGroupDescription(new GroupDescription("Active"));
        var kinds = KumikoExportSnapshot.Create(source).Rows.Select(r => (r.Kind, r.Level)).ToList();
        Assert.Contains((KumikoExportRowKind.GroupHeader, 0), kinds);
        Assert.Contains((KumikoExportRowKind.GroupHeader, 1), kinds);
        Assert.Contains((KumikoExportRowKind.Data, 2), kinds);
    }

    [Fact]
    public void IncludeSummariesFalse_OmitsThemAll()
    {
        var source = Grouped();
        var kinds = KumikoExportSnapshot.Create(source, new KumikoExportOptions { IncludeSummaries = false }).Rows.Select(r => r.Kind).Distinct().ToList();
        Assert.DoesNotContain(KumikoExportRowKind.GroupSummary, kinds);
        Assert.DoesNotContain(KumikoExportRowKind.TableSummary, kinds);
    }

    [Fact]
    public void TableSummaryOnly_WhenNotGrouped_AndLayoutNone()
    {
        var source = Source();
        source.AddTableSummaryRow(new TableSummaryRow("t") { Title = "Total", Columns = { new SummaryColumnDescription("Salary", SummaryType.Sum) { Label = "" } } });
        var rows = Rfc4180.Parse(source.ExportToCsvString(new KumikoExportOptions { GroupLayout = KumikoExportGroupLayout.None, IncludeSummaries = true, Columns = new[] { Col(source, "Name"), Col(source, "Salary") } }));
        Assert.Equal(new[] { "Total", "5100.75" }, new[] { rows[^1][0], rows[^1][1] });
    }
}
