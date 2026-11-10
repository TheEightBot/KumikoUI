using System.Collections;
using System.Collections.ObjectModel;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Nested grouping, group ordering, key and display selectors, group and caption summary values, and
/// collapsed groups, pinned to the rows a user sees.
/// </summary>
public class KumikoSourceGroupingTests
{
    private sealed class Sale
    {
        public string Region { get; set; } = "";
        public string City { get; set; } = "";
        public string Rep { get; set; } = "";
        public int Amount { get; set; }
        public decimal Price { get; set; }
    }

    private sealed record NullableRow(int? Value);

    private static List<Sale> Sales() => new()
    {
        new Sale { Region = "West", City = "Seattle", Rep = "r1", Amount = 10, Price = 1.5m },
        new Sale { Region = "East", City = "Boston", Rep = "r2", Amount = 20, Price = 2.5m },
        new Sale { Region = "West", City = "Portland", Rep = "r3", Amount = 30, Price = 3.5m },
        new Sale { Region = "East", City = "Boston", Rep = "r4", Amount = 40, Price = 4.5m },
        new Sale { Region = "West", City = "Seattle", Rep = "r5", Amount = 50, Price = 5.5m },
    };

    private static KumikoSource MakeSource(IEnumerable? items = null)
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { PropertyName = "Region", Header = "Region" },
            new KumikoColumn { PropertyName = "City", Header = "City" },
            new KumikoColumn { PropertyName = "Rep", Header = "Rep" },
            new KumikoColumn { PropertyName = "Amount", Header = "Amount", ColumnType = KumikoColumnType.Numeric },
            new KumikoColumn { PropertyName = "Price", Header = "Price", ColumnType = KumikoColumnType.Numeric },
        });
        source.SetItems(items ?? Sales());
        return source;
    }

    private static KumikoColumn Col(KumikoSource s, string property) => s.Columns.Single(c => c.PropertyName == property);

    /// <summary>One entry per visible row: "H{level}:text(count)", "S:index" or the rep of a data row.</summary>
    private static List<string> Rows(KumikoSource s)
    {
        var rows = new List<string>();
        for (int i = 0; i < s.RowCount; i++)
        {
            if (s.GetGroupHeaderInfo(i) is { } h) rows.Add($"H{h.Level}:{h.DisplayText}({h.ItemCount})");
            else if (s.GetGroupSummaryRowInfo(i) is { } sum) rows.Add($"S:{sum.SummaryIndex}");
            else rows.Add(((Sale)s.GetItem(i)).Rep);
        }
        return rows;
    }

    // ── Nested grouping ───────────────────────────────────────────

    [Fact]
    public void NestedGroups_OrderEachLevelByKey_AndKeepSourceOrderInsideALeaf()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("City"));

        Assert.Equal(new[]
        {
            "H0:East(2)", "H1:Boston(2)", "r2", "r4",
            "H0:West(3)", "H1:Portland(1)", "r3", "H1:Seattle(2)", "r1", "r5",
        }, Rows(source));
    }

    [Fact]
    public void NestedGroups_CarryLevelPathAndCountsOfTheirOwnSubtree()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("City"));

        var west = source.GetGroupHeaderInfo(4)!;
        Assert.Equal(0, west.Level);
        Assert.Equal(3, west.ItemCount);
        Assert.Equal("Region=West", west.GroupPath);

        var seattle = source.GetGroupHeaderInfo(7)!;
        Assert.Equal(1, seattle.Level);
        Assert.Equal(2, seattle.ItemCount);
        Assert.Equal("Region=West|City=Seattle", seattle.GroupPath);
        Assert.Equal("City", seattle.PropertyName);
    }

    [Fact]
    public void CollapsingAnOuterGroup_HidesItsInnerGroupsAndRows_AndKeepsTheInnerState()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("City"));

        source.ToggleGroupExpansion(7); // collapse West > Seattle
        Assert.Equal(new[]
        {
            "H0:East(2)", "H1:Boston(2)", "r2", "r4",
            "H0:West(3)", "H1:Portland(1)", "r3", "H1:Seattle(2)",
        }, Rows(source));

        source.ToggleGroupExpansion(4); // collapse West
        Assert.Equal(new[] { "H0:East(2)", "H1:Boston(2)", "r2", "r4", "H0:West(3)" }, Rows(source));

        source.ToggleGroupExpansion(4); // expand West again: Seattle is still collapsed
        Assert.Equal("H1:Seattle(2)", Rows(source)[^1]);
    }

    [Fact]
    public void ExpandAndCollapseAll_ApplyToEveryLevel_AndSetTheDefaultForNewGroups()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("City"));

        source.CollapseAllGroups();
        Assert.Equal(new[] { "H0:East(2)", "H0:West(3)" }, Rows(source));
        Assert.False(source.DefaultGroupExpanded);

        source.ExpandAllGroups();
        Assert.Equal(10, source.RowCount);
        Assert.True(source.DefaultGroupExpanded);
    }

    [Fact]
    public void InsertGroupDescription_PutsTheNewLevelFirst()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("City"));
        source.InsertGroupDescription(0, new GroupDescription("Region"));

        Assert.Equal(new[] { "Region", "City" }, source.GroupDescriptions.Select(g => g.PropertyName));
        Assert.Equal("H0:East(2)", Rows(source)[0]);
        Assert.Equal("H1:Boston(2)", Rows(source)[1]);
    }

    [Fact]
    public void RemovingAnInnerLevel_LeavesTheOuterGroupsWithDataRows()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("City"));

        source.RemoveGroupDescription("City");

        Assert.Equal(new[] { "H0:East(2)", "r2", "r4", "H0:West(3)", "r1", "r3", "r5" }, Rows(source));
    }

    [Fact]
    public void RemovingAnUnknownLevel_ChangesNothing()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        var before = Rows(source);

        source.RemoveGroupDescription("Nope");

        Assert.Equal(before, Rows(source));
        Assert.True(source.IsGroupingActive);
    }

    [Fact]
    public void ClearingTheGroups_RestoresTheFlatRows()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.ClearGroupDescriptions();

        Assert.False(source.IsGroupingActive);
        Assert.Equal(new[] { "r1", "r2", "r3", "r4", "r5" }, Rows(source));
    }

    // ── Group order, selectors, header text ───────────────────────

    [Fact]
    public void GroupSortDirection_OrdersGroupsAscendingDescendingOrBySourceAppearance()
    {
        var source = MakeSource();
        var group = new GroupDescription("Region") { GroupSortDirection = KumikoSortDirection.Descending };
        source.AddGroupDescription(group);
        Assert.Equal(new[] { "H0:West(3)", "r1", "r3", "r5", "H0:East(2)", "r2", "r4" }, Rows(source));

        source.ClearGroupDescriptions();
        group.GroupSortDirection = KumikoSortDirection.None;
        source.AddGroupDescription(group);
        Assert.Equal("H0:West(3)", Rows(source)[0]); // West appears first in the source
        Assert.Equal("H0:East(2)", Rows(source)[4]);
    }

    [Fact]
    public void KeySelector_GroupsByTheSelectedKey_AndReportsIt()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Amount")
        {
            KeySelector = v => (int)v! >= 30 ? "big" : "small",
        });

        Assert.Equal(new[] { "H0:big(3)", "r3", "r4", "r5", "H0:small(2)", "r1", "r2" }, Rows(source));
        Assert.Equal("big", source.GetGroupHeaderInfo(0)!.Key);
    }

    [Fact]
    public void DisplayTextSelector_ShapesTheHeaderText()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region") { DisplayTextSelector = k => $"<{k}>" });

        Assert.Equal("<East>", source.GetGroupHeaderInfo(0)!.DisplayText);
        Assert.Equal("Region=<East>", source.GetGroupHeaderInfo(0)!.GroupPath);
    }

    [Fact]
    public void HeaderText_PrefersTheDescriptionThenTheColumnHeaderThenThePropertyName()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region", "Sales region"));
        Assert.Equal("Sales region", source.GetGroupHeaderInfo(0)!.HeaderText);

        source.ClearGroupDescriptions();
        source.AddGroupDescription(new GroupDescription("Region"));
        Assert.Equal("Region", source.GetGroupHeaderInfo(0)!.HeaderText);

        source.ClearGroupDescriptions();
        source.AddGroupDescription(new GroupDescription("Missing"));
        Assert.Equal("Missing", source.GetGroupHeaderInfo(0)!.HeaderText);
    }

    [Fact]
    public void GroupingOnAPropertyWithNoColumn_PutsEveryRowInOneBlankGroup()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Missing"));

        Assert.Equal(new[] { "H0:(Blank)(5)", "r1", "r2", "r3", "r4", "r5" }, Rows(source));
    }

    [Fact]
    public void SortAndGroupTogether_SortRowsInsideEachGroup()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.SetSort(Col(source, "Amount"), KumikoSortDirection.Descending);

        Assert.Equal(new[] { "H0:East(2)", "r4", "r2", "H0:West(3)", "r5", "r3", "r1" }, Rows(source));
    }

    [Fact]
    public void FilterAndGroupTogether_CountOnlyTheRowsThatPass()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.SetFilter(item => ((Sale)item).Amount >= 30);

        Assert.Equal(new[] { "H0:East(1)", "r4", "H0:West(2)", "r3", "r5" }, Rows(source));
    }

    // ── Group summaries ───────────────────────────────────────────

    private static GroupSummaryRow Totals() => new("Totals")
    {
        Columns =
        {
            new SummaryColumnDescription("Amount", SummaryType.Sum),
            new SummaryColumnDescription("Price", SummaryType.Max),
        },
    };

    [Fact]
    public void GroupSummary_ComputesTheValuesOfEachInnermostGroup()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("City"));
        source.AddGroupSummaryRow(Totals());

        var boston = source.GetGroupSummaries("Region=East|City=Boston")!.Single();
        Assert.Equal("Sum: 60", boston.Values["Amount"]);
        Assert.Equal(60d, boston.RawValues["Amount"]);
        Assert.Equal(4.5m, boston.RawValues["Price"]);

        var seattle = source.GetGroupSummaries("Region=West|City=Seattle")!.Single();
        Assert.Equal(60d, seattle.RawValues["Amount"]);
        Assert.Equal(5.5m, seattle.RawValues["Price"]);

        var portland = source.GetGroupSummaries("Region=West|City=Portland")!.Single();
        Assert.Equal(30d, portland.RawValues["Amount"]);
    }

    [Fact]
    public void GroupSummary_FollowsEachLeafGroupAsARow()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("City"));
        source.AddGroupSummaryRow(Totals());

        Assert.Equal(new[]
        {
            "H0:East(2)", "H1:Boston(2)", "r2", "r4", "S:0",
            "H0:West(3)", "H1:Portland(1)", "r3", "S:0", "H1:Seattle(2)", "r1", "r5", "S:0",
        }, Rows(source));
        Assert.True(source.IsGroupSummaryRow(4));
        Assert.True(source.IsNonDataRow(4));
        Assert.False(source.IsGroupHeaderRow(4));
        Assert.Equal(("Region=East|City=Boston", 0), source.GetGroupSummaryRowInfo(4));
    }

    [Fact]
    public void GroupSummary_ResolvesTheColumnCaseInsensitively_AndFormatsWithTheDescriptionFormat()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupSummaryRow(new GroupSummaryRow("T")
        {
            Columns = { new SummaryColumnDescription("amount", SummaryType.Average, "F1") { Label = "~" } },
        });

        var east = source.GetGroupSummaries("Region=East")!.Single();
        Assert.Equal("~30.0", east.Values["amount"]);
    }

    [Fact]
    public void SeveralGroupSummaryRows_AppearInOrder_UnderTheirGroup()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupSummaryRow(new GroupSummaryRow("A") { Columns = { new("Amount", SummaryType.Sum) } });
        source.AddGroupSummaryRow(new GroupSummaryRow("B") { Columns = { new("Amount", SummaryType.Count) } });

        Assert.Equal(new[] { "H0:East(2)", "r2", "r4", "S:0", "S:1", "H0:West(3)", "r1", "r3", "r5", "S:0", "S:1" }, Rows(source));
        var summaries = source.GetGroupSummaries("Region=West")!;
        Assert.Equal(new[] { "A", "B" }, summaries.Select(s => s.Name));
        Assert.Equal(3, summaries[1].RawValues["Amount"]);
    }

    [Fact]
    public void GroupSummary_OfACollapsedGroup_HasNoRowAndIsNotComputed()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupSummaryRow(Totals());

        source.ToggleGroupExpansion(0); // collapse East

        Assert.Equal(new[] { "H0:East(2)", "H0:West(3)", "r1", "r3", "r5", "S:0" }, Rows(source));
        Assert.Null(source.GetGroupSummaries("Region=East"));
        Assert.Equal(90d, source.GetGroupSummaries("Region=West")!.Single().RawValues["Amount"]);
    }

    [Fact]
    public void GroupSummary_ChangesWithTheFilter()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupSummaryRow(Totals());

        source.SetFilter(item => ((Sale)item).Amount != 10);

        Assert.Equal(80d, source.GetGroupSummaries("Region=West")!.Single().RawValues["Amount"]);
    }

    [Fact]
    public void GroupSummary_RecomputesWhenAnItemIsAdded()
    {
        var items = new ObservableCollection<Sale>(Sales());
        var source = MakeSource(items);
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupSummaryRow(Totals());

        items.Add(new Sale { Region = "East", City = "Boston", Rep = "r6", Amount = 5 });

        Assert.Equal(65d, source.GetGroupSummaries("Region=East")!.Single().RawValues["Amount"]);
    }

    [Fact]
    public void GroupSummary_RemoveAndClear_DropTheRows()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupSummaryRow(Totals());
        Assert.True(source.HasGroupSummaries);

        source.RemoveGroupSummaryRow("Totals");
        Assert.False(source.HasGroupSummaries);
        Assert.Equal(7, source.RowCount);

        source.AddGroupSummaryRow(Totals());
        source.ClearGroupSummaryRows();
        Assert.Empty(source.GroupSummaryRows);
        Assert.Equal(7, source.RowCount);
    }

    [Fact]
    public void GroupSummary_OnlyLeafGroupsGetSummaryRows()
    {
        // Pins today's behavior: an outer level gets no total of its own (all-level summaries are #19).
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("City"));
        source.AddGroupSummaryRow(Totals());

        Assert.Null(source.GetGroupSummaries("Region=East"));
        Assert.Null(source.GetGroupSummaries("Region=West"));
    }

    [Fact]
    public void GroupSummary_Custom_AggregateSeesTheGroupsValues()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupSummaryRow(new GroupSummaryRow("C")
        {
            Columns =
            {
                new SummaryColumnDescription
                {
                    PropertyName = "Rep",
                    SummaryType = SummaryType.Custom,
                    CustomAggregate = values => string.Join("+", values),
                },
            },
        });

        Assert.Equal("r2+r4", source.GetGroupSummaries("Region=East")!.Single().Values["Rep"]);
    }

    // ── Caption summaries ─────────────────────────────────────────

    [Fact]
    public void CaptionSummary_JoinsTheFormattedValuesWithBars_PerGroup()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.CaptionSummary = new CaptionSummaryDescription
        {
            Columns =
            {
                new SummaryColumnDescription("Amount", SummaryType.Sum),
                new SummaryColumnDescription("Rep", SummaryType.Count),
            },
        };

        Assert.Equal("Sum: 60 | Count: 2", source.GetCaptionSummaryText("Region=East"));
        Assert.Equal("Sum: 90 | Count: 3", source.GetCaptionSummaryText("Region=West"));
    }

    [Fact]
    public void CaptionSummary_FormatTemplate_ReplacesTheDefaultJoin()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.CaptionSummary = new CaptionSummaryDescription
        {
            FormatTemplate = "[{1}] total {0}",
            Columns =
            {
                new SummaryColumnDescription("Amount", SummaryType.Sum) { Label = "" },
                new SummaryColumnDescription("Rep", SummaryType.Count) { Label = "n=" },
            },
        };

        Assert.Equal("[n=2] total 60", source.GetCaptionSummaryText("Region=East"));
    }

    [Fact]
    public void CaptionSummary_WithABadTemplate_FallsBackToTheDefaultJoin()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.CaptionSummary = new CaptionSummaryDescription
        {
            FormatTemplate = "{5}",
            Columns = { new SummaryColumnDescription("Amount", SummaryType.Sum) },
        };

        Assert.Equal("Sum: 60", source.GetCaptionSummaryText("Region=East"));
    }

    [Fact]
    public void CaptionSummary_IsAvailableForEveryGroupLevel_EvenWhenCollapsed()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.AddGroupDescription(new GroupDescription("City"));
        source.CaptionSummary = new CaptionSummaryDescription
        {
            Columns = { new SummaryColumnDescription("Amount", SummaryType.Sum) },
        };
        source.CollapseAllGroups();

        Assert.Equal("Sum: 60", source.GetCaptionSummaryText("Region=East"));
        Assert.Equal("Sum: 90", source.GetCaptionSummaryText("Region=West"));
    }

    [Fact]
    public void CaptionSummary_OfAnUnknownGroup_IsNull_AndSettingNullClearsIt()
    {
        var source = MakeSource();
        source.AddGroupDescription(new GroupDescription("Region"));
        source.CaptionSummary = new CaptionSummaryDescription
        {
            Columns = { new SummaryColumnDescription("Amount", SummaryType.Sum) },
        };
        Assert.Null(source.GetCaptionSummaryText("Region=North"));

        source.CaptionSummary = null;
        Assert.Null(source.GetCaptionSummaryText("Region=East"));
    }

    // ── Table summary values ──────────────────────────────────────

    [Fact]
    public void TableSummary_OverNoRows_HasZeroSumAndCount_AndNoAverageMinOrMax()
    {
        var source = MakeSource(new List<Sale>());
        foreach (var type in new[] { SummaryType.Sum, SummaryType.Count, SummaryType.Average, SummaryType.Min, SummaryType.Max })
            source.AddTableSummaryRow(new TableSummaryRow(type.ToString())
            {
                Columns = { new SummaryColumnDescription("Price", type) },
            });

        var raw = source.ComputedBottomSummaries.ToDictionary(r => r.Name, r => r.RawValues["Price"]);
        Assert.Equal(0d, raw["Sum"]);
        Assert.Equal(0, raw["Count"]);
        Assert.Null(raw["Average"]);
        Assert.Null(raw["Min"]);
        Assert.Null(raw["Max"]);
    }

    [Theory]
    [InlineData(SummaryType.Sum, 150d)]
    [InlineData(SummaryType.Average, 30d)]
    [InlineData(SummaryType.Count, 5)]
    [InlineData(SummaryType.Min, 10)]
    [InlineData(SummaryType.Max, 50)]
    public void TableSummary_RawValue_IsTheAggregateOfTheVisibleRows(SummaryType type, object expected)
    {
        var source = MakeSource();
        source.AddTableSummaryRow(new TableSummaryRow("T") { Columns = { new SummaryColumnDescription("Amount", type) } });

        Assert.Equal(expected, source.ComputedBottomSummaries.Single().RawValues["Amount"]);
    }

    [Fact]
    public void TableSummary_DecimalSum_FormatsToTheCent()
    {
        var source = MakeSource();
        source.AddTableSummaryRow(new TableSummaryRow("T") { Columns = { new SummaryColumnDescription("Price", SummaryType.Sum, "F2") } });

        Assert.Equal("Sum: 17.50", source.ComputedBottomSummaries.Single().Values["Price"]);
    }

    [Fact]
    public void TableSummary_SkipsNullValues_ButCountsTheRows()
    {
        var source = new KumikoSource();
        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Value" } });
        source.SetItems(new List<NullableRow> { new(null), new(4), new(8) });
        source.AddTableSummaryRow(new TableSummaryRow("Avg")
        {
            Columns = { new SummaryColumnDescription("Value", SummaryType.Average) },
        });
        source.AddTableSummaryRow(new TableSummaryRow("Cnt")
        {
            Columns = { new SummaryColumnDescription("Value", SummaryType.Count) },
        });

        Assert.Equal(6d, source.ComputedBottomSummaries[0].RawValues["Value"]);
        Assert.Equal(3, source.ComputedBottomSummaries[1].RawValues["Value"]);
    }

    [Fact]
    public void TableSummary_OfAllNullValues_HasNoAverage_AndAnEmptyText()
    {
        var source = new KumikoSource();
        source.SetColumns(new[] { new KumikoColumn { PropertyName = "Value" } });
        source.SetItems(new List<NullableRow> { new(null), new(null) });
        source.AddTableSummaryRow(new TableSummaryRow("T")
        {
            Columns = { new SummaryColumnDescription("Value", SummaryType.Average) { Label = "" } },
        });

        Assert.Null(source.ComputedBottomSummaries.Single().RawValues["Value"]);
        Assert.Equal("", source.ComputedBottomSummaries.Single().Values["Value"]);
    }

    [Fact]
    public void TableSummary_OfANonNumericColumn_SumsToZero_WithoutThrowing()
    {
        var source = MakeSource();
        source.AddTableSummaryRow(new TableSummaryRow("T") { Columns = { new SummaryColumnDescription("Rep", SummaryType.Sum) } });

        Assert.Equal(0d, source.ComputedBottomSummaries.Single().RawValues["Rep"]);
    }

    [Fact]
    public void TableSummary_FormatWithAPlaceholder_UsesStringFormat()
    {
        var source = MakeSource();
        source.AddTableSummaryRow(new TableSummaryRow("T")
        {
            Columns = { new SummaryColumnDescription("Amount", SummaryType.Sum, "{0} units") { Label = "" } },
        });

        Assert.Equal("150 units", source.ComputedBottomSummaries.Single().Values["Amount"]);
    }

    // ── Summary row registries ────────────────────────────────────

    [Fact]
    public void TableSummaryRows_ListTheAddedRowsInOrder_AndCountsFollowTheirPositions()
    {
        var source = MakeSource();
        var top = new TableSummaryRow("Top", SummaryPosition.Top) { Columns = { new("Amount", SummaryType.Sum) } };
        var bottom = new TableSummaryRow("Bottom") { Columns = { new("Amount", SummaryType.Max) } };

        source.AddTableSummaryRow(top);
        source.AddTableSummaryRow(bottom);

        Assert.Equal(new[] { top, bottom }, source.TableSummaryRows);
        Assert.Equal(1, source.TopSummaryCount);
        Assert.Equal(1, source.BottomSummaryCount);
        Assert.Equal("Sum: 150", source.ComputedTopSummaries.Single().Values["Amount"]);
        Assert.Equal("Max: 50", source.ComputedBottomSummaries.Single().Values["Amount"]);
    }

    [Fact]
    public void RemoveTableSummaryRow_ByName_LeavesTheOthers_AndAnUnknownNameChangesNothing()
    {
        var source = MakeSource();
        source.AddTableSummaryRow(new TableSummaryRow("A") { Columns = { new("Amount", SummaryType.Sum) } });
        source.AddTableSummaryRow(new TableSummaryRow("B") { Columns = { new("Amount", SummaryType.Min) } });

        source.RemoveTableSummaryRow("Nope");
        Assert.Equal(2, source.TableSummaryRows.Count);

        source.RemoveTableSummaryRow("A");
        Assert.Equal(new[] { "B" }, source.TableSummaryRows.Select(r => r.Name));
        Assert.Equal(1, source.BottomSummaryCount);
    }

    [Fact]
    public void GroupSummaryRows_ListTheAddedRows_AndRemovingByNameKeepsTheRest()
    {
        var source = MakeSource();
        source.AddGroupSummaryRow(new GroupSummaryRow("A") { Columns = { new("Amount", SummaryType.Sum) } });
        source.AddGroupSummaryRow(new GroupSummaryRow("B") { Columns = { new("Amount", SummaryType.Count) } });

        source.RemoveGroupSummaryRow("A");

        Assert.Equal(new[] { "B" }, source.GroupSummaryRows.Select(r => r.Name));
        Assert.True(source.HasGroupSummaries);
    }

    [Fact]
    public void Refresh_RebuildsTheViewFromTheCurrentItems()
    {
        var items = Sales();
        var source = MakeSource(items);
        source.AddGroupDescription(new GroupDescription("Region"));

        items[0].Region = "East"; // a plain object: no change notification
        Assert.Equal("H0:East(2)", Rows(source)[0]);
        Assert.Equal("H0:West(3)", Rows(source)[3]); // stale until refreshed

        source.Refresh();

        Assert.Equal("H0:East(3)", Rows(source)[0]);
        Assert.Equal("H0:West(2)", Rows(source)[4]);
    }
}
