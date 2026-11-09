using System.Globalization;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Sorting, grouping, filtering and Min/Max never throw and follow one set of culture and case rules (#15).
/// </summary>
public class SafeComparisonTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";
        public object? Value { get; set; }
        public int Number { get; set; }
        public DateTime When { get; set; }
        public string City { get; set; } = "";
    }

    private sealed class Opaque(string label)
    {
        public override string ToString() => label;
    }

    private static readonly KumikoColumn[] Columns =
    {
        new() { Header = "Name", PropertyName = "Name" },
        new() { Header = "Value", PropertyName = "Value" },
        new() { Header = "Number", PropertyName = "Number", ColumnType = KumikoColumnType.Numeric },
        new() { Header = "When", PropertyName = "When", ColumnType = KumikoColumnType.Date, Format = "dd/MM/yyyy" },
        new() { Header = "City", PropertyName = "City" },
    };

    private static KumikoSource Source(params Row[] rows)
    {
        var source = new KumikoSource { Culture = CultureInfo.InvariantCulture };
        source.SetColumns(Columns.Select(c => new KumikoColumn
        {
            Header = c.Header, PropertyName = c.PropertyName, ColumnType = c.ColumnType, Format = c.Format,
        }).ToArray());
        source.SetItems(rows.ToList());
        return source;
    }

    private static KumikoColumn Col(KumikoSource source, string property) => source.Columns.Single(c => c.PropertyName == property);

    private static IEnumerable<string> Names(KumikoSource source) =>
        Enumerable.Range(0, source.RowCount).Where(r => !source.IsNonDataRow(r)).Select(r => ((Row)source.GetItem(r)).Name);

    // ── KumikoComparer ────────────────────────────────────────────

    [Fact]
    public void Comparer_OrdersMixedValues_WithoutThrowing()
    {
        var c = new KumikoComparer(CultureInfo.InvariantCulture);

        Assert.True(c.Compare(null, 1) < 0);
        Assert.True(c.Compare(2, 10.5) < 0);              // int vs double, by value
        Assert.True(c.Compare(3.5m, 3L) > 0);             // decimal vs long
        Assert.Equal(0, c.Compare(2, 2.0));
        Assert.True(c.Compare(new DateTime(2026, 1, 1), new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)) < 0);
        Assert.True(c.Compare(new DateOnly(2025, 12, 31), new DateTime(2026, 1, 1)) < 0);
        Assert.True(c.Compare("apple", "Banana") < 0);    // case-insensitive
        Assert.True(c.Compare(DayOfWeek.Monday, DayOfWeek.Friday) < 0);
        Assert.True(c.Compare(new Opaque("a"), new Opaque("b")) < 0);   // not comparable: by text
        Assert.NotEqual(0, c.Compare("1", 1));            // unrelated types: text, then type name — deterministic
        Assert.Equal(-Math.Sign(c.Compare("1", 1)), Math.Sign(c.Compare(1, "1")));
        Assert.True(c.Compare(true, "x") != 0);
    }

    [Fact]
    public void Comparer_Ordinal_IsCaseSensitive()
    {
        var c = new KumikoComparer(CultureInfo.InvariantCulture, CompareOptions.Ordinal);

        Assert.True(c.Compare("apple", "Banana") > 0);
        Assert.False(c.TextEquals("Oslo", "oslo"));
    }

    // ── Sorting never throws, and is deterministic ────────────────

    [Fact]
    public void Sort_MixedIntAndDouble_OrdersNumerically()
    {
        var source = Source(
            new Row { Name = "a", Value = 10 }, new Row { Name = "b", Value = 2.5 },
            new Row { Name = "c", Value = 3m }, new Row { Name = "d", Value = null }, new Row { Name = "e", Value = 1L });

        source.SetSort(Col(source, "Value"), KumikoSortDirection.Ascending);

        Assert.Equal(new[] { "d", "e", "b", "c", "a" }, Names(source)); // null first
    }

    [Fact]
    public void Sort_NonComparableValues_DoesNotThrow_AndOrdersByText()
    {
        var source = Source(
            new Row { Name = "a", Value = new Opaque("zeta") }, new Row { Name = "b", Value = new Opaque("alpha") },
            new Row { Name = "c", Value = 7 }, new Row { Name = "d", Value = "mid" });

        source.SetSort(Col(source, "Value"), KumikoSortDirection.Ascending);

        Assert.Equal(new[] { "c", "b", "d", "a" }, Names(source)); // "7" < "alpha" < "mid" < "zeta"
    }

    [Fact]
    public void Sort_IsStable_EqualValuesKeepSourceOrder()
    {
        var rows = Enumerable.Range(0, 50).Select(i => new Row { Name = $"r{i:D2}", Number = i % 3 }).ToArray();
        var source = Source(rows);

        source.SetSort(Col(source, "Number"), KumikoSortDirection.Ascending);
        var first = Names(source).ToList();
        source.SetSort(Col(source, "Number"), KumikoSortDirection.Descending);
        source.SetSort(Col(source, "Number"), KumikoSortDirection.Ascending);

        Assert.Equal(first, Names(source));
        Assert.Equal(rows.Where(r => r.Number == 0).Select(r => r.Name), first.Take(17));
    }

    [Fact]
    public void Sort_WholeItemColumn_DoesNotThrow()
    {
        var source = Source(new Row { Name = "b" }, new Row { Name = "a" });
        var whole = new KumikoColumn { Header = "Row", PropertyName = "", ColumnType = KumikoColumnType.Template };
        source.SetColumns(source.Columns.Append(whole).ToArray());

        source.SetSort(whole, KumikoSortDirection.Ascending); // row objects are not IComparable

        Assert.Equal(new[] { "b", "a" }, Names(source)); // equal by text: source order kept
    }

    [Fact]
    public void TemplateHeaderTap_DoesNotSortOrThrow()
    {
        var source = Source(new Row { Name = "b" }, new Row { Name = "a" });
        var template = new KumikoColumn { Header = "Actions", PropertyName = "", ColumnType = KumikoColumnType.Template, Width = 100 };
        source.SetColumns(new[] { template });
        var selection = new KumikoSelectionModel(source);
        var input = new KumikoInputController();
        var style = new KumikoStyle();
        var scroll = new ScrollState { ViewportWidth = 400, ViewportHeight = 300 };
        new KumikoRenderer().Render(new TestHelpers.RecordingDrawingContext(), source, scroll, selection, style);

        Assert.False(template.AllowSorting);
        input.HandlePointer(new KumikoPointerEventArgs { X = 30, Y = 20, Action = InputAction.Pressed }, scroll, selection, style, source);
        input.HandlePointer(new KumikoPointerEventArgs { X = 30, Y = 20, Action = InputAction.Released }, scroll, selection, style, source);

        Assert.Empty(source.ActiveSortColumns);
    }

    [Theory]
    [InlineData(KumikoColumnType.Template, "", false)]
    [InlineData(KumikoColumnType.Image, "Photo", false)]
    [InlineData(KumikoColumnType.Text, "", false)]
    [InlineData(KumikoColumnType.Text, "Name", true)]
    [InlineData(KumikoColumnType.Numeric, "Age", true)]
    public void AllowSorting_DefaultsToWhetherThereIsAValueToOrderBy(KumikoColumnType type, string property, bool expected)
    {
        var column = new KumikoColumn { ColumnType = type, PropertyName = property };
        Assert.Equal(expected, column.AllowSorting);

        column.AllowSorting = !expected;
        Assert.Equal(!expected, column.AllowSorting);
    }

    // ── Groups order by key ───────────────────────────────────────

    [Fact]
    public void Groups_ByInt_OrderNumerically()
    {
        var source = Source(new[] { 10, 2, 1, 11, 2 }.Select(n => new Row { Name = $"n{n}", Number = n }).ToArray());

        source.AddGroupDescription(new GroupDescription("Number"));

        var headers = Enumerable.Range(0, source.RowCount).Select(source.GetGroupHeaderInfo).Where(h => h != null).Select(h => h!.DisplayText);
        Assert.Equal(new[] { "1", "2", "10", "11" }, headers);
    }

    [Fact]
    public void Groups_ByDate_OrderChronologically_AndUseTheColumnFormat()
    {
        var source = Source(
            new Row { Name = "a", When = new DateTime(2026, 2, 1) },
            new Row { Name = "b", When = new DateTime(2025, 12, 31) },
            new Row { Name = "c", When = new DateTime(2026, 1, 15) });

        source.AddGroupDescription(new GroupDescription("When"));

        var headers = Enumerable.Range(0, source.RowCount).Select(source.GetGroupHeaderInfo).Where(h => h != null).Select(h => h!.DisplayText);
        Assert.Equal(new[] { "31/12/2025", "15/01/2026", "01/02/2026" }, headers); // not text order
    }

    // ── One set of text rules for sort, group and filter ──────────

    private static KumikoSource Cities(CompareOptions options)
    {
        var source = Source(
            new Row { Name = "1", City = "oslo" }, new Row { Name = "2", City = "Bergen" },
            new Row { Name = "3", City = "Oslo" }, new Row { Name = "4", City = "alta" });
        source.StringCompareOptions = options;
        return source;
    }

    [Fact]
    public void IgnoreCase_SortGroupAndFilterAgree()
    {
        var source = Cities(CompareOptions.IgnoreCase);
        var city = Col(source, "City");

        source.SetSort(city, KumikoSortDirection.Ascending);
        Assert.Equal(new[] { "4", "2", "1", "3" }, Names(source)); // alta, Bergen, oslo, Oslo (stable)

        Assert.Equal(new[] { "alta", "Bergen", "oslo" }, source.GetUniqueColumnValues(city)); // oslo/Oslo is one value

        source.SetColumnFilter(city, new FilterDescription(city) { SelectedValues = new() { "OSLO" } });
        Assert.Equal(new[] { "1", "3" }, Names(source));

        source.ClearAllFilters();
        source.AddGroupDescription(new GroupDescription("City"));
        var groups = Enumerable.Range(0, source.RowCount).Select(source.GetGroupHeaderInfo).Where(h => h != null).ToList();
        Assert.Equal(3, groups.Count);
        Assert.Equal(2, groups[2]!.ItemCount);
    }

    [Fact]
    public void Ordinal_SortGroupAndFilterAgree()
    {
        var source = Cities(CompareOptions.Ordinal);
        var city = Col(source, "City");

        source.SetSort(city, KumikoSortDirection.Ascending);
        Assert.Equal(new[] { "2", "3", "4", "1" }, Names(source)); // Bergen, Oslo, alta, oslo

        Assert.Equal(new[] { "Bergen", "Oslo", "alta", "oslo" }, source.GetUniqueColumnValues(city));

        source.SetColumnFilter(city, new FilterDescription(city)
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Equals, Value = "oslo" },
        });
        Assert.Equal(new[] { "1" }, Names(source));

        source.ClearAllFilters();
        source.AddGroupDescription(new GroupDescription("City"));
        Assert.Equal(4, Enumerable.Range(0, source.RowCount).Count(r => source.IsGroupHeaderRow(r)));
    }

    [Fact]
    public void Culture_FormatsAndParsesValues()
    {
        var source = Source(new Row { Name = "a", Value = 3.5 }, new Row { Name = "b", Value = 12.25 });
        source.Culture = CultureInfo.GetCultureInfo("de-DE");
        var value = Col(source, "Value");
        value.Format = "N2";

        Assert.Equal("3,50", source.GetCellDisplayText(0, value));

        var number = new KumikoColumn { PropertyName = "Value", ColumnType = KumikoColumnType.Numeric };
        source.SetColumns(new[] { Col(source, "Name"), number });
        source.SetColumnFilter(number, new FilterDescription(number)
        {
            Condition1 = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = "4,5" },
        });
        Assert.Equal(new[] { "b" }, Names(source));
    }

    // ── Summaries ─────────────────────────────────────────────────

    [Fact]
    public void MinMax_OverMixedNumericTypes_DoNotThrow()
    {
        var source = Source(new Row { Name = "a", Value = 10 }, new Row { Name = "b", Value = 2.5 }, new Row { Name = "c", Value = 7m });

        source.AddTableSummaryRow(new TableSummaryRow("T")
        {
            Columns = { new SummaryColumnDescription("Value", SummaryType.Max) },
        });

        // By value, not by text (which would make "7" the largest).
        Assert.Equal("Max: 10", source.ComputedBottomSummaries[0].Values["Value"]);
    }
}
