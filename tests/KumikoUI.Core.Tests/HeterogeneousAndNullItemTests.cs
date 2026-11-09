using System.Collections;
using System.Collections.ObjectModel;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Items of different runtime types in one list resolve each column per item type, and null items are
/// skipped like items a filter rejects (#18).
/// </summary>
public class HeterogeneousAndNullItemTests
{
    private class Animal
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private sealed class Dog : Animal
    {
        public string Breed { get; set; } = "";
    }

    private sealed class Cat : Animal
    {
        public int Lives { get; set; } = 9;
    }

    private sealed class Robot
    {
        public string Name { get; set; } = "";
    }

    private sealed class Plant
    {
        public string Name { get; set; } = "";
    }

    private static List<Animal> Animals() => new()
    {
        new Dog { Name = "Rex", Age = 5, Breed = "Collie" },
        new Cat { Name = "Tom", Age = 3 },
        new Dog { Name = "Fido", Age = 9, Breed = "Beagle" },
        new Cat { Name = "Kit", Age = 7 },
    };

    private static KumikoColumn Col(string property, KumikoColumnType type = KumikoColumnType.Text) =>
        new() { Header = property, PropertyName = property, ColumnType = type };

    private static KumikoSource MakeSource(IEnumerable items, params KumikoColumn[] columns)
    {
        var source = new KumikoSource();
        source.SetColumns(columns.Length > 0 ? columns : new[] { Col("Name"), Col("Age", KumikoColumnType.Numeric) });
        source.SetItems(items);
        return source;
    }

    private static List<string?> Names(KumikoSource source, KumikoColumn name) =>
        Enumerable.Range(0, source.RowCount).Select(r => (string?)source.GetCellValue(r, name)).ToList();

    private static List<object> Items(KumikoSource source) =>
        Enumerable.Range(0, source.RowCount).Select(source.GetItem).ToList();

    // ── Heterogeneous lists ───────────────────────────────────────

    [Fact]
    public void Sort_ByBaseProperty_Ascending_And_Descending()
    {
        var name = Col("Name");
        var age = Col("Age", KumikoColumnType.Numeric);
        var source = MakeSource(Animals(), name, age);

        source.SetSort(age, KumikoSortDirection.Ascending);
        Assert.Equal(new[] { "Tom", "Rex", "Kit", "Fido" }, Names(source, name));

        source.SetSort(age, KumikoSortDirection.Descending);
        Assert.Equal(new[] { "Fido", "Kit", "Rex", "Tom" }, Names(source, name));
    }

    [Fact]
    public void Sort_BySubtypeProperty_NullsFirstAscending_LastDescending()
    {
        var name = Col("Name");
        var breed = Col("Breed");
        var source = MakeSource(Animals(), name, breed);

        // Cats read null for Breed; null sorts before every value ascending and after every value descending.
        source.SetSort(breed, KumikoSortDirection.Ascending);
        var asc = Names(source, name);
        Assert.Equal(new[] { "Tom", "Kit" }, asc.Take(2).OrderBy(n => n).Reverse().ToArray());
        Assert.Equal(new[] { "Fido", "Rex" }, asc.Skip(2).ToArray()); // Beagle, Collie

        source.SetSort(breed, KumikoSortDirection.Descending);
        var desc = Names(source, name);
        Assert.Equal(new[] { "Rex", "Fido" }, desc.Take(2).ToArray()); // Collie, Beagle
        Assert.Equal(new[] { "Kit", "Tom" }, desc.Skip(2).OrderBy(n => n).ToArray());
    }

    [Fact]
    public void Filter_OnBaseProperty()
    {
        var name = Col("Name");
        var age = Col("Age", KumikoColumnType.Numeric);
        var source = MakeSource(Animals(), name, age);

        source.SetColumnFilter(age, new FilterDescription(age) { SelectedValues = new HashSet<string> { "3", "9" } });

        Assert.Equal(new[] { "Tom", "Fido" }, Names(source, name));
    }

    [Fact]
    public void Filter_OnSubtypeProperty_RejectsOtherSubtype()
    {
        var name = Col("Name");
        var breed = Col("Breed");
        var source = MakeSource(Animals(), name, breed);

        source.SetColumnFilter(breed, new FilterDescription(breed) { SelectedValues = new HashSet<string> { "Beagle" } });

        Assert.Equal(new[] { "Fido" }, Names(source, name));
    }

    [Fact]
    public void Group_ByBaseProperty()
    {
        var source = MakeSource(new List<Animal>
        {
            new Dog { Name = "Rex", Age = 5 }, new Cat { Name = "Tom", Age = 5 }, new Dog { Name = "Fido", Age = 9 },
        });
        source.AddGroupDescription(new GroupDescription("Age"));

        var headers = Enumerable.Range(0, source.RowCount)
            .Select(source.GetGroupHeaderInfo).Where(h => h != null).ToList();
        Assert.Equal(new[] { "5", "9" }, headers.Select(h => h!.DisplayText).ToArray());
        Assert.Equal(new[] { 2, 1 }, headers.Select(h => h!.ItemCount).ToArray());
        Assert.Equal(5, source.RowCount); // two headers, three data rows
    }

    [Fact]
    public void Group_BySubtypeProperty_PutsOtherSubtypeInBlankGroup()
    {
        var breed = Col("Breed");
        var source = MakeSource(Animals(), Col("Name"), breed);
        source.AddGroupDescription(new GroupDescription("Breed"));

        var headers = Enumerable.Range(0, source.RowCount)
            .Select(source.GetGroupHeaderInfo).Where(h => h != null).ToList();
        Assert.Equal(3, headers.Count);
        var blank = headers.Single(h => h!.DisplayText == "(Blank)");
        Assert.Equal(2, blank!.ItemCount); // the two cats
    }

    [Fact]
    public void TableSummaries_OverBaseProperty()
    {
        var source = MakeSource(Animals());
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Bottom)
        {
            Columns =
            {
                new SummaryColumnDescription("Age", SummaryType.Sum),
            }
        });
        source.AddTableSummaryRow(new TableSummaryRow("Count", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Count) }
        });
        source.AddTableSummaryRow(new TableSummaryRow("Min", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Min) }
        });
        source.AddTableSummaryRow(new TableSummaryRow("Max", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Max) }
        });

        var rows = source.ComputedBottomSummaries;
        Assert.Equal(24.0, Convert.ToDouble(rows[0].RawValues["Age"]));
        Assert.Equal(4.0, Convert.ToDouble(rows[1].RawValues["Age"]));
        Assert.Equal(3.0, Convert.ToDouble(rows[2].RawValues["Age"]));
        Assert.Equal(9.0, Convert.ToDouble(rows[3].RawValues["Age"]));
    }

    [Fact]
    public void GetCellValue_OfSubtypeProperty_OnOtherSubtype_IsNull()
    {
        var breed = Col("Breed");
        var lives = Col("Lives", KumikoColumnType.Numeric);
        var source = MakeSource(Animals(), Col("Name"), breed, lives);

        Assert.Equal("Collie", source.GetCellValue(0, breed)); // Rex, a Dog
        Assert.Null(source.GetCellValue(0, lives));
        Assert.Null(source.GetCellValue(1, breed));             // Tom, a Cat
        Assert.Equal(9, source.GetCellValue(1, lives));
    }

    [Fact]
    public void UnrelatedTypes_ShareAPropertyName()
    {
        var name = Col("Name");
        var source = MakeSource(new List<object>
        {
            new Robot { Name = "R2" }, new Plant { Name = "Fern" }, new Robot { Name = "C3" }, "text",
        }, name);

        source.SetSort(name, KumikoSortDirection.Ascending);

        // "text" has no Name property: it reads null and sorts first.
        Assert.Equal(new string?[] { null, "C3", "Fern", "R2" }, Names(source, name));

        source.SetColumnFilter(name, new FilterDescription(name) { SelectedValues = new HashSet<string> { "Fern", "R2" } });
        Assert.Equal(new[] { "Fern", "R2" }, Names(source, name));
    }

    // ── Null items ────────────────────────────────────────────────

    private static List<Person?> PeopleWithNulls() => new()
    {
        null,
        new Person { Name = "Alice", Age = 30, Department = "Eng" },
        null,
        new Person { Name = "Bob", Age = 25, Department = "Ops" },
        new Person { Name = "Cara", Age = 35, Department = "Eng" },
        null,
    };

    private static KumikoSource PersonSource(IEnumerable items)
    {
        var source = new KumikoSource();
        source.SetColumns(new[] { Col("Name"), Col("Age", KumikoColumnType.Numeric), Col("Department") });
        source.SetItems(items);
        return source;
    }

    [Fact]
    public void SetItems_ListWithNulls_ExcludesThem()
    {
        var source = PersonSource(PeopleWithNulls());

        Assert.Equal(3, source.RowCount);
        Assert.Equal(new[] { "Alice", "Bob", "Cara" }, Names(source, source.Columns[0]));
        Assert.All(Items(source), Assert.NotNull);
    }

    [Fact]
    public void SetItems_ObjectArrayWithNulls_ExcludesThem()
    {
        object?[] items = { null, new Person { Name = "Alice" }, null, new Robot { Name = "R2" } };
        var source = PersonSource(items);

        Assert.Equal(2, source.RowCount);
        Assert.Equal(new[] { "Alice", "R2" }, Names(source, source.Columns[0]));
    }

    [Fact]
    public void SetItems_AllNulls_HasNoRows()
    {
        var source = PersonSource(new Person?[] { null, null, null });

        Assert.Equal(0, source.RowCount);
    }

    [Fact]
    public void Render_WithNullItems_DoesNotThrow()
    {
        var source = PersonSource(PeopleWithNulls());
        var scroll = new ScrollState { ViewportWidth = 800, ViewportHeight = 400 };
        var ctx = new RecordingDrawingContext();

        new KumikoRenderer().Render(ctx, source, scroll, new KumikoSelectionModel(source), new KumikoStyle());

        Assert.Equal(3, source.RowCount);
    }

    [Fact]
    public void Render_WithNullItemsSortedGroupedAndSummarized_DoesNotThrow()
    {
        var source = PersonSource(PeopleWithNulls());
        source.SetSort(source.Columns[1], KumikoSortDirection.Descending);
        source.AddGroupDescription(new GroupDescription("Department"));
        source.AddTableSummaryRow(new TableSummaryRow("Total", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Sum) }
        });
        var scroll = new ScrollState { ViewportWidth = 800, ViewportHeight = 400 };

        new KumikoRenderer().Render(new RecordingDrawingContext(), source, scroll, new KumikoSelectionModel(source), new KumikoStyle());

        Assert.Equal(90.0, Convert.ToDouble(source.ComputedBottomSummaries[0].RawValues["Age"]));
    }

    [Fact]
    public void Sort_Filter_Group_Summary_IgnoreNulls()
    {
        var name = Col("Name");
        var age = Col("Age", KumikoColumnType.Numeric);
        var dept = Col("Department");
        var source = new KumikoSource();
        source.SetColumns(new[] { name, age, dept });
        source.SetItems(PeopleWithNulls());

        source.SetSort(age, KumikoSortDirection.Ascending);
        Assert.Equal(new[] { "Bob", "Alice", "Cara" }, Names(source, name));
        source.SetSort(age, KumikoSortDirection.Descending);
        Assert.Equal(new[] { "Cara", "Alice", "Bob" }, Names(source, name));
        source.ClearSort();

        source.SetColumnFilter(dept, new FilterDescription(dept) { SelectedValues = new HashSet<string> { "Eng" } });
        Assert.Equal(new[] { "Alice", "Cara" }, Names(source, name));
        source.ClearAllFilters();

        source.AddGroupDescription(new GroupDescription("Department"));
        var headers = Enumerable.Range(0, source.RowCount)
            .Select(source.GetGroupHeaderInfo).Where(h => h != null).ToList();
        Assert.Equal(new[] { 2, 1 }, headers.Select(h => h!.ItemCount).ToArray());
        source.RemoveGroupDescription("Department");

        source.AddTableSummaryRow(new TableSummaryRow("Count", SummaryPosition.Bottom)
        {
            Columns = { new SummaryColumnDescription("Age", SummaryType.Count) }
        });
        Assert.Equal(3.0, Convert.ToDouble(source.ComputedBottomSummaries[0].RawValues["Age"]));
    }

    [Fact]
    public void GetUniqueColumnValues_HasNoEntryForNulls()
    {
        var source = PersonSource(PeopleWithNulls());

        Assert.Equal(new[] { "Eng", "Ops" }, source.GetUniqueColumnValues(source.Columns[2]));
        Assert.Equal(new[] { "Alice", "Bob", "Cara" }, source.GetUniqueColumnValues(source.Columns[0]));
    }

    // ── Null items in an observable collection ────────────────────

    private sealed class Watched
    {
        public ObservableCollection<Person?> Collection { get; }
        public KumikoSource Source { get; }
        public List<KumikoViewChangedEventArgs> Events { get; } = new();

        public Watched(params Person?[] items)
        {
            Collection = new ObservableCollection<Person?>(items);
            Source = PersonSource(Collection);
            Source.ViewChanged += (_, e) => Events.Add(e);
        }

        public string[] Visible => Names(Source, Source.Columns[0]).Select(n => n!).ToArray();
    }

    private static Person P(string name) => new() { Name = name, Age = name.Length };

    [Fact]
    public void Observable_AddNull_ShowsNoRow()
    {
        var w = new Watched(P("Al"), P("Bob"));

        w.Collection.Add(null);

        Assert.Equal(new[] { "Al", "Bob" }, w.Visible);
        Assert.Single(w.Events);
    }

    [Fact]
    public void Observable_AddNull_ThenAddRealItem_AppendsRow()
    {
        var w = new Watched(P("Al"));
        w.Collection.Add(null);
        w.Events.Clear();

        w.Collection.Add(P("Cy"));

        Assert.Equal(new[] { "Al", "Cy" }, w.Visible);
        Assert.Single(w.Events);
    }

    [Fact]
    public void Observable_InsertNull_ShiftsLaterRows()
    {
        var w = new Watched(P("Al"), P("Bob"), P("Cy"));

        w.Collection.Insert(1, null);

        Assert.Equal(new[] { "Al", "Bob", "Cy" }, w.Visible);
        Assert.Single(w.Events);

        // Source indices shifted correctly: removing "Bob" (now at source index 2) removes the right row.
        w.Events.Clear();
        w.Collection.RemoveAt(2);
        Assert.Equal(new[] { "Al", "Cy" }, w.Visible);
        Assert.Single(w.Events);
    }

    [Fact]
    public void Observable_RemoveNullEntry_KeepsViewCorrect()
    {
        var w = new Watched(P("Al"), null, P("Bob"), P("Cy"));

        w.Collection.RemoveAt(1);

        Assert.Equal(new[] { "Al", "Bob", "Cy" }, w.Visible);
        Assert.Single(w.Events);
        Assert.Same(w.Collection[1], w.Source.GetItem(1));
    }

    [Fact]
    public void Observable_RemoveRealItemAfterNulls_RemovesTheRightRow()
    {
        var w = new Watched(null, null, P("Al"), null, P("Bob"));

        w.Collection.RemoveAt(2);

        Assert.Equal(new[] { "Bob" }, w.Visible);
        Assert.Single(w.Events);
    }

    [Fact]
    public void Observable_ReplaceRealItemWithNull_RemovesRow()
    {
        var w = new Watched(P("Al"), P("Bob"), P("Cy"));

        w.Collection[1] = null;

        Assert.Equal(new[] { "Al", "Cy" }, w.Visible);
        Assert.Single(w.Events);
    }

    [Fact]
    public void Observable_ReplaceNullWithRealItem_AddsRow()
    {
        var w = new Watched(P("Al"), null, P("Cy"));

        w.Collection[1] = P("Bob");

        Assert.Equal(new[] { "Al", "Bob", "Cy" }, w.Visible);
        Assert.Single(w.Events);
    }

    [Fact]
    public void Observable_ReplaceNullWithNull_StaysCorrect()
    {
        var w = new Watched(P("Al"), null);

        w.Collection[1] = null;

        Assert.Equal(new[] { "Al" }, w.Visible);
        Assert.Single(w.Events);
    }

    [Fact]
    public void Observable_NullEntries_WhileSorted_StayExcluded()
    {
        var w = new Watched(P("Cy"), null, P("Al"));
        w.Source.SetSort(w.Source.Columns[0], KumikoSortDirection.Ascending);
        w.Events.Clear();

        w.Collection.Add(null);
        w.Collection.Insert(0, null);

        Assert.Equal(new[] { "Al", "Cy" }, w.Visible);
        Assert.Equal(2, w.Events.Count); // one per change
    }

    [Fact]
    public void Observable_Clear_WithNulls_HasNoRows()
    {
        var w = new Watched(null, P("Al"), null);

        w.Collection.Clear();

        Assert.Equal(0, w.Source.RowCount);
        Assert.Single(w.Events);
    }

    [Fact]
    public void ReorderRow_WithNullsInTheSource_MovesTheRightItems()
    {
        var rex = new Dog { Name = "Rex" };
        var tom = new Cat { Name = "Tom" };
        var kit = new Cat { Name = "Kit" };
        var items = new List<Animal?> { null, rex, null, tom, kit, null };
        var name = Col("Name");
        var source = MakeSource(items, name);

        source.ReorderRow(0, 2); // Rex after Kit

        Assert.Equal(new[] { "Tom", "Kit", "Rex" }, Names(source, name));
        Assert.Equal(3, items.Count(x => x is null)); // nulls stay in the collection
    }
}
